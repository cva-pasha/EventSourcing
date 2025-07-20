using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Events.Extensions;

/// <summary>
///     Extension methods for <see cref="IEvent" />.
/// </summary>
public static class EventExtensions
{
    private const string MethodName = "HandleAsync";
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes = new();
    private static readonly ConcurrentDictionary<Tuple<Type, Type>, MethodInfo> HandlerMethods = new();

    /// <summary>
    ///     Publishes an event to all subscribed handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="eventModel">The event model.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException" />
    public static async Task PublishAsync<TEvent>(
        this TEvent eventModel,
        IServiceProvider serviceProvider
    ) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(eventModel);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var eventType = typeof(TEvent);
        var handlerType = HandlerTypes.GetOrAdd(eventType, t => typeof(IEventHandler<>).MakeGenericType(t));
        var handlers = serviceProvider.GetServices(handlerType).Where(x => x != null);

        var tasks = handlers.Select(handler => InvokeHandlerMethodAsync(eventModel, eventType, handler));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Publishes an event to all subscribed handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="eventModel">The event model.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException" />
    public static void Publish<TEvent>(
        this TEvent eventModel,
        IServiceProvider serviceProvider
    ) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(eventModel);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        Task.Run(async () => await PublishAsync(eventModel, serviceProvider));
    }

    private static Task InvokeHandlerMethodAsync<TEvent>(
        TEvent eventModel,
        Type eventType,
        object? handler
    ) where TEvent : IEvent
    {
        var method = GetHandlerMethod(handler, eventType);
        return (Task)method.Invoke(handler, [eventModel])!;
    }

    private static MethodInfo GetHandlerMethod(object? handler, Type eventType)
    {
        var handlerType = handler!.GetType();
        var cacheKey = new Tuple<Type, Type>(handlerType, eventType);

        if (HandlerMethods.TryGetValue(cacheKey, out var cachedMethod))
        {
            return cachedMethod;
        }

        var method = handlerType.GetMethod(MethodName, [eventType]);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Method '{MethodName}' not found on handler '{handlerType.FullName}' for event '{eventType.FullName}'."
            );
        }

        HandlerMethods.TryAdd(cacheKey, method);
        return method;
    }
}