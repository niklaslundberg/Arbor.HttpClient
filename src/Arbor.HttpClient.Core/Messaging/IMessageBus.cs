namespace Arbor.HttpClient.Core.Messaging;

/// <summary>
/// A minimal, UI-framework-agnostic publish/subscribe bus for cross-feature notifications.
/// </summary>
/// <remarks>
/// <para>
/// Messages are strongly typed: a subscriber to <typeparamref name="TMessage" /> receives every
/// message published with that exact type. This keeps every publisher and subscriber discoverable
/// through "find all references" rather than hidden behind string keys or a service locator.
/// </para>
/// <para>
/// The bus is intentionally scheduler-agnostic. <see cref="Publish{TMessage}" /> may be called from
/// any thread, and observers are notified synchronously on the publishing thread. UI consumers are
/// responsible for marshalling to their own scheduler (for example
/// <c>.ObserveOn(RxApp.MainThreadScheduler)</c>) at the subscription site.
/// </para>
/// <para>
/// The bus carries fire-and-forget notifications only; it never returns a result. Anything that
/// needs a reply is a command (a direct call or a mediator request), not a published message.
/// </para>
/// </remarks>
public interface IMessageBus
{
    /// <summary>Publishes a message to all current subscribers of <typeparamref name="TMessage" />.</summary>
    void Publish<TMessage>(TMessage message);

    /// <summary>Returns an observable stream of all messages published with type <typeparamref name="TMessage" />.</summary>
    IObservable<TMessage> Listen<TMessage>();
}
