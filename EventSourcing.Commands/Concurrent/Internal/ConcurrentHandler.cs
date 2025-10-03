namespace EventSourcing.Commands.Concurrent.Internal;

internal sealed record ConcurrentHandler(
    object Handler,
    Type CommandType,
    SemaphoreSlim Semaphore
);