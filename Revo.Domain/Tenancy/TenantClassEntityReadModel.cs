﻿using System;
using Revo.Domain.ReadModel;

namespace Revo.Domain.Tenancy
{
    public abstract class TenantClassEntityReadModel : ClassEntityReadModel, ITenantOwned, ITenantReadModel
    {
        public Guid? TenantId { get; set; }
    }
}
