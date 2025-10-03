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
        var handlerMock =
            new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        var commandBus = new ConcurrentCommandBus();

        // Act
        commandBus.Subscribe(handlerMock.Object);

        // Assert
        var handlersField = typeof(ConcurrentCommandBus)
            .GetField(name: "_handlers", BindingFlags.NonPublic | BindingFlags.Instance);

        var handlers =
            (ConcurrentDictionary<string, ConcurrentHandler>?)handlersField?.GetValue(commandBus);

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
        var handlerMock =
            new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
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
    public async Task
        ExecuteAsync_Should_Throw_InvalidOperationException_When_Handler_Is_Not_Registered()
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
        var handlerMock =
            new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        handlerMock.Setup(e => e.ConcurrentCount).Returns(expectedConcurrentCount);
        handlerMock
            .Setup(e => e.HandleAsync(
                    It.IsAny<ConcurrentSampleCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async (
                    ConcurrentSampleCommand _,
                    CancellationToken ct
                ) =>
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
        var handlers =
            (ConcurrentDictionary<string, ConcurrentHandler>?)handlersField.GetValue(busInstance);
        Assert.NotNull(handlers);

        var handlerKey = handlerMock.Object.GetType().FullName;
        Assert.NotNull(handlerKey);
        Assert.True(
            handlers.ContainsKey(handlerKey),
            userMessage: "The handler was not added to the bus's dictionary."
        );

        var semaphore = handlers[handlerKey].Semaphore;

        Assert.Equal(expected: 0, semaphore.CurrentCount);

        await Task.WhenAll(tasks);

        Assert.Equal(expectedConcurrentCount, semaphore.CurrentCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithRegisteredHandler_ShouldInvokeAndReturnResult()
    {
        // Arrange
        var command = new ConcurrentSampleCommand();
        var expectedResult = new SampleResult();
        var handlerMock =
            new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
        handlerMock.Setup(h => h.ConcurrentCount).Returns(1);

        handlerMock
            .Setup(h => h.HandleAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(handlerMock.Object)
            .BuildServiceProvider();

        // Act
        var result = await command.ExecuteAsync(serviceProvider);

        // Assert
        handlerMock.Verify(h => h.HandleAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(expectedResult, result);
    }

    #endregion

    #region [ Execute ]

    [Fact]
    public async Task Execute_Should_Execute_Registered_Command_And_Return_Result()
    {
        // Arrange
        var handlerMock =
            new Mock<IConcurrentCommandHandler<ConcurrentSampleCommand, SampleResult>>();
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

    #region [ ExecuteAsync Dynamic ]

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Execute_Handler_Successfully()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandler();
        var type = typeof(ConcurrentSampleCommand);

        // Act
        var result = await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Throw_ArgumentNullException_When_Type_Is_Null()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandler();
        Type type = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => commandBus.ExecuteAsync(
                type,
                command,
                handler
            )
        );
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Throw_When_Handler_Is_Null()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var type = typeof(ConcurrentSampleCommand);
        dynamic handler = null!;

        // Act & Assert
        // Dynamic handler being null causes RuntimeBinderException due to ambiguous ThrowIfNull overloads
        await Assert.ThrowsAsync<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>(() =>
            commandBus.ExecuteAsync(
                type,
                command,
                handler
            )
        );
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Throw_When_ConcurrentCount_Property_Missing()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var type = typeof(ConcurrentSampleCommand);
        var handler = new HandlerWithoutConcurrentCount();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await commandBus.ExecuteAsync(
                type,
                command,
                handler
            )
        );
        Assert.Contains(
            expectedSubstring: "Property 'ConcurrentCount' not found on handler",
            exception.Message
        );
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Throw_When_HandleAsync_Method_Missing()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var type = typeof(ConcurrentSampleCommand);
        var handler = new HandlerWithoutHandleAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await commandBus.ExecuteAsync(
                type,
                command,
                handler
            )
        );
        Assert.Contains(
            expectedSubstring: "Method 'HandleAsync' not found on handler",
            exception.Message
        );
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Default_ConcurrentCount_To_One_When_Zero()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandlerWithZeroConcurrency();
        var type = typeof(ConcurrentSampleCommand);

        // Act
        var result = await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Default_ConcurrentCount_To_One_When_Negative()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandlerWithNegativeConcurrency();
        var type = typeof(ConcurrentSampleCommand);

        // Act
        var result = await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Use_GetOrAdd_For_Handler_Registration()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandler();
        var type = typeof(ConcurrentSampleCommand);

        // Act
        var result1 = await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );
        var result2 = await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(expected: 2, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Cache_Method_Info()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new TestDynamicHandler();
        var type = typeof(ConcurrentSampleCommand);

        // Act
        await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );
        await commandBus.ExecuteAsync(
            type,
            command,
            handler
        );

        // Assert
        Assert.Equal(expected: 2, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Dynamic_Should_Release_Semaphore_On_Exception()
    {
        // Arrange
        var commandBus = new ConcurrentCommandBus();
        var command = new ConcurrentSampleCommand();
        var handler = new ThrowingDynamicHandler();
        var type = typeof(ConcurrentSampleCommand);

        // Act & Assert
        // Dynamic invocation wraps exceptions in TargetInvocationException
        var exception1 =
            await Assert.ThrowsAsync<TargetInvocationException>(() =>
                commandBus.ExecuteAsync(
                    type,
                    command,
                    handler
                )
            );
        Assert.IsType<InvalidOperationException>(exception1.InnerException);

        // Verify semaphore is released by executing again
        var exception2 =
            await Assert.ThrowsAsync<TargetInvocationException>(() =>
                commandBus.ExecuteAsync(
                    type,
                    command,
                    handler
                )
            );

        Assert.IsType<InvalidOperationException>(exception2.InnerException);
    }

    #endregion

    #region [ Test Helpers for Dynamic Execution ]

    private class TestDynamicHandler
    {
        public int ConcurrentCount => 1;
        public bool WasCalled { get; private set; }
        public int CallCount { get; private set; }

        public Task<SampleResult> HandleAsync(
            ConcurrentSampleCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            CallCount++;
            return Task.FromResult(new SampleResult());
        }
    }

    private class TestDynamicHandlerWithZeroConcurrency
    {
        public int ConcurrentCount => 0;
        public bool WasCalled { get; private set; }

        public Task<SampleResult> HandleAsync(
            ConcurrentSampleCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            return Task.FromResult(new SampleResult());
        }
    }

    private class TestDynamicHandlerWithNegativeConcurrency
    {
        public int ConcurrentCount => -5;
        public bool WasCalled { get; private set; }

        public Task<SampleResult> HandleAsync(
            ConcurrentSampleCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            return Task.FromResult(new SampleResult());
        }
    }

    private class HandlerWithoutConcurrentCount
    {
        // Missing ConcurrentCount property
        public Task<SampleResult> HandleAsync(
            ConcurrentSampleCommand command,
            CancellationToken ct = default
        )
        {
            return Task.FromResult(new SampleResult());
        }
    }

    private class HandlerWithoutHandleAsync
    {
        public int ConcurrentCount => 1;
        // Missing HandleAsync method
    }

    private class ThrowingDynamicHandler
    {
        public int ConcurrentCount => 1;

        public Task<SampleResult> HandleAsync(
            ConcurrentSampleCommand command,
            CancellationToken ct = default
        )
        {
            throw new InvalidOperationException("Handler failed");
        }
    }

    #endregion
}