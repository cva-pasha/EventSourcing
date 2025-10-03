using EventSourcing.Api.Models;
using EventSourcing.Commands.Concurrent;

namespace EventSourcing.Api.Commands;

public record ParallelConcurrentCommand(
    int Number
) : IConcurrentCommand<BaseResult>;

internal sealed class ParallelConcurrentCommandHandler(
    ILogger<ParallelConcurrentCommandHandler> logger
)
    : IConcurrentCommandHandler<ParallelConcurrentCommand, BaseResult>
{
    public int ConcurrentCount { get; init; } = 2;

    public async Task<BaseResult> HandleAsync(
        ParallelConcurrentCommand command,
        CancellationToken ct = default
    )
    {
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var message = $"{nameof(ParallelConcurrentCommandHandler)} with number:"
            + $" {command.Number} handled at {DateTime.Now:HH:mm:ss.fff}";

        logger.LogInformation(message);
        return new BaseResult(command.Number, message);
    }
}