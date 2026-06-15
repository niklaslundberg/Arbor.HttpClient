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
public sealed class MessageBus : IMessageBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, ISubjectEntry> _subjects = new();
    private bool _disposed;

    /// <inheritdoc />
    public void Publish<TMessage>(TMessage message) => EntryFor<TMessage>().Synchronized.OnNext(message);

    /// <inheritdoc />
    public IObservable<TMessage> Listen<TMessage>() => EntryFor<TMessage>().Synchronized;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var entry in _subjects.Values)
        {
            entry.Dispose();
        }

        _subjects.Clear();
    }

    private SubjectEntry<TMessage> EntryFor<TMessage>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (SubjectEntry<TMessage>)_subjects.GetOrAdd(typeof(TMessage), static _ => new SubjectEntry<TMessage>());
    }

    private interface ISubjectEntry : IDisposable { }

    private sealed class SubjectEntry<TMessage> : ISubjectEntry
    {
        private readonly Subject<TMessage> _inner = new();

        public SubjectEntry() => Synchronized = Subject.Synchronize<TMessage>(_inner);

        public ISubject<TMessage> Synchronized { get; }

        public void Dispose() => _inner.Dispose();
    }
}
