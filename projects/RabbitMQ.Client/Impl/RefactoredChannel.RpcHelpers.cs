// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    /// <summary>
    /// Refactored Channel class with improved RPC handling
    /// </summary>
    internal partial class RefactoredChannel : IChannel, IRecoverable
    {
        /// <summary>
        /// Executes an RPC operation with proper exception handling and resource management
        /// </summary>
        /// <typeparam name="T">The return type of the RPC operation</typeparam>
        /// <typeparam name="TContinuation">The type of RPC continuation</typeparam>
        /// <param name="continuationFactory">Factory function to create the continuation</param>
        /// <param name="sendMethodFunc">Function to send the AMQP method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the RPC operation</returns>
        private async Task<T> ExecuteRpcAsync<T, TContinuation>(
            Func<TContinuation> continuationFactory,
            Func<TContinuation, CancellationToken, ValueTask> sendMethodFunc,
            CancellationToken cancellationToken)
            where TContinuation : IRpcContinuation, IAsyncResult<T>
        {
            bool enqueued = false;
            TContinuation k = continuationFactory();

            await _rpcSemaphore.WaitAsync(k.CancellationToken).ConfigureAwait(false);
            try
            {
                enqueued = Enqueue(k);
                
                await sendMethodFunc(k, k.CancellationToken).ConfigureAwait(false);
                
                try
                {
                    return await k;
                }
                catch (OperationCanceledException)
                {
                    _continuationQueue.RpcCanceled(k.HandledProtocolCommandIds);
                    throw;
                }
            }
            finally
            {
                MaybeDisposeContinuation(enqueued, k);
                _rpcSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes an RPC operation that returns a boolean result
        /// </summary>
        /// <typeparam name="TContinuation">The type of RPC continuation</typeparam>
        /// <param name="continuationFactory">Factory function to create the continuation</param>
        /// <param name="sendMethodFunc">Function to send the AMQP method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task that completes when the RPC operation completes</returns>
        private async Task ExecuteBooleanRpcAsync<TContinuation>(
            Func<TContinuation> continuationFactory,
            Func<TContinuation, CancellationToken, ValueTask> sendMethodFunc,
            CancellationToken cancellationToken)
            where TContinuation : IRpcContinuation, IAsyncResult<bool>
        {
            bool result = await ExecuteRpcAsync<bool, TContinuation>(
                continuationFactory,
                sendMethodFunc,
                cancellationToken).ConfigureAwait(false);
                
            Debug.Assert(result);
        }

        /// <summary>
        /// Executes an RPC operation with no-wait option
        /// </summary>
        /// <param name="sendMethodFunc">Function to send the AMQP method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task that completes when the method is sent</returns>
        private async Task ExecuteNoWaitAsync(
            Func<CancellationToken, ValueTask> sendMethodFunc,
            CancellationToken cancellationToken)
        {
            await _rpcSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await sendMethodFunc(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _rpcSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes an RPC operation with proper exception handling for abort scenarios
        /// </summary>
        /// <typeparam name="T">The return type of the RPC operation</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="abort">Whether to ignore certain exceptions</param>
        /// <returns>The result of the operation or default value if aborted</returns>
        private async Task<T> HandleRpcExceptionsAsync<T>(Func<Task<T>> operation, bool abort = false)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (AlreadyClosedException)
            {
                if (!abort)
                {
                    throw;
                }
                return default!;
            }
            catch (IOException)
            {
                if (!abort)
                {
                    throw;
                }
                return default!;
            }
            catch (Exception)
            {
                if (!abort)
                {
                    throw;
                }
                return default!;
            }
        }

        /// <summary>
        /// Creates an effective cancellation token that includes a timeout
        /// </summary>
        /// <param name="userToken">User-provided cancellation token</param>
        /// <param name="timeout">Timeout duration</param>
        /// <returns>A linked cancellation token</returns>
        private CancellationToken GetEffectiveCancellationToken(CancellationToken userToken, TimeSpan timeout)
        {
            var timeoutCts = new CancellationTokenSource(timeout);
            return CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, userToken).Token;
        }
    }
}
