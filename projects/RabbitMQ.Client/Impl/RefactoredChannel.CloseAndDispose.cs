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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQ.Client.Impl
{
    internal partial class RefactoredChannel
    {
        public Task CloseAsync(ushort replyCode, string replyText, bool abort,
            CancellationToken cancellationToken)
        {
            var args = new ShutdownEventArgs(ShutdownInitiator.Application, replyCode, replyText);
            return CloseAsync(args, abort, cancellationToken);
        }

        public async Task CloseAsync(ShutdownEventArgs args, bool abort,
            CancellationToken cancellationToken)
        {
            CancellationToken argCancellationToken = cancellationToken;
            if (IsOpen)
            {
                // Note: we really do need to try and close this channel!
                cancellationToken = CancellationToken.None;
            }

            return await HandleRpcExceptionsAsync<bool>(async () =>
            {
                var k = new ChannelCloseAsyncRpcContinuation(ContinuationTimeout, cancellationToken);

                await _rpcSemaphore.WaitAsync(k.CancellationToken).ConfigureAwait(false);
                bool enqueued = false;
                try
                {
                    ChannelShutdownAsync += k.OnConnectionShutdownAsync;
                    enqueued = Enqueue(k);
                    ConsumerDispatcher.Quiesce();

                    if (SetCloseReason(args))
                    {
                        var method = new Framing.ChannelClose(
                            args.ReplyCode, args.ReplyText, args.ClassId, args.MethodId);
                        await ModelSendAsync(in method, k.CancellationToken).ConfigureAwait(false);
                    }

                    bool result = await k;
                    Debug.Assert(result);

                    await ConsumerDispatcher.WaitForShutdownAsync().ConfigureAwait(false);
                    return true;
                }
                finally
                {
                    MaybeDisposeContinuation(enqueued, k);
                    _rpcSemaphore.Release();
                    ChannelShutdownAsync -= k.OnConnectionShutdownAsync;
                    argCancellationToken.ThrowIfCancellationRequested();
                }
            }, abort);
        }

        internal async Task FinishCloseAsync(CancellationToken cancellationToken)
        {
            ShutdownEventArgs? reason = CloseReason;
            if (reason != null)
            {
                await Session.CloseAsync(reason).ConfigureAwait(false);
            }

            m_connectionStartCell?.TrySetResult(null);
        }

        [MemberNotNull(nameof(_closeReason))]
        internal bool SetCloseReason(ShutdownEventArgs reason)
        {
            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            // NB: this ensures that CloseAsync is only called once on a channel
            return Interlocked.CompareExchange(ref _closeReason, reason, null) is null;
        }

        private bool Enqueue(IRpcContinuation k)
        {
            if (IsOpen)
            {
                _continuationQueue.Enqueue(k);
                return true;
            }
            else
            {
                k.HandleChannelShutdown(CloseReason);
                return false;
            }
        }

        private void MaybeDisposeContinuation(bool enqueued, IRpcContinuation continuation)
        {
            try
            {
                if (enqueued)
                {
                    if (_continuationQueue.TryPeek(out IRpcContinuation? enqueuedContinuation))
                    {
                        if (object.ReferenceEquals(continuation, enqueuedContinuation))
                        {
                            IRpcContinuation dequeuedContinuation = _continuationQueue.Next();
                            dequeuedContinuation.Dispose();
                        }
                    }
                }
                else
                {
                    continuation.Dispose();
                }
            }
            catch
            {
                // TODO low-level debug logging
            }
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (IsDisposing)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    if (IsOpen)
                    {
                        this.AbortAsync().GetAwaiter().GetResult();
                    }

                    _serverOriginatedChannelCloseTcs?.Task.Wait(InternalConstants.DefaultChannelDisposeTimeout);

                    ConsumerDispatcher.Dispose();
                }
                finally
                {
                    try
                    {
                        _rpcSemaphore.Dispose();
                    }
                    catch
                    {
                    }

                    _disposed = true;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(false);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_disposed)
            {
                return;
            }

            if (IsDisposing)
            {
                return;
            }

            try
            {
                if (IsOpen)
                {
                    await this.AbortAsync().ConfigureAwait(false);
                }

                if (_serverOriginatedChannelCloseTcs is not null)
                {
                    await _serverOriginatedChannelCloseTcs.Task.WaitAsync(InternalConstants.DefaultChannelDisposeTimeout)
                        .ConfigureAwait(false);
                }

                ConsumerDispatcher.Dispose();
            }
            finally
            {
                try
                {
                    _rpcSemaphore.Dispose();
                }
                catch
                {
                }

                _disposed = true;
            }
        }

        private bool IsDisposing
        {
            get
            {
                if (Interlocked.Exchange(ref _isDisposing, 1) != 0)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
