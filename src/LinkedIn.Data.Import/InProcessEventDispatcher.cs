using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import;

/// <summary>
/// In-process implementation of <see cref="IEventDispatcher"/>.
/// Uses a keyed handler registry — no reflection, no external bus, no MediatR.
/// Handlers are registered at DI startup via <see cref="Register{TEvent}"/>.
/// </summary>
public sealed class InProcessEventDispatcher : IEventDispatcher
{
    private readonly Dictionary<Type, List<Func<IDomainEvent, CancellationToken, Task>>> _handlers = [];

    /// <inheritdoc/>
    public void Register<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent
    {
        var key = typeof(TEvent);
        if (!_handlers.TryGetValue(key, out var list))
        {
            list = [];
            _handlers[key] = list;
        }

        // Wrap the strongly-typed handler in a covariant lambda so we can store
        // a uniform Func<IDomainEvent, CancellationToken, Task> in the registry.
        list.Add((evt, ct) => handler((TEvent)evt, ct));
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var key = typeof(TEvent);
        if (!_handlers.TryGetValue(key, out var list))
            return;

        foreach (var handler in list)
            await handler(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
