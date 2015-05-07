using System;
using NServiceBus.RavenDB.Tests;
using NServiceBus.Saga;
using NServiceBus.SagaPersisters.RavenDB;
using NUnit.Framework;

[TestFixture]
public class When_persisting_a_saga_entity_with_an_Enum_property : RavenDBPersistenceTestBase
{
    [Test]
    public void Enums_should_be_persisted()
    {
        var entity = new SagaData
            {
                Id = Guid.NewGuid(),
                Status = StatusEnum.AnotherStatus
            };

        var factory = new RavenSessionFactory(store);
        factory.ReleaseSession();
        var persister = new SagaPersister(factory);
        persister.Save(entity);
        factory.SaveChanges();

        var savedEntity = persister.Get<SagaData>(entity.Id);
        Assert.AreEqual(entity.Status, savedEntity.Status);
    }

    class SagaData : IContainSagaData
    {
        public Guid Id { get; set; }
        public string Originator { get; set; }
        public string OriginalMessageId { get; set; }
        public StatusEnum Status { get; set; }
    }

    enum StatusEnum
    {
        SomeStatus,
        AnotherStatus
    }

}