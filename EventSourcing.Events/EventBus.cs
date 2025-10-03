using System.Collections.Concurrent;

namespace EventSourcing.Events;

/// <inheritdoc cref="IEventBus" />
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<object>> _handlers;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventBus" /> class.
    /// </summary>
    public EventBus()
    {
        _handlers = new ConcurrentDictionary<string, List<object>>(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: 100
        );
    }

    /// <inheritdoc />
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = typeof(TEvent).Name;
        if (!_handlers.ContainsKey(type)) _handlers[type] = [];
        _handlers[type].Add(handler);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent eventModel) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(eventModel);
        var type = typeof(TEvent).Name;

        if (!_handlers.TryGetValue(type, out var handlers))
        {
            throw new InvalidOperationException(
                $"Handler for event type {type} not registered."
            );
        }

        foreach (var handler in handlers)
            if (handler is IEventHandler<TEvent> eventHandler)
                await eventHandler.HandleAsync(eventModel);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventModel) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(eventModel);
        Task.Run(async () => { await PublishAsync(eventModel); });
    }
}