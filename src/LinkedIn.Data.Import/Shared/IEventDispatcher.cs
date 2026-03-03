namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Dispatches domain events to all registered handlers.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Publishes <paramref name="domainEvent"/> to all handlers registered for
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The concrete domain event type.</typeparam>
    /// <param name="domainEvent">The event payload to publish.</param>
    /// <param name="cancellationToken">Token for cooperative cancellation.</param>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;

    /// <summary>
    /// Registers a handler that will be invoked whenever <typeparamref name="TEvent"/>
    /// is published.
    /// </summary>
    void Register<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent;
}
