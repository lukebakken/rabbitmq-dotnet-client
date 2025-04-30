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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class RefactoredChannel
    {
        public virtual ValueTask BasicAckAsync(ulong deliveryTag, bool multiple,
            CancellationToken cancellationToken)
        {
            var method = new BasicAck(deliveryTag, multiple);
            return ModelSendAsync(in method, cancellationToken);
        }

        public virtual ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicNack(deliveryTag, multiple, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

        public virtual ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue,
            CancellationToken cancellationToken)
        {
            var method = new BasicReject(deliveryTag, requeue);
            return ModelSendAsync(in method, cancellationToken);
        }

        public async Task BasicCancelAsync(string consumerTag, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new BasicCancel(consumerTag, noWait);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => 
                    {
                        await ModelSendAsync(in method, token).ConfigureAwait(false);
                        ConsumerDispatcher.GetAndRemoveConsumer(consumerTag);
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteRpcAsync<bool, BasicCancelAsyncRpcContinuation>(
                    () => new BasicCancelAsyncRpcContinuation(consumerTag, ConsumerDispatcher, ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive,
            IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer,
            CancellationToken cancellationToken)
        {
            return await ExecuteRpcAsync<string, BasicConsumeAsyncRpcContinuation>(
                () => new BasicConsumeAsyncRpcContinuation(consumer, ConsumerDispatcher, ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new BasicConsume(queue, consumerTag, noLocal, autoAck, exclusive, false, arguments);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck,
            CancellationToken cancellationToken)
        {
            BasicGetResult? result = await ExecuteRpcAsync<BasicGetResult?, BasicGetAsyncRpcContinuation>(
                () => new BasicGetAsyncRpcContinuation(AdjustDeliveryTag, ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new BasicGet(queue, autoAck);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            using Activity? activity = result != null
                ? RabbitMQActivitySource.BasicGet(result.RoutingKey,
                    result.Exchange,
                    result.DeliveryTag, result.BasicProperties, result.Body.Length)
                : RabbitMQActivitySource.BasicGetEmpty(queue);

            activity?.SetStartTime(result != null ? result.StartTime : DateTimeOffset.UtcNow);

            return result;
        }

        public async Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global,
            CancellationToken cancellationToken)
        {
            await ExecuteBooleanRpcAsync<BasicQosAsyncRpcContinuation>(
                () => new BasicQosAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new BasicQos(prefetchSize, prefetchCount, global);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        protected virtual ulong AdjustDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<T>(in T method, CancellationToken cancellationToken) where T : struct, IOutgoingAmqpMethod
        {
            return Session.TransmitAsync(in method, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ValueTask ModelSendAsync<TMethod, THeader>(in TMethod method, in THeader header, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
            where TMethod : struct, IOutgoingAmqpMethod
            where THeader : IAmqpHeader
        {
            return Session.TransmitAsync(in method, in header, body, cancellationToken);
        }
    }
}
