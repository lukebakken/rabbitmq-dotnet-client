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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class RefactoredChannel
    {
        internal async ValueTask ConnectionOpenAsync(string virtualHost, CancellationToken cancellationToken)
        {
            using var timeoutTokenSource = new CancellationTokenSource(HandshakeContinuationTimeout);
            using var lts = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
            var method = new ConnectionOpen(virtualHost);
            // Note: must be awaited or else the timeoutTokenSource instance will be disposed
            await ModelSendAsync(in method, lts.Token).ConfigureAwait(false);
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionSecureOkAsync(byte[] response,
            CancellationToken cancellationToken)
        {
            return await ExecuteRpcAsync<ConnectionSecureOrTune, ConnectionSecureOrTuneAsyncRpcContinuation>(
                () => new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new ConnectionSecureOk(response);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask<ConnectionSecureOrTune> ConnectionStartOkAsync(
            IDictionary<string, object?> clientProperties,
            string mechanism, byte[] response, string locale,
            CancellationToken cancellationToken)
        {
            return await ExecuteRpcAsync<ConnectionSecureOrTune, ConnectionSecureOrTuneAsyncRpcContinuation>(
                () => new ConnectionSecureOrTuneAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new ConnectionStartOk(clientProperties, mechanism, response, locale);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<IChannel> OpenAsync(CreateChannelOptions createChannelOptions,
            CancellationToken cancellationToken)
        {
            ConfigurePublisherConfirmations(createChannelOptions.PublisherConfirmationsEnabled,
                createChannelOptions.PublisherConfirmationTrackingEnabled,
                createChannelOptions.OutstandingPublisherConfirmationsRateLimiter);

            await ExecuteBooleanRpcAsync<ChannelOpenAsyncRpcContinuation>(
                () => new ChannelOpenAsyncRpcContinuation(ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    var method = new ChannelOpen();
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            await MaybeConfirmSelect(cancellationToken).ConfigureAwait(false);

            return this;
        }

        public Task ConnectionTuneOkAsync(ushort channelMax, uint frameMax, ushort heartbeat, CancellationToken cancellationToken)
        {
            var method = new ConnectionTuneOk(channelMax, frameMax, heartbeat);
            return ModelSendAsync(in method, cancellationToken).AsTask();
        }

        public async Task UpdateSecretAsync(string newSecret, string reason,
            CancellationToken cancellationToken)
        {
            if (newSecret is null)
            {
                throw new ArgumentNullException(nameof(newSecret));
            }

            if (reason is null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            await ExecuteBooleanRpcAsync<SimpleAsyncRpcContinuation>(
                () => new SimpleAsyncRpcContinuation(ProtocolCommandId.ConnectionUpdateSecretOk, ContinuationTimeout, cancellationToken),
                async (k, token) => 
                {
                    byte[] newSecretBytes = Encoding.UTF8.GetBytes(newSecret);
                    var method = new ConnectionUpdateSecret(newSecretBytes, reason);
                    await ModelSendAsync(in method, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        // These methods would be implemented in the actual class
        private void ConfigurePublisherConfirmations(bool enabled, bool trackingEnabled, object? rateLimiter)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        private Task MaybeConfirmSelect(CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }
    }
}
