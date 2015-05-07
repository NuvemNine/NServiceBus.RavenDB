using System;
using NServiceBus.RavenDB.Tests;
using NServiceBus.Saga;
using NServiceBus.SagaPersisters.RavenDB;
using NUnit.Framework;

[TestFixture]
public class When_updating_a_saga_without_unique_properties : RavenDBPersistenceTestBase
{
    [Test]
    public void It_should_persist_successfully()
    {
        var factory = new RavenSessionFactory(store);
        factory.ReleaseSession();
        var persister = new SagaPersister(factory);
        var uniqueString = Guid.NewGuid().ToString();
        var anotherUniqueString = Guid.NewGuid().ToString();

        var saga1 = new SagaData
        {
            Id = Guid.NewGuid(),
            UniqueString = uniqueString,
            NonUniqueString = "notUnique"
        };
        persister.Save(saga1);
        factory.SaveChanges();

        var saga = persister.Get<SagaData>(saga1.Id);
        saga.NonUniqueString = "notUnique2";
        saga.UniqueString = anotherUniqueString;
        persister.Update(saga);
        factory.SaveChanges();
    }

    class SagaData : IContainSagaData
    {
        public Guid Id { get; set; }
        public string Originator { get; set; }
        public string OriginalMessageId { get; set; }
// ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string UniqueString { get; set; }
// ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string NonUniqueString { get; set; }
    }
}