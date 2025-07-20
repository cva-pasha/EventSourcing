using System.Collections.Concurrent;
using System.Reflection;
using EventSourcing.Commands.Concurrent;
using EventSourcing.Commands.Concurrent.Internal;
using EventSourcing.Commands.Extensions;
using EventSourcing.Tests.Unit.Commands.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Commands.Concurrent;

public class ConcurrentCommandBusTests
{
    #region [ Subscribe ]

    [Fact]
    public void Subscribe_Should_Register_Handler()
    {
        // Arrange
        var handlerMock = new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        var commandBus = new ConcurrentCommandBus();

        // Act
        commandBus.Subscribe(handlerMock.Object);

        // Assert
        var handlersField = typeof(ConcurrentCommandBus)
            .GetField(name: "_handlers", BindingFlags.NonPublic | BindingFlags.Instance);

        var handlers = (ConcurrentDictionary<string, ConcurrentHandler>?)handlersField?.GetValue(commandBus);

        Assert.NotNull(handlers);
        Assert.True(handlers.ContainsKey(typeof(ConcurrentSampleCommand).FullName!));
    }

    [Fact]
    public void Subscribe_Should_Throw_ArgumentNullException_When_Handler_Is_Null()
    {
        // Arrange
        IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult> handler = default!;
        var commandBus = new ConcurrentCommandBus();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => commandBus.Subscribe(handler));
    }

    #endregion

    #region [ ExecuteAsync ]

    [Fact]
    public async Task ExecuteAsync_Should_Execute_Registered_Command_And_Return_Result()
    {
        // Arrange
        var handlerMock = new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        var expectedResult = new SampleResult();
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();

        handlerMock.Setup(e =>
                e.HandleAsync(
                    It.IsAny<ConcurrentSampleCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResult);

        commandBus.Subscribe(handlerMock.Object);

        // Act
        var result = await commandBus
            .ExecuteAsync<ConcurrentSampleCommand, SampleResult>(command);

        // Assert
        handlerMock.Verify(
            e =>
                e.HandleAsync(command, It.IsAny<CancellationToken>()),
            Times.Once
        );

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        ConcurrentSampleCommand command = default!;
        var commandBus = new ConcurrentCommandBus();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            commandBus.ExecuteAsync<ConcurrentSampleCommand, SampleResult>(command)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_InvalidOperationException_When_Handler_Is_Not_Registered()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commandBus.ExecuteAsync<ConcurrentSampleCommand, SampleResult>(command)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Enforce_Concurrency_Limit()
    {
        // Arrange
        const int expectedConcurrentCount = 2;

        var command = new ConcurrentSampleCommand();
        var handlerMock = new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        handlerMock.Setup(e => e.ConcurrentCount).Returns(expectedConcurrentCount);
        handlerMock
            .Setup(e => e.HandleAsync(It.IsAny<ConcurrentSampleCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConcurrentSampleCommand _, CancellationToken ct) =>
                {
                    await Task.Delay(millisecondsDelay: 1000, ct);
                    return new SampleResult();
                }
            );

        var serviceProvider = new ServiceCollection()
            .AddSingleton(handlerMock.Object)
            .BuildServiceProvider();

        // Act
        var tasks = new[]
        {
            command.ExecuteAsync(serviceProvider, CancellationToken.None),
            command.ExecuteAsync(serviceProvider, CancellationToken.None),
            command.ExecuteAsync(serviceProvider, CancellationToken.None)
        };

        await Task.Delay(200); // Give time for tasks to start and acquire the semaphore

        // Assert
        var busField = typeof(ConcurrentCommandExtensions)
            .GetField(name: "Bus", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(busField);
        var busInstance = busField.GetValue(null);
        Assert.NotNull(busInstance);

        var handlersField = typeof(ConcurrentCommandBus)
            .GetField(name: "_handlers", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(handlersField);
        var handlers = (ConcurrentDictionary<string, ConcurrentHandler>?)handlersField.GetValue(busInstance);
        Assert.NotNull(handlers);

        var handlerKey = handlerMock.Object.GetType().FullName;
        Assert.NotNull(handlerKey);
        Assert.True(handlers.ContainsKey(handlerKey), userMessage: "The handler was not added to the bus's dictionary.");

        var semaphore = handlers[handlerKey].Semaphore;

        Assert.Equal(expected: 0, semaphore.CurrentCount);

        await Task.WhenAll(tasks);

        Assert.Equal(expectedConcurrentCount, semaphore.CurrentCount);
    }

    #endregion

    #region [ Execute ]

    [Fact]
    public async Task Execute_Should_Execute_Registered_Command_And_Return_Result()
    {
        // Arrange
        var handlerMock = new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        var expectedResult = new SampleResult();
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();

        handlerMock.Setup(e =>
                e.HandleAsync(
                    It.IsAny<ConcurrentSampleCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResult);

        commandBus.Subscribe(handlerMock.Object);

        // Act
        commandBus.Execute<ConcurrentSampleCommand, SampleResult>(command);

        await Task.Delay(100);

        // Assert
        handlerMock.Verify(
            e =>
                e.HandleAsync(command, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        ConcurrentSampleCommand command = default!;
        var commandBus = new ConcurrentCommandBus();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            commandBus.Execute<ConcurrentSampleCommand, SampleResult>(command)
        );
    }

    #endregion
}