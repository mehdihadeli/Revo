﻿using GTRevo.DataAccess.EF6.Entities;
using GTRevo.Platform.Core;
using GTRevo.Transactions;
using Ninject.Modules;

namespace GTRevo.Infrastructure.Repositories
{
    public class DomainRepositoryModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IRepository, IUnitOfWorkProvider>()
                .To<Repository>()
                .InRequestOrJobScope();

            Bind<IAggregateStore>()
                .To<EventSourcedAggregateStore>()
                .InTransientScope();
        }
    }
}