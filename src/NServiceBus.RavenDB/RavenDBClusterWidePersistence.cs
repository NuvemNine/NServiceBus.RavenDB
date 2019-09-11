﻿namespace NServiceBus
{
    using NServiceBus.Features;
    using NServiceBus.Persistence;
    using NServiceBus.Persistence.RavenDB;

    /// <summary>
    ///     Specifies the capabilities of the ravendb suite of storages
    /// </summary>
    public class RavenDBClusterWidePersistence : PersistenceDefinition
    {

        /// <summary>
        ///     Defines the capabilities
        /// </summary>
        public RavenDBClusterWidePersistence()
        {
            Defaults(s =>
            {
                s.EnableFeatureByDefault<RavenDbStorageSession>();
            });

            Supports<StorageType.GatewayDeduplication>(s => s.EnableFeatureByDefault<RavenDbGatewayDeduplication>());
            Supports<StorageType.Timeouts>(s => s.EnableFeatureByDefault<RavenDbTimeoutStorage>());
            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<RavenDbSagaStorage>());
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<RavenDbSubscriptionStorage>());
            Supports<StorageType.Outbox>(s => s.EnableFeatureByDefault<RavenDbOutboxStorage>());
        }
    }
}