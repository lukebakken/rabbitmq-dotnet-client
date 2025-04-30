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
        public async Task ExchangeBindAsync(string destination, string source, string routingKey,
            IDictionary<string, object?>? arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new ExchangeBind(destination, source, routingKey, noWait, arguments);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteBooleanRpcAsync<ExchangeBindAsyncRpcContinuation>(
                    () => new ExchangeBindAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken)
        {
            return ExchangeDeclareAsync(exchange: exchange, type: string.Empty, passive: true,
                durable: false, autoDelete: false, arguments: null, noWait: false,
                cancellationToken: cancellationToken);
        }

        public async Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object?>? arguments, bool passive, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new ExchangeDeclare(exchange, type, passive, durable, autoDelete, false, noWait, arguments);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteBooleanRpcAsync<ExchangeDeclareAsyncRpcContinuation>(
                    () => new ExchangeDeclareAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new ExchangeDelete(exchange, ifUnused, Nowait: noWait);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteBooleanRpcAsync<ExchangeDeleteAsyncRpcContinuation>(
                    () => new ExchangeDeleteAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ExchangeUnbindAsync(string destination, string source, string routingKey,
            IDictionary<string, object?>? arguments, bool noWait,
            CancellationToken cancellationToken)
        {
            var method = new ExchangeUnbind(destination, source, routingKey, noWait, arguments);

            if (noWait)
            {
                await ExecuteNoWaitAsync(
                    async (token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteBooleanRpcAsync<ExchangeUnbindAsyncRpcContinuation>(
                    () => new ExchangeUnbindAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                    async (k, token) => await ModelSendAsync(in method, token).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
