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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class RefactoredChannel
    {
        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue,
            CancellationToken cancellationToken)
        {
            return QueueDeclareAsync(queue: queue, passive: true,
                durable: false, exclusive: false, autoDelete: false,
                noWait: false, arguments: null, cancellationToken: cancellationToken);
        }

        public async Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object?>? arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
        {
            if (noWait && queue == string.Empty)
            {
                throw new InvalidOperationException("noWait must not be used with a server-named queue.");
            }

            if (noWait && passive)
            {
                throw new InvalidOperationException("It does not make sense to use noWait: true and passive: true");
            }

            var method = new QueueDeclare(queue, passive, durable, exclusive, autoDelete, noWait, arguments);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => 
                    {
                        await ModelSendAsync(in method, token).ConfigureAwait(false);
                        if (!passive)
                        {
                            CurrentQueue = queue;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
                
                return new QueueDeclareOk(queue, 0, 0);
            }
            else
            {
                QueueDeclareOk result = await ExecuteRpcAsync<QueueDeclareOk, QueueDeclareAsyncRpcContinuation>(
                    () => new QueueDeclareAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                
                if (!passive)
                {
                    CurrentQueue = result.QueueName;
                }
                
                return result;
            }
        }

        public async Task QueueBindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object?>? arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new QueueBind(queue, exchange, routingKey, noWait, arguments);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteBooleanRpcAsync<QueueBindAsyncRpcContinuation>(
                    () => new QueueBindAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<uint> MessageCountAsync(string queue,
            CancellationToken cancellationToken)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue, cancellationToken)
                .ConfigureAwait(false);
            return ok.MessageCount;
        }

        public async Task<uint> ConsumerCountAsync(string queue,
            CancellationToken cancellationToken)
        {
            QueueDeclareOk ok = await QueueDeclarePassiveAsync(queue, cancellationToken)
                .ConfigureAwait(false);
            return ok.ConsumerCount;
        }

        public async Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new QueueDelete(queue, ifUnused, ifEmpty, noWait);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                
                return 0;
            }
            else
            {
                return await ExecuteRpcAsync<uint, QueueDeleteAsyncRpcContinuation>(
                    () => new QueueDeleteAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken)
        {
            return await ExecuteRpcAsync<uint, QueuePurgeAsyncRpcContinuation>(
                () => new QueuePurgeAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new QueuePurge(queue, false);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task QueueUnbindAsync(string queue, string exchange, string routingKey,
            IDictionary<string, object?>? arguments,
            CancellationToken cancellationToken)
        {
            await ExecuteBooleanRpcAsync<QueueUnbindAsyncRpcContinuation>(
                () => new QueueUnbindAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new QueueUnbind(queue, exchange, routingKey, arguments);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
