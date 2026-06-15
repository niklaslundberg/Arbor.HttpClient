using Arbor.HttpClient.Core.Messaging;

namespace Arbor.HttpClient.Core.Tests.Messaging;

public class MessageBusTests
{
    private sealed record SampleMessage(int Value);

    private sealed record OtherMessage(string Text);

    [Fact]
    public void Publish_WithActiveSubscriber_DeliversMessage()
    {
        var bus = new MessageBus();
        var received = new List<SampleMessage>();
        using var subscription = bus.Listen<SampleMessage>().Subscribe(received.Add);

        bus.Publish(new SampleMessage(42));

        received.Should().ContainSingle().Which.Value.Should().Be(42);
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var bus = new MessageBus();

        var publish = () => bus.Publish(new SampleMessage(1));

        publish.Should().NotThrow();
    }

    [Fact]
    public void Publish_DeliversToAllSubscribersOfSameType()
    {
        var bus = new MessageBus();
        var first = new List<SampleMessage>();
        var second = new List<SampleMessage>();
        using var s1 = bus.Listen<SampleMessage>().Subscribe(first.Add);
        using var s2 = bus.Listen<SampleMessage>().Subscribe(second.Add);

        bus.Publish(new SampleMessage(7));

        first.Should().ContainSingle().Which.Value.Should().Be(7);
        second.Should().ContainSingle().Which.Value.Should().Be(7);
    }

    [Fact]
    public void Publish_OnlyDeliversToSubscribersOfMatchingType()
    {
        var bus = new MessageBus();
        var samples = new List<SampleMessage>();
        var others = new List<OtherMessage>();
        using var s1 = bus.Listen<SampleMessage>().Subscribe(samples.Add);
        using var s2 = bus.Listen<OtherMessage>().Subscribe(others.Add);

        bus.Publish(new SampleMessage(3));

        samples.Should().ContainSingle();
        others.Should().BeEmpty();
    }

    [Fact]
    public void Publish_AfterSubscriptionDisposed_DoesNotDeliver()
    {
        var bus = new MessageBus();
        var received = new List<SampleMessage>();
        var subscription = bus.Listen<SampleMessage>().Subscribe(received.Add);

        subscription.Dispose();
        bus.Publish(new SampleMessage(99));

        received.Should().BeEmpty();
    }

    [Fact]
    public void Listen_DoesNotReplayMessagesPublishedBeforeSubscription()
    {
        var bus = new MessageBus();

        bus.Publish(new SampleMessage(1));
        var received = new List<SampleMessage>();
        using var subscription = bus.Listen<SampleMessage>().Subscribe(received.Add);

        received.Should().BeEmpty();
    }

    [Fact]
    public void Publish_DeliversMultipleMessagesInOrder()
    {
        var bus = new MessageBus();
        var received = new List<int>();
        using var subscription = bus.Listen<SampleMessage>().Subscribe(m => received.Add(m.Value));

        bus.Publish(new SampleMessage(1));
        bus.Publish(new SampleMessage(2));
        bus.Publish(new SampleMessage(3));

        received.Should().Equal(1, 2, 3);
    }
}
