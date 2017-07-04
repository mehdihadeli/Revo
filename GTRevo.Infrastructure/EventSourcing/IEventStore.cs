﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GTRevo.Infrastructure.Domain.Events;
using GTRevo.Infrastructure.Domain.EventSourcing;

namespace GTRevo.Infrastructure.EventSourcing
{
    public interface IEventStore
    {
        void AddAggregate(Guid aggregateId, Guid aggregateClassId);

        Task<IAggregateHistory> GetAggregateHistory(Guid aggregateId);
        Task<AggregateState> GetLastStateAsync(Guid aggregateId);
        Task<AggregateState> GetStateByVersionAsync(Guid aggregateId, int version);

        void PushEvents(Guid aggregateId, IEnumerable<DomainAggregateEventRecord> events, int version);
        Task PushEventsAsync(Guid aggregateId, IEnumerable<DomainAggregateEventRecord> events, int version);

        void CommitChanges();
        Task CommitChangesAsync();
    }
}
