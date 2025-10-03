using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Extensions;

/// <summary>
/// Extension methods for <see cref="ICommand" />.
/// </summary>
public static class CommandExtensions
{
    private const string MethodName = "HandleAsync";

    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes = new(
        concurrencyLevel: Environment.ProcessorCount,
        capacity: 100
    );

    private static readonly ConcurrentDictionary<Tuple<Type, Type>, MethodInfo> HandlerMethods =
        new(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: 100
        );

    /// <summary>
    /// Executes a command that does not return a result.
    /// </summary>
    /// <typeparam name="TCommand">
    /// The type of the command to be executed, which must implement <see cref="ICommand" />.
    /// </typeparam>
    /// <param name="command">The command to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="ct">Optional <see cref="CancellationToken" /> to cancel the execution.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException" />
    public static async Task ExecuteAsync<TCommand>(
        this TCommand command,
        IServiceProvider serviceProvider,
        CancellationToken ct = default
    ) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var commandType = typeof(TCommand);
        var commandHandlerType = HandlerTypes
            .GetOrAdd(
                commandType,
                t => typeof(ICommandHandler<>).MakeGenericType(t)
            );

        var handlers = serviceProvider
            .GetServices(commandHandlerType)
            .Where(x => x != null);

        var tasks = handlers
            .Select(handler => InvokeHandlerMethodAsync(
                    command,
                    commandType,
                    handler,
                    ct
                )
            );

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Executes a command that does not return a result.
    /// </summary>
    /// <typeparam name="TCommand">
    /// The type of the command to be executed, which must implement <see cref="ICommand" />.
    /// </typeparam>
    /// <param name="command">The command to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException" />
    public static void Execute<TCommand>(
        this TCommand command,
        IServiceProvider serviceProvider
    ) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        Task.Run(async () =>
            {
                try
                {
                    await ExecuteAsync(command, serviceProvider);
                }
                catch (Exception ex)
                {
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger(nameof(EventSourcing));

                    logger.LogError(
                        ex,
                        message: "Unhandled exception in fire-and-forget command execution"
                    );
                }
            }
        );
    }

    /// <summary>
    /// Executes a command that returns a result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the result returned by the command execution.
    /// </typeparam>
    /// <param name="command">The command to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="ct">Optional <see cref="CancellationToken" /> to cancel the execution.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException" />
    public static async Task<TResult> ExecuteAsync<TResult>(
        this ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var commandType = command.GetType();

        var commandHandlerType = HandlerTypes
            .GetOrAdd(
                commandType,
                type => typeof(ICommandHandler<,>).MakeGenericType(type, typeof(TResult))
            );

        var handlers = serviceProvider
            .GetServices(commandHandlerType)
            .Where(x => x != null)
            .ToList();

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException(
                $"Handler for command type '{commandType.Name}' not registered."
            );
        }

        var tasks = handlers
            .Select(handler => InvokeHandlerMethodAsync(
                    command,
                    commandType,
                    handler,
                    ct
                )
            )
            .ToList();

        var firstCompletedTask = await Task.WhenAny(tasks);

        await Task.WhenAll(tasks);

        return await firstCompletedTask;
    }

    /// <summary>
    /// Executes a command that returns a result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the result returned by the command execution.
    /// </typeparam>
    /// <param name="command">The command to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException" />
    public static void Execute<TResult>(
        this ICommand<TResult> command,
        IServiceProvider serviceProvider
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        Task.Run(async () =>
            {
                try
                {
                    await ExecuteAsync(command, serviceProvider);
                }
                catch (Exception ex)
                {
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger(nameof(EventSourcing));

                    logger.LogError(
                        ex,
                        message: "Unhandled exception in fire-and-forget command execution"
                    );
                }
            }
        );
    }

    private static Task InvokeHandlerMethodAsync<TCommand>(
        TCommand command,
        Type commandType,
        object? handler,
        CancellationToken ct
    ) where TCommand : ICommand
    {
        var method = GetHandlerMethod(handler, commandType);
        return (Task)method.Invoke(handler, [command, ct])!;
    }

    private static Task<TResult> InvokeHandlerMethodAsync<TResult>(
        ICommand<TResult> command,
        Type commandType,
        object? handler,
        CancellationToken ct
    )
    {
        var method = GetHandlerMethod(handler, commandType);
        return (Task<TResult>)method.Invoke(handler, [command, ct])!;
    }

    private static MethodInfo GetHandlerMethod(
        object? handler,
        Type commandType
    )
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