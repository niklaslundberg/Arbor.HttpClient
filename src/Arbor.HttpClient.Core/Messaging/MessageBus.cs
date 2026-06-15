using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace Arbor.HttpClient.Core.Messaging;

/// <summary>
/// Default <see cref="IMessageBus" /> implementation backed by a per-message-type
/// <see cref="Subject{T}" /> registry.
/// </summary>
/// <remarks>
/// A single instance is expected to live for the lifetime of the application and be shared by all
/// features. The subject for each message type is created lazily on first publish or subscribe.
/// Subjects are wrapped with <see cref="Subject.Synchronize{T}(ISubject{T})" /> so the bus is safe
/// to publish to from multiple threads; consumers still marshal to their own scheduler.
/// </remarks>
public sealed class MessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new();

    /// <inheritdoc />
    public void Publish<TMessage>(TMessage message) => SubjectFor<TMessage>().OnNext(message);

    /// <inheritdoc />
    public IObservable<TMessage> Listen<TMessage>() => SubjectFor<TMessage>();

    private ISubject<TMessage> SubjectFor<TMessage>() =>
        (ISubject<TMessage>)_subjects.GetOrAdd(
            typeof(TMessage),
            static _ => Subject.Synchronize<TMessage>(new Subject<TMessage>()));
}
