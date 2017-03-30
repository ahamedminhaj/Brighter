﻿#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class ControlBusSenderPostMessageTests : IDisposable
    {
        private CommandProcessor _commandProcessor;
        private ControlBusSender _controlBusSender;
        private readonly MyCommand _myCommand = new MyCommand();
        private Message _message;
        private FakeMessageStore _fakeMessageStore;
        private FakeMessageProducer _fakeMessageProducer;

        public ControlBusSenderPostMessageTests()
        {
            _myCommand.Value = "Hello World";

            _fakeMessageStore = new FakeMessageStore();
            _fakeMessageProducer = new FakeMessageProducer();

            _message = new Message(
                header: new MessageHeader(messageId: _myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(_myCommand))
                );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                (IAmAMessageStore<Message>)_fakeMessageStore,
                (IAmAMessageProducer)_fakeMessageProducer);

            _controlBusSender = new ControlBusSender(_commandProcessor);
        }

        [Fact]
        public void When_Posting_Via_A_Control_Bus_Sender()
        {
            _controlBusSender.Post(_myCommand);

            //_should_store_the_message_in_the_sent_command_message_repository
            _fakeMessageStore.MessageWasAdded.Should().BeTrue();
            //_should_send_a_message_via_the_messaging_gateway
            _fakeMessageProducer.MessageWasSent.Should().BeTrue();
            //_should_convert_the_command_into_a_message
            _fakeMessageStore.Get().First().Should().Be(_message);
        }

        public void Dispose()
        {
            _controlBusSender.Dispose();
        }
    }
}
