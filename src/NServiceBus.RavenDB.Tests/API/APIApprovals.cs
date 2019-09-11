﻿namespace NServiceBus.RavenDB.Tests.API
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void ApproveRavenDBPersistence()
        {

            var publicApi = ApiGenerator.GeneratePublicApi(typeof(RavenDBClusterWidePersistence).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" });
            Approver.Verify(publicApi);
        }
    }
}