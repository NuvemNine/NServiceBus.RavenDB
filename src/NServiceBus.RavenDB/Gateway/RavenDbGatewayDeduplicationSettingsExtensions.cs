﻿namespace NServiceBus
{
    using System;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Persistence.RavenDB;
    using NServiceBus.Settings;
    using Raven.Client.Documents;

    /// <summary>
    ///     Configuration settings specific to the timeout storage
    /// </summary>
    public static class RavenDbGatewayDeduplicationSettingsExtensions
    {
        /// <summary>
        ///     Configures the given document store to be used when storing gateway deduplication data
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="documentStore">The document store to use</param>
        public static PersistenceExtensions<RavenDBClusterWidePersistence> UseDocumentStoreForGatewayDeduplication(this PersistenceExtensions<RavenDBClusterWidePersistence> cfg, IDocumentStore documentStore)
        {
            DocumentStoreManager.SetDocumentStore<StorageType.GatewayDeduplication>(cfg.GetSettings(), documentStore);
            return cfg;
        }

        /// <summary>
        ///     Configures the given document store to be used when storing gateway deduplication data
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="storeCreator">A Func that will create the document store on NServiceBus initialization.</param>
        public static PersistenceExtensions<RavenDBClusterWidePersistence> UseDocumentStoreForGatewayDeduplication(this PersistenceExtensions<RavenDBClusterWidePersistence> cfg, Func<ReadOnlySettings, IDocumentStore> storeCreator)
        {
            DocumentStoreManager.SetDocumentStore<StorageType.GatewayDeduplication>(cfg.GetSettings(), storeCreator);
            return cfg;
        }
    }
}