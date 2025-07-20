using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Extensions;

public static class TaskExtensions
{
    /// <summary>
    ///     Executes the specified task with a stopwatch.
    /// </summary>
    public static async Task<TResult> WithWatcher<TResult>(
        this Task<TResult> task,
        string operation,
        LogLevel logLevel = LogLevel.Debug
    )
    {
        var logger = EventSourcingContext.Logger;
        if (logger?.IsEnabled(logLevel) != true)
        {
            return await task;
        }

        var sw = new Stopwatch();

        logger.Log(
            logLevel,
            message: "{Operation} started execution.",
            operation
        );

        sw.Start();

        var result = await task;

        sw.Stop();

        logger.Log(
            logLevel,
            message: "{Operation} finished execution in {ElapsedMilliseconds} ms.",
            operation,
            sw.ElapsedMilliseconds
        );

        return result;
    }

    /// <summary>
    ///     Executes the specified task with a stopwatch.
    /// </summary>
    public static async Task WithWatcher(
        this Task task,
        string operation,
        LogLevel logLevel = LogLevel.Debug
    )
    {
        var logger = EventSourcingContext.Logger;
        if (logger?.IsEnabled(logLevel) != true)
        {
            await task;
            return;
        }

        var sw = new Stopwatch();

        logger.Log(
            logLevel,
            message: "{Operation} started execution.",
            operation
        );

        sw.Start();
        await task;
        sw.Stop();

        logger.Log(
            logLevel,
            message: "{Operation} finished execution in {ElapsedMilliseconds} ms.",
            operation,
            sw.ElapsedMilliseconds
        );
    }
}