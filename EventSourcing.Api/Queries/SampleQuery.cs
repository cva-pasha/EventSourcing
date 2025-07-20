using EventSourcing.Api.Models;
using EventSourcing.Queries;

namespace EventSourcing.Api.Queries;

public record SampleQuery(
    int Number
) : IQuery<BaseResult>;

internal sealed class SampleQueryHandler(
    ILogger<SampleQueryHandler> logger
)
    : IQueryHandler<SampleQuery, BaseResult>
{
    public Task<BaseResult> HandleAsync(SampleQuery query, CancellationToken ct = default)
    {
        var message = $"{nameof(SampleQueryHandler)} with number: {query.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.FromResult(new BaseResult(query.Number, message));
    }
}

internal sealed class AnotherSampleQueryHandler(
    ILogger<AnotherSampleQueryHandler> logger
)
    : IQueryHandler<SampleQuery, BaseResult>
{
    public Task<BaseResult> HandleAsync(SampleQuery query, CancellationToken ct = default)
    {
        var message = $"{nameof(AnotherSampleQueryHandler)} with number: {query.Number} handled at {DateTime.Now:HH:mm:ss.fff}";
        logger.LogInformation(message);
        return Task.FromResult(new BaseResult(query.Number, message));
    }
}