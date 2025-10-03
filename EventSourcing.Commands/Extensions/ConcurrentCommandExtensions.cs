using System.Collections.Concurrent;
using EventSourcing.Commands.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Extensions;

public static class ConcurrentCommandExtensions
{
    private const string MethodName = "HandleAsync";

    private static readonly ConcurrentCommandBus Bus = new();

    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes = new(
        concurrencyLevel: Environment.ProcessorCount,
        capacity: 100
    );

    /// <summary>
    /// Executes a concurrent command that returns a result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the result returned by the command execution.
    /// </typeparam>
    /// <param name="command">The concurrent command to be executed.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="ct">Optional <see cref="CancellationToken" /> to cancel the execution.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="InvalidOperationException" />
    public static async Task<TResult> ExecuteAsync<TResult>(
        this IConcurrentCommand<TResult> command,
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
                typeof(IConcurrentCommandHandler<,>).MakeGenericType(commandType, typeof(TResult))
            );

        List<dynamic> handlers = serviceProvider
            .GetServices(commandHandlerType)
            .Where(x => x != null).ToList()!;

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException(
                $"Handler for concurrent command type '{commandType.Name}' not registered."
            );
        }

        var tasks = handlers
            .Select(handler => (Task<TResult>)Bus.ExecuteAsync(
                    commandType,
                    command,
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
    /// Executes a concurrent command that returns a result.
    /// </summary>
    public static void Execute<TResult>(
        this IConcurrentCommand<TResult> command,
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
}