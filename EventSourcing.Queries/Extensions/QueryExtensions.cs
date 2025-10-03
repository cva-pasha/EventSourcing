using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Queries.Extensions;

/// <summary>
/// Extension methods for <see cref="IQuery{TResult}" />.
/// </summary>
public static class QueryExtensions
{
    private const string MethodName = "HandleAsync";

    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes =
        new(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: 100
        );

    private static readonly ConcurrentDictionary<Tuple<Type, Type>, MethodInfo> HandlerMethods =
        new(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: 100
        );

    /// <summary>
    /// Executes a query that returns a result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the result returned by the query execution.
    /// </typeparam>
    /// <param name="query">The query to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="ct">Optional <see cref="CancellationToken" /> to cancel the execution.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException" />
    public static async Task<TResult> ExecuteAsync<TResult>(
        this IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var queryType = query.GetType();
        var handlerType = HandlerTypes.GetOrAdd(
            queryType,
            type => typeof(IQueryHandler<,>).MakeGenericType(type, typeof(TResult))
        );
        var handlers = serviceProvider.GetServices(handlerType).Where(x => x != null).ToList();

        switch (handlers.Count)
        {
            case 0:
                throw new InvalidOperationException(
                    $"Handler for query type {queryType.Name} not registered."
                );
            case > 1:
                throw new InvalidOperationException(
                    $"Query has {handlers.Count} handlers, but only one is allowed."
                );
        }

        var handler = handlers[0];
        return await InvokeHandlerMethodAsync(
            query,
            queryType,
            handler,
            ct
        );
    }

    private static Task<TResult> InvokeHandlerMethodAsync<TResult>(
        IQuery<TResult> query,
        Type queryType,
        object? handler,
        CancellationToken ct
    )
    {
        var method = GetHandlerMethod(handler, queryType);
        return (Task<TResult>)method.Invoke(handler, [query, ct])!;
    }

    private static MethodInfo GetHandlerMethod(
        object? handler,
        Type queryType
    )
    {
        var handlerType = handler!.GetType();
        var cacheKey = new Tuple<Type, Type>(handlerType, queryType);

        if (HandlerMethods.TryGetValue(cacheKey, out var cachedMethod))
        {
            return cachedMethod;
        }

        var method = handlerType
            .GetMethod(MethodName, [queryType, typeof(CancellationToken)]);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Method '{MethodName}' not found on handler '{handlerType.FullName}' for query '{queryType.FullName}'."
            );
        }

        HandlerMethods.TryAdd(cacheKey, method);
        return method;
    }
}