using EventSourcing.Api.Models;
using EventSourcing.Commands;

namespace EventSourcing.Api.Commands;

public record SampleCommand(
    int Number
) : ICommand<BaseResult>;

internal sealed class SampleCommandHandler(
    ILogger<SampleCommandHandler> logger
)
    : ICommandHandler<SampleCommand, BaseResult>
{
    public Task<BaseResult> HandleAsync(SampleCommand command, CancellationToken ct = default)
    {
        var message = $"{nameof(SampleCommandHandler)} with number: {command.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.FromResult(new BaseResult(command.Number, message));
    }
}

internal sealed class AnotherSampleCommandHandler(
    ILogger<AnotherSampleCommandHandler> logger
)
    : ICommandHandler<SampleCommand, BaseResult>
{
    public Task<BaseResult> HandleAsync(SampleCommand command, CancellationToken ct = default)
    {
        var message = $"{nameof(AnotherSampleCommandHandler)} with number: {command.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.FromResult(new BaseResult(command.Number, message));
    }
}