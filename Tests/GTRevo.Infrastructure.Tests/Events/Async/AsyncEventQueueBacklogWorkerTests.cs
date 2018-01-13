﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTRevo.Core.Core;
using GTRevo.Core.Events;
using GTRevo.Infrastructure.Events.Async;
using NSubstitute;
using Xunit;

namespace GTRevo.Infrastructure.Tests.Events.Async
{
    public class AsyncEventQueueBacklogWorkerTests
    {
        private AsyncEventQueueBacklogWorker sut;
        private IAsyncEventQueueManager asyncEventQueueManager;
        private IServiceLocator serviceLocator;
        private List<IAsyncEventQueueRecord> events = new List<IAsyncEventQueueRecord>();
        private FakeAsyncEventQueueState queueState = new FakeAsyncEventQueueState();
        private List<IAsyncEventListener> listeners = new List<IAsyncEventListener>();

        public AsyncEventQueueBacklogWorkerTests()
        {
            asyncEventQueueManager = Substitute.For<IAsyncEventQueueManager>();
            serviceLocator = Substitute.For<IServiceLocator>();
            sut = new AsyncEventQueueBacklogWorker(asyncEventQueueManager, serviceLocator);

            asyncEventQueueManager.GetQueueStateAsync("queue").Returns(ci => queueState);
            asyncEventQueueManager.GetQueueEventsAsync("queue").Returns(ci => events);
            serviceLocator.GetAll(typeof(IAsyncEventListener<Event1>)).Returns(ci => listeners);

            listeners.Add(Substitute.For<IAsyncEventListener<Event1>>());
            listeners.Add(Substitute.For<IAsyncEventListener<Event1>>());
            listeners[0].EventSequencer.Returns(Substitute.For<IAsyncEventSequencer<Event1>>());
            listeners[1].EventSequencer.Returns(Substitute.For<IAsyncEventSequencer<Event1>>());

            listeners[0].EventSequencer.GetEventSequencing(null).ReturnsForAnyArgs(new List<EventSequencing>()
            {
                new EventSequencing() {EventSequenceNumber = 0 /* shouldn't be important now */, SequenceName = "queue"}
            });

            listeners[1].EventSequencer.GetEventSequencing(null).ReturnsForAnyArgs(new List<EventSequencing>()
            {
                new EventSequencing() {EventSequenceNumber = 0 /* shouldn't be important now */, SequenceName = "queue"}
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public async Task RunQueueBacklogAsync_InvokesListenersDequeuesAndCommits(long? lastSequenceNumberProcessed)
        {
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = 1
            });
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = 2
            });

            queueState.LastSequenceNumberProcessed = lastSequenceNumberProcessed;

            await sut.RunQueueBacklogAsync("queue");

            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[0]).HandleAsync((IEventMessage<Event1>)events[0].EventMessage, "queue");
                ((IAsyncEventListener<Event1>)listeners[0]).HandleAsync((IEventMessage<Event1>)events[1].EventMessage, "queue");
                listeners[0].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[1]).HandleAsync((IEventMessage<Event1>)events[0].EventMessage, "queue");
                ((IAsyncEventListener<Event1>)listeners[1]).HandleAsync((IEventMessage<Event1>)events[1].EventMessage, "queue");
                listeners[1].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                asyncEventQueueManager.DequeueEventAsync(events[0].Id);
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                asyncEventQueueManager.DequeueEventAsync(events[1].Id);
                asyncEventQueueManager.CommitAsync();
            });

            ((IAsyncEventListener<Event1>) listeners[0]).ReceivedWithAnyArgs(2).HandleAsync(null, null);
            ((IAsyncEventListener<Event1>) listeners[0]).ReceivedWithAnyArgs(1).OnFinishedEventQueueAsync(null);
            ((IAsyncEventListener<Event1>) listeners[1]).ReceivedWithAnyArgs(2).HandleAsync(null, null);
            ((IAsyncEventListener<Event1>)listeners[1]).ReceivedWithAnyArgs(1).OnFinishedEventQueueAsync(null);
            asyncEventQueueManager.ReceivedWithAnyArgs(2).DequeueEventAsync(Guid.Empty);
            asyncEventQueueManager.Received(1).CommitAsync();
        }

        [Fact]
        public async Task RunQueueBacklogAsync_BothSeqAndNonseqEvents()
        {
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = 1
            });
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = null
            });

            queueState.LastSequenceNumberProcessed = 0;

            await sut.RunQueueBacklogAsync("queue");

            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[0]).HandleAsync((IEventMessage<Event1>)events[1].EventMessage, "queue");
                listeners[0].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
                ((IAsyncEventListener<Event1>)listeners[0]).HandleAsync((IEventMessage<Event1>)events[0].EventMessage, "queue");
                listeners[0].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[1]).HandleAsync((IEventMessage<Event1>)events[1].EventMessage, "queue");
                listeners[1].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
                ((IAsyncEventListener<Event1>)listeners[1]).HandleAsync((IEventMessage<Event1>)events[0].EventMessage, "queue");
                listeners[1].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                asyncEventQueueManager.DequeueEventAsync(events[1].Id);
                asyncEventQueueManager.CommitAsync();
                asyncEventQueueManager.DequeueEventAsync(events[0].Id);
                asyncEventQueueManager.CommitAsync();
            });
            
            ((IAsyncEventListener<Event1>) listeners[0]).ReceivedWithAnyArgs(2).HandleAsync(null, null);
            ((IAsyncEventListener<Event1>) listeners[0]).ReceivedWithAnyArgs(2).OnFinishedEventQueueAsync(null);
            ((IAsyncEventListener<Event1>) listeners[1]).ReceivedWithAnyArgs(2).HandleAsync(null, null);
            ((IAsyncEventListener<Event1>)listeners[1]).ReceivedWithAnyArgs(2).OnFinishedEventQueueAsync(null);
            asyncEventQueueManager.ReceivedWithAnyArgs(2).DequeueEventAsync(Guid.Empty);
            asyncEventQueueManager.Received(2).CommitAsync();
        }

        [Theory]
        [InlineData(0, 1, 3)]
        [InlineData(0, 2, 3)]
        public async Task RunQueueBacklogAsync_SequenceMissingEvents(long? lastSequenceNumberProcessed, long? event1SequenceNumber,
            long? event2SequenceNumber)
        {
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = event1SequenceNumber
            });
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = event2SequenceNumber
            });

            queueState.LastSequenceNumberProcessed = lastSequenceNumberProcessed;

            await Assert.ThrowsAsync<AsyncEventProcessingSequenceException>(() => sut.RunQueueBacklogAsync("queue"));

            ((IAsyncEventListener<Event1>) listeners[0]).DidNotReceiveWithAnyArgs().HandleAsync(null, null); 
            ((IAsyncEventListener<Event1>) listeners[1]).DidNotReceiveWithAnyArgs().HandleAsync(null, null);
            asyncEventQueueManager.DidNotReceiveWithAnyArgs().DequeueEventAsync(Guid.Empty);
            asyncEventQueueManager.DidNotReceive().CommitAsync();
        }

        [Theory]
        [InlineData(0, 2, null, 3, 1)]
        [InlineData(0, 1, null, 3, 1)]
        public async Task RunQueueBacklogAsync_SequenceMissingEventsProcessesNonsequentialEvents(long? lastSequenceNumberProcessed,
            long? event1SequenceNumber, long? event2SequenceNumber, long? event3SequenceNumber,
            int nonseqEventIndex)
        {
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = event1SequenceNumber
            });
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = event2SequenceNumber
            });
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = event3SequenceNumber
            });

            queueState.LastSequenceNumberProcessed = lastSequenceNumberProcessed;

            await Assert.ThrowsAsync<AsyncEventProcessingSequenceException>(() => sut.RunQueueBacklogAsync("queue"));
            
            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[0]).HandleAsync((IEventMessage<Event1>)events[nonseqEventIndex].EventMessage, "queue");
                listeners[0].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                ((IAsyncEventListener<Event1>)listeners[1]).HandleAsync((IEventMessage<Event1>)events[nonseqEventIndex].EventMessage, "queue");
                listeners[1].OnFinishedEventQueueAsync("queue");
                asyncEventQueueManager.CommitAsync();
            });

            Received.InOrder(() =>
            {
                asyncEventQueueManager.DequeueEventAsync(events[nonseqEventIndex].Id);
                asyncEventQueueManager.CommitAsync();
            });

            ((IAsyncEventListener<Event1>) listeners[0]).ReceivedWithAnyArgs(1).HandleAsync(null, null);
            ((IAsyncEventListener<Event1>) listeners[1]).ReceivedWithAnyArgs(1).HandleAsync(null, null);
            asyncEventQueueManager.ReceivedWithAnyArgs(1).DequeueEventAsync(Guid.Empty);
            asyncEventQueueManager.Received(1).CommitAsync();
        }

        [Fact]
        public async Task RunQueueBacklogAsync_FinishingEventQueueFailed()
        {
            events.Add(new FakeAsyncEventQueueRecord() { EventId = Guid.NewGuid(),
                EventMessage = new EventMessage<Event1>(new Event1(), new Dictionary<string, string>()),
                Id = Guid.NewGuid(), QueueName = "queue", SequenceNumber = 2
            });

            listeners[1].When(x => x.OnFinishedEventQueueAsync("queue")).Throw<Exception>();
            
            await Assert.ThrowsAsync<AsyncEventProcessingException>(() => sut.RunQueueBacklogAsync("queue"));
            
            asyncEventQueueManager.DidNotReceiveWithAnyArgs().DequeueEventAsync(Guid.Empty);
            asyncEventQueueManager.DidNotReceive().CommitAsync();
        }

        public class Event1 : IEvent
        {
        }
    }
}
