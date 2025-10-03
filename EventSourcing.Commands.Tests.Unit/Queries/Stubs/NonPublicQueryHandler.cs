using EventSourcing.Queries;

namespace EventSourcing.Tests.Unit.Queries.Stubs;

internal sealed class NonPublicQueryHandler : IQueryHandler<TestQuery, string>
{
    public ushort InvokeCount { get; private set; } = 0;

    public Task<string> HandleAsync(TestQuery query, CancellationToken ct = default)
    {
        InvokeCount++;
        return Task.FromResult("TestResult");
    }
}