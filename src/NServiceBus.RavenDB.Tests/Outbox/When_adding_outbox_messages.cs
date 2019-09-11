namespace NServiceBus.RavenDB.Tests.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Outbox;
    using NServiceBus.Persistence.RavenDB;
    using NServiceBus.RavenDB.Outbox;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using Raven.Client.Exceptions;

    [TestFixture]
    public class When_adding_outbox_messages : RavenDBPersistenceTestBase
    {
        string testEndpointName = "TestEndpoint";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            new OutboxRecordsIndex().Execute(store);
        }

        [Test]
        public async Task Should_throw_if__trying_to_insert_same_messageid_concurrently()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var exception = await Catch<ArgumentException>(async () =>
            {
                using (var transaction = await persister.BeginTransaction(new ContextBag()))
                {
                    await persister.Store(new OutboxMessage("MySpecialId", new TransportOperation[0]), transaction, new ContextBag());
                    await persister.Store(new OutboxMessage("MySpecialId", new TransportOperation[0]), transaction, new ContextBag());
                    await transaction.Commit();
                }
            });

            Assert.NotNull(exception);
        }

        [Test]
        public async Task Should_throw_if__trying_to_insert_same_messageid()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            using (var transaction = await persister.BeginTransaction(new ContextBag()))
            {
                await persister.Store(new OutboxMessage("MySpecialId", new TransportOperation[0]), transaction, new ContextBag());

                await transaction.Commit();
            }

            var exception = await Catch<ConcurrencyException>(async () =>
            {
                using (var transaction = await persister.BeginTransaction(new ContextBag()))
                {
                    await persister.Store(new OutboxMessage("MySpecialId", new TransportOperation[0]), transaction, new ContextBag());

                    await transaction.Commit();
                }
            });
            Assert.NotNull(exception);
        }

        [Test]
        public async Task Should_save_with_not_dispatched()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var id = Guid.NewGuid().ToString("N");
            var message = new OutboxMessage(id, new[]
            {
                new TransportOperation(id, new Dictionary<string, string>(), new byte[1024*5], new Dictionary<string, string>())
            });

            using (var transaction = await persister.BeginTransaction(new ContextBag()))
            {
                await persister.Store(message, transaction, new ContextBag());

                await transaction.Commit();
            }

            var result = await persister.Get(id, new ContextBag());

            var operation = result.TransportOperations.Single();

            Assert.AreEqual(id, operation.MessageId);
        }

        [Test]
        public async Task Should_update_dispatched_flag()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var messageId = Guid.NewGuid().ToString("N");
            var message = new OutboxMessage(messageId, new[]
            {
                new TransportOperation(messageId, new Dictionary<string, string>(), new byte[1024*5], new Dictionary<string, string>())
            });

            using (var transaction = await persister.BeginTransaction(new ContextBag()))
            {
                await persister.Store(message, transaction, new ContextBag());

                await transaction.Commit();
            }
            await persister.SetAsDispatched(messageId, new ContextBag());

            var outboxRecordId = $"Outbox/TestEndpoint/{messageId}";
            using (var s = store.OpenAsyncSession(new SessionOptions { TransactionMode = TransactionMode.ClusterWide }))
            {
                var result = await s.Advanced.ClusterTransaction.GetCompareExchangeValueAsync<OutboxRecord>(outboxRecordId);

                Assert.NotNull(result?.Value);
                Assert.True(result?.Value.Dispatched);
            }
        }

        [Test]
        public async Task Should_get_messages()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var messageId = Guid.NewGuid().ToString();

            //manually store an OutboxRecord to control the OutboxRecordId format
            using (var session = OpenAsyncSession())
            {
                var newRecord = new OutboxRecord
                {
                    MessageId = messageId,
                    Dispatched = false,
                    TransportOperations = new[]
                    {
                        new OutboxRecord.OutboxOperation
                        {
                            Message = new byte[1024*5],
                            Headers = new Dictionary<string, string>(),
                            MessageId = messageId,
                            Options = new Dictionary<string, string>()
                        }
                    }
                };
                var fullDocumentId = $"Outbox/TestEndpoint/{messageId}";
                session.Advanced.ClusterTransaction.CreateCompareExchangeValue(fullDocumentId, newRecord);

                await session.SaveChangesAsync();
            }

            var result = await persister.Get(messageId, new ContextBag());

            Assert.NotNull(result);
            Assert.AreEqual(messageId, result.MessageId);
        }

        [Test]
        public async Task Should_set_messages_as_dispatched()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var messageId = Guid.NewGuid().ToString();

            //manually store an OutboxRecord to control the OutboxRecordId format
            var outboxId = $"Outbox/TestEndpoint/{messageId}";

            using (var session = OpenAsyncSession())
            {
                session.Advanced.ClusterTransaction.CreateCompareExchangeValue(outboxId, new OutboxRecord
                {
                    MessageId = messageId,
                    Dispatched = false,
                    TransportOperations = new[]
                    {
                        new OutboxRecord.OutboxOperation
                        {
                            Message = new byte[1024*5],
                            Headers = new Dictionary<string, string>(),
                            MessageId = messageId,
                            Options = new Dictionary<string, string>()
                        }
                    }
                });

                await session.SaveChangesAsync();
            }

            await persister.SetAsDispatched(messageId, new ContextBag());

            using (var session = OpenAsyncSession())
            {
                var result = await session.Advanced.ClusterTransaction.GetCompareExchangeValueAsync<OutboxRecord>(outboxId);

                Assert.NotNull(result?.Value);
                Assert.True(result.Value.Dispatched);
            }
        }

        [Test]
        public async Task Should_filter_invalid_docid_character()
        {
            var persister = new OutboxPersister(store, testEndpointName, CreateTestSessionOpener());

            var guid = Guid.NewGuid();
            var messageId = $@"{guid}\12345";
            var emptyDictionary = new Dictionary<string, string>();
            var operation = new TransportOperation("test", emptyDictionary, new byte[0], emptyDictionary);
            var transportOperations = new[] { operation };

            using (var transaction = await persister.BeginTransaction(new ContextBag()))
            {
                await persister.Store(new OutboxMessage(messageId, transportOperations), transaction, new ContextBag());
                await transaction.Commit();
            }

            var outboxMessage = await persister.Get(messageId, new ContextBag());

            Assert.AreEqual(messageId, outboxMessage.MessageId);
            Assert.AreEqual(1, outboxMessage.TransportOperations.Length);
            Assert.AreEqual("test", outboxMessage.TransportOperations[0].MessageId);
        }

    }
}