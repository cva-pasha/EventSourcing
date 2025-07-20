using System.Collections.Concurrent;
using System.Reflection;
using EventSourcing.Commands.Concurrent.Internal;

namespace EventSourcing.Commands.Concurrent;

/// <inheritdoc cref="IConcurrentCommandBus" />
public class ConcurrentCommandBus : IConcurrentCommandBus
{
    private const string MethodName = "HandleAsync";
    private readonly ConcurrentDictionary<string, ConcurrentHandler> _handlers;
    private static readonly ConcurrentDictionary<Tuple<Type, Type>, MethodInfo> HandlerMethods = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandBus" /> class.
    /// </summary>
    public ConcurrentCommandBus()
    {
        _handlers = new ConcurrentDictionary<string, ConcurrentHandler>();
    }

    /// <inheritdoc />
    public void Subscribe<TCommand, TResult>(
        IConcurrentCommandHandler<TCommand, TResult> handler
    )
        where TCommand : IConcurrentCommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(handler);

        var type = typeof(TCommand);
        var concurrentCount = handler.ConcurrentCount;
        if (concurrentCount <= 0) concurrentCount = 1;

        _handlers[type.FullName!] = new ConcurrentHandler(
            handler,
            type,
            new SemaphoreSlim(concurrentCount, concurrentCount)
        );
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken ct = default
    )
        where TCommand : IConcurrentCommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        var type = command.GetType().FullName!;

        if (!_handlers.TryGetValue(type, out var handler)
            || handler.Handler is not IConcurrentCommandHandler<TCommand, TResult> commandHandler)
            throw new InvalidOperationException(
                $"Handler for command type {type} not registered."
            );

        try
        {
            await handler.Semaphore.WaitAsync(ct);
            return await commandHandler.HandleAsync(command, ct);
        }
        finally
        {
            handler.Semaphore.Release();
        }
    }

    /// <inheritdoc />
    public void Execute<TCommand, TResult>(TCommand command)
        where TCommand : IConcurrentCommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        Task.Run(async () => { await ExecuteAsync<TCommand, TResult>(command); });
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Type type,
        IConcurrentCommand<TResult> command,
        dynamic handler,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(handler);

        var handlerType = handler.GetType();
        var propertyInfo = handlerType.GetProperty("ConcurrentCount");
        if (propertyInfo == null)
        {
            throw new InvalidOperationException($"Property 'ConcurrentCount' not found on handler '{handlerType.FullName}'.");
        }

        var concurrentCount = (int)propertyInfo.GetValue(handler)!;
        if (concurrentCount <= 0) concurrentCount = 1;

        var concurrentHandler = _handlers
            .GetOrAdd(
                handler.GetType().FullName,
                new ConcurrentHandler(
                    handler,
                    type,
                    new SemaphoreSlim(concurrentCount, concurrentCount)
                )
            );

        try
        {
            await concurrentHandler.Semaphore.WaitAsync(ct);
            MethodInfo method = GetHandlerMethod(handler, concurrentHandler.CommandType);
            dynamic task = method.Invoke(handler, new object[] { command, ct });
            return await task;
        }
        finally
        {
            concurrentHandler.Semaphore.Release();
        }
    }

    private static MethodInfo GetHandlerMethod(object? handler, Type commandType)
    {
        var handlerType = handler!.GetType();
        var cacheKey = new Tuple<Type, Type>(handlerType, commandType);
        if (HandlerMethods.TryGetValue(cacheKey, out var cachedMethod))
        {
            return cachedMethod;
        }

        var method = handlerType.GetMethod(MethodName, [commandType, typeof(CancellationToken)]);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Method '{MethodName}' not found on handler '{handlerType.FullName}' for command '{commandType.FullName}'."
            );
        }

        HandlerMethods.TryAdd(cacheKey, method);
        return method;
    }
}