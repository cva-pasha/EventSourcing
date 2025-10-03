using EventSourcing.Api.Commands;
using EventSourcing.Api.Events;
using EventSourcing.Api.Models;
using EventSourcing.Api.Queries;
using EventSourcing.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddHttpContextAccessor()
    .AddEventSourcing(typeof(SampleCommand));

var app = builder.Build();

app.Services.UseEventSourcing();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

#region [ Commands ]

app.MapGet(
        pattern: "/commands/execute-and-wait-response",
        async (CancellationToken ct) =>
        {
            return await new SampleCommand(1).ExecuteAsync(ct);
        }
    )
    .WithName("ExecuteCommandAsync")
    .WithOpenApi();

app.MapGet(
        pattern: "/commands/execute-and-forget",
        (ILogger<Program> logger) =>
        {
            new FireAndForgetCommand(1).Execute();
            logger.LogInformation(message: "FireAndForgetCommand with number {Number} has been dispatched.", 1);
            return Results.Accepted(value: "Command dispatched. It will be processed in the background.");
        }
    )
    .WithName("ExecuteFireAndForgetCommand")
    .WithOpenApi();

app.MapGet(
        pattern: "/commands/concurrent/execute-one-by-one-and-wait",
        async (int count, CancellationToken ct) =>
        {
            var tasks = new List<Task<BaseResult>>();
            var number = 0;

            for (var i = 0; i < count; i++)
            {
                ++number;

                tasks.Add(
                    new ConcurrentCommand(number)
                        .ExecuteAsync(ct)
                        .WithWatcher($"{nameof(ConcurrentCommand)}_{number}", LogLevel.Information)
                );
            }

            return await Task.WhenAll(tasks);
        }
    )
    .WithName("ExecuteConcurrentCommandAsync")
    .WithOpenApi();

app.MapGet(
        pattern: "/commands/concurrent/execute-parallel-and-wait",
        async (
            int count,
            CancellationToken ct
        ) =>
        {
            var tasks = new List<Task<BaseResult>>();
            var number = 0;

            for (var i = 0; i < count; i++)
            {
                ++number;

                tasks.Add(
                    new ParallelConcurrentCommand(number)
                        .ExecuteAsync(ct)
                        .WithWatcher($"{nameof(ParallelConcurrentCommand)}_{number}", LogLevel.Information)
                );
            }

            return await Task.WhenAll(tasks);
        }
    )
    .WithName("ExecuteParallelConcurrentCommandAsync")
    .WithOpenApi();

app.MapGet(
        pattern: "/commands/concurrent/execute-parallel-and-no-wait",
        (int count) =>
        {
            var number = 0;

            Parallel.For(
                fromInclusive: 0,
                count,
                _ =>
                {
                    new ConcurrentCommand(++number).Execute();
                }
            );
        }
    )
    .WithName("ExecuteConcurrentCommand")
    .WithOpenApi();

#endregion

#region [ Events ]

app.MapGet(
        pattern: "/events/publish-and-wait-execution",
        async () =>
        {
            await new SampleEvent(1).PublishAsync();
        }
    )
    .WithName("PublishEventAsync")
    .WithOpenApi();

app.MapGet(
        pattern: "/events/publish-and-no-wait-execution",
        () =>
        {
            new SampleEvent(1).Publish();
        }
    )
    .WithName("PublishEvent")
    .WithOpenApi();

#endregion

#region [ Queries ]

app.MapGet(
        pattern: "/queries/execute-async",
        async () =>
        {
            return await new SampleQuery(1)
                .ExecuteAsync()
                .WithWatcher(nameof(SampleQuery), LogLevel.Information);
        }
    )
    .WithName("ExecuteQueryAsync")
    .WithOpenApi();

#endregion

app.Run();