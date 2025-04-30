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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.ConsumerDispatching;
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
        ///<summary>Only used to kick-start a connection open
        ///sequence. See <see cref="Connection.OpenAsync"/> </summary>
        internal TaskCompletionSource<ConnectionStartDetails?>? m_connectionStartCell;
        private Exception? m_connectionStartException;

        // AMQP only allows one RPC operation to be active at a time.
        protected readonly SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(1, 1);
        private readonly RpcContinuationQueue _continuationQueue = new RpcContinuationQueue();

        private ShutdownEventArgs? _closeReason;
        public ShutdownEventArgs? CloseReason => Volatile.Read(ref _closeReason);

        private TaskCompletionSource<bool>? _serverOriginatedChannelCloseTcs;

        internal readonly IConsumerDispatcher ConsumerDispatcher;

        private bool _disposed;
        private int _isDisposing;

        public RefactoredChannel(ISession session, CreateChannelOptions createChannelOptions)
        {
            ContinuationTimeout = createChannelOptions.ContinuationTimeout;
            ConsumerDispatcher = new AsyncConsumerDispatcher(this, createChannelOptions.InternalConsumerDispatchConcurrency);
            Func<Exception, string, CancellationToken, Task> onExceptionAsync = (exception, context, cancellationToken) =>
                OnCallbackExceptionAsync(CallbackExceptionEventArgs.Build(exception, context, cancellationToken));
            _basicAcksAsyncWrapper = new AsyncEventingWrapper<BasicAckEventArgs>("OnBasicAck", onExceptionAsync);
            _basicNacksAsyncWrapper = new AsyncEventingWrapper<BasicNackEventArgs>("OnBasicNack", onExceptionAsync);
            _basicReturnAsyncWrapper = new AsyncEventingWrapper<BasicReturnEventArgs>("OnBasicReturn", onExceptionAsync);
            _callbackExceptionAsyncWrapper =
                new AsyncEventingWrapper<CallbackExceptionEventArgs>(string.Empty, (exception, context, cancellationToken) => Task.CompletedTask);
            _flowControlAsyncWrapper = new AsyncEventingWrapper<FlowControlEventArgs>("OnFlowControl", onExceptionAsync);
            _channelShutdownAsyncWrapper = new AsyncEventingWrapper<ShutdownEventArgs>("OnChannelShutdownAsync", onExceptionAsync);
            _recoveryAsyncWrapper = new AsyncEventingWrapper<AsyncEventArgs>("OnChannelRecovery", onExceptionAsync);
            session.CommandReceived = HandleCommandAsync;
            session.SessionShutdownAsync += OnSessionShutdownAsync;
            Session = session;
        }

        internal TimeSpan HandshakeContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan ContinuationTimeout { get; set; }

        public event AsyncEventHandler<BasicAckEventArgs> BasicAcksAsync
        {
            add => _basicAcksAsyncWrapper.AddHandler(value);
            remove => _basicAcksAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicAckEventArgs> _basicAcksAsyncWrapper;

        public event AsyncEventHandler<BasicNackEventArgs> BasicNacksAsync
        {
            add => _basicNacksAsyncWrapper.AddHandler(value);
            remove => _basicNacksAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicNackEventArgs> _basicNacksAsyncWrapper;

        public event AsyncEventHandler<BasicReturnEventArgs> BasicReturnAsync
        {
            add => _basicReturnAsyncWrapper.AddHandler(value);
            remove => _basicReturnAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<BasicReturnEventArgs> _basicReturnAsyncWrapper;

        public event AsyncEventHandler<CallbackExceptionEventArgs> CallbackExceptionAsync
        {
            add => _callbackExceptionAsyncWrapper.AddHandler(value);
            remove => _callbackExceptionAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<CallbackExceptionEventArgs> _callbackExceptionAsyncWrapper;

        public event AsyncEventHandler<FlowControlEventArgs> FlowControlAsync
        {
            add => _flowControlAsyncWrapper.AddHandler(value);
            remove => _flowControlAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<FlowControlEventArgs> _flowControlAsyncWrapper;

        public event AsyncEventHandler<ShutdownEventArgs> ChannelShutdownAsync
        {
            add
            {
                if (IsOpen)
                {
                    _channelShutdownAsyncWrapper.AddHandler(value);
                }
                else
                {
                    value(this, CloseReason);
                }
            }
            remove => _channelShutdownAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<ShutdownEventArgs> _channelShutdownAsyncWrapper;

        public event AsyncEventHandler<AsyncEventArgs> RecoveryAsync
        {
            add => _recoveryAsyncWrapper.AddHandler(value);
            remove => _recoveryAsyncWrapper.RemoveHandler(value);
        }

        private AsyncEventingWrapper<AsyncEventArgs> _recoveryAsyncWrapper;

        internal Task RunRecoveryEventHandlers(object sender, CancellationToken cancellationToken)
        {
            return _recoveryAsyncWrapper.InvokeAsync(sender, AsyncEventArgs.CreateOrDefault(cancellationToken));
        }

        public int ChannelNumber => ((Session)Session).ChannelNumber;

        public IAsyncBasicConsumer? DefaultConsumer
        {
            get => ConsumerDispatcher.DefaultConsumer;
            set => ConsumerDispatcher.DefaultConsumer = value;
        }

        public bool IsClosed => !IsOpen;

        [MemberNotNullWhen(false, nameof(CloseReason))]
        public bool IsOpen => CloseReason is null;

        public string? CurrentQueue { get; private set; }

        public ISession Session { get; private set; }

        public Exception? ConnectionStartException => m_connectionStartException;

        public void MaybeSetConnectionStartException(Exception ex)
        {
            if (m_connectionStartCell != null)
            {
                m_connectionStartException = ex;
            }
        }

        protected void TakeOver(RefactoredChannel other)
        {
            _basicAcksAsyncWrapper.Takeover(other._basicAcksAsyncWrapper);
            _basicNacksAsyncWrapper.Takeover(other._basicNacksAsyncWrapper);
            _basicReturnAsyncWrapper.Takeover(other._basicReturnAsyncWrapper);
            _callbackExceptionAsyncWrapper.Takeover(other._callbackExceptionAsyncWrapper);
            _flowControlAsyncWrapper.Takeover(other._flowControlAsyncWrapper);
            _channelShutdownAsyncWrapper.Takeover(other._channelShutdownAsyncWrapper);
            _recoveryAsyncWrapper.Takeover(other._recoveryAsyncWrapper);
        }

        // Implementation of IChannel interface methods will be added in separate files
        // to keep the code organized
    }
}
