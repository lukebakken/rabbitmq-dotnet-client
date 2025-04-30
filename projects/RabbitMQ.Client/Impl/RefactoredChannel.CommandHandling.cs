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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;

namespace RabbitMQ.Client.Impl
{
    internal partial class RefactoredChannel
    {
        private async Task HandleCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            /*
             * If DispatchCommandAsync returns `true`, it means that the incoming command is server-originated, and has
             * already been handled.
             *
             * Else, the incoming command is the return of an RPC call, and must be handled.
             */
            try
            {
                if (_continuationQueue.ShouldIgnoreCommand(cmd.CommandId))
                {
                    return;
                }

                if (false == await DispatchCommandAsync(cmd, cancellationToken).ConfigureAwait(false))
                {
                    using (IRpcContinuation c = _continuationQueue.Next())
                    {
                        await c.HandleCommandAsync(cmd).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                cmd.ReturnBuffers();
            }
        }

        /// <summary>
        /// Returning <c>true</c> from this method means that the command was server-originated,
        /// and handled already.
        /// Returning <c>false</c> (the default) means that the incoming command is the response to
        /// a client-initiated RPC call, and must be handled.
        /// </summary>
        /// <param name="cmd">The incoming command from the AMQP server</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        private Task<bool> DispatchCommandAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            switch (cmd.CommandId)
            {
                case ProtocolCommandId.BasicCancel:
                    {
                        // Note: always returns true
                        return HandleBasicCancelAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicDeliver:
                    {
                        // Note: always returns true
                        return HandleBasicDeliverAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicAck:
                    {
                        return HandleBasicAck(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicNack:
                    {
                        return HandleBasicNack(cmd, cancellationToken);
                    }
                case ProtocolCommandId.BasicReturn:
                    {
                        // Note: always returns true
                        return HandleBasicReturn(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelClose:
                    {
                        // Note: always returns true
                        return HandleChannelCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelCloseOk:
                    {
                        // Note: always returns true
                        return HandleChannelCloseOkAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ChannelFlow:
                    {
                        // Note: always returns true
                        return HandleChannelFlowAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionBlocked:
                    {
                        // Note: always returns true
                        return HandleConnectionBlockedAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionClose:
                    {
                        // Note: always returns true
                        return HandleConnectionCloseAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionSecure:
                    {
                        // Note: always returns true
                        return HandleConnectionSecureAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionStart:
                    {
                        // Note: always returns true
                        return HandleConnectionStartAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionTune:
                    {
                        // Note: always returns true
                        return HandleConnectionTuneAsync(cmd, cancellationToken);
                    }
                case ProtocolCommandId.ConnectionUnblocked:
                    {
                        // Note: always returns true
                        return HandleConnectionUnblockedAsync(cancellationToken);
                    }
                default:
                    {
                        return Task.FromResult(false);
                    }
            }
        }

        // Command handler methods would be implemented here
        // These are placeholders for the actual implementations

        protected Task<bool> HandleBasicCancelAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleBasicDeliverAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleBasicAck(IncomingCommand cmd, CancellationToken cancellationToken = default)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleBasicNack(IncomingCommand cmd, CancellationToken cancellationToken = default)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleBasicReturn(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleChannelCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleChannelCloseOkAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleChannelFlowAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionBlockedAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionCloseAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionSecureAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionStartAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionTuneAsync(IncomingCommand cmd, CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        protected Task<bool> HandleConnectionUnblockedAsync(CancellationToken cancellationToken)
        {
            // Implementation would be added here
            throw new NotImplementedException();
        }

        internal Task OnCallbackExceptionAsync(CallbackExceptionEventArgs args)
        {
            return _callbackExceptionAsyncWrapper.InvokeAsync(this, args);
        }

        ///<summary>Broadcasts notification of the final shutdown of the channel.</summary>
        ///<remarks>
        ///<para>
        ///Do not call anywhere other than at the end of OnSessionShutdownAsync.
        ///</para>
        ///<para>
        ///Must not be called when m_closeReason is null, because
        ///otherwise there's a window when a new continuation could be
        ///being enqueued at the same time as we're broadcasting the
        ///shutdown event. See the definition of Enqueue() above.
        ///</para>
        ///</remarks>
        private async Task OnChannelShutdownAsync(ShutdownEventArgs reason)
        {
            _continuationQueue.HandleChannelShutdown(reason);

            await _channelShutdownAsyncWrapper.InvokeAsync(this, reason).ConfigureAwait(false);
        }

        private async Task OnSessionShutdownAsync(object? sender, ShutdownEventArgs reason)
        {
            ConsumerDispatcher.Quiesce();
            SetCloseReason(reason);
            await OnChannelShutdownAsync(reason).ConfigureAwait(false);
            await ConsumerDispatcher.ShutdownAsync(reason).ConfigureAwait(false);
        }
    }
}
