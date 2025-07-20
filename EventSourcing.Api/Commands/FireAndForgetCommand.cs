using EventSourcing.Commands;

namespace EventSourcing.Api.Commands;

public record FireAndForgetCommand(
    int Number
) : ICommand;

internal sealed class FireAndForgetCommandHandler(
    ILogger<FireAndForgetCommandHandler> logger
) : ICommandHandler<FireAndForgetCommand>
{
    public async Task HandleAsync(FireAndForgetCommand command, CancellationToken ct = default)
    {
        await Task.Delay(millisecondsDelay: 2000, ct);
        logger.LogInformation(message: "Background task for FireAndForgetCommand with number {Number} completed.", command.Number);
    }
}