using System.Linq;
using NServiceBus.RavenDB.Tests;
using NServiceBus.Unicast.Subscriptions.RavenDB;
using NUnit.Framework;

[TestFixture]
public class When_receiving_an_unsubscribe_message : RavenDBPersistenceTestBase
{
    [Test]
    public void All_subscription_entries_for_specified_message_types_should_be_removed()
    {
        var storage = new SubscriptionPersister
        {
            DocumentStore = store
        };

        storage.Subscribe(TestClients.ClientA, MessageTypes.All);

        storage.Unsubscribe(TestClients.ClientA, MessageTypes.All);

        var clients = storage.GetSubscriberAddressesForMessage(MessageTypes.All);
        Assert.IsFalse(clients.Any(a => a == TestClients.ClientA));
    }
}