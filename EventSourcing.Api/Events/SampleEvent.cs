using EventSourcing.Events;

namespace EventSourcing.Api.Events;

public record SampleEvent(
    int Number
) : IEvent;

internal sealed class SampleEventHandler(
    ILogger<SampleEventHandler> logger
)
    : IEventHandler<SampleEvent>
{
    public Task HandleAsync(SampleEvent eventModel)
    {
        var message = $"{nameof(SampleEventHandler)} with number: {eventModel.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.CompletedTask;
    }
}

internal sealed class AnotherSampleEventHandler(
    ILogger<AnotherSampleEventHandler> logger
)
    : IEventHandler<SampleEvent>
{
    public Task HandleAsync(SampleEvent eventModel)
    {
        var message = $"{nameof(AnotherSampleEventHandler)} with number: {eventModel.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.CompletedTask;
    }
}