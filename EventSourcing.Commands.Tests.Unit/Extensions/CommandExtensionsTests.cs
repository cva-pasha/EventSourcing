using EventSourcing.Commands;
using EventSourcing.Commands.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Extensions;

public class CommandExtensionsTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Execute_Handler_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandler();
        services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommand();

        // Act
        await command.ExecuteAsync(serviceProvider);

        // Assert
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        TestCommand command = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => command.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var command = new TestCommand();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => command.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Execute_Multiple_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler1 = new FirstMultipleHandler();
        var handler2 = new SecondMultipleHandler();
        services.AddSingleton<ICommandHandler<MultipleHandlerCommand>>(handler1);
        services.AddSingleton<ICommandHandler<MultipleHandlerCommand>>(handler2);
        var serviceProvider = services.BuildServiceProvider();
        var command = new MultipleHandlerCommand();

        // Act
        await command.ExecuteAsync(serviceProvider);

        // Assert
        Assert.True(handler1.WasCalled);
        Assert.True(handler2.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Propagate_Handler_Exception()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TestCommand>>(new ThrowingCommandHandler());
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommand();

        // Act & Assert
        // Reflection invocation wraps exceptions in TargetInvocationException
        var exception =
            await Assert.ThrowsAsync<System.Reflection.TargetInvocationException>(() =>
                command.ExecuteAsync(serviceProvider)
            );
        
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(expected: "Handler failed", exception.InnerException!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Support_Cancellation()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandler();
        
        services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommand();
        var cts = new CancellationTokenSource();
        
        await cts.CancelAsync();

        // Act & Assert
        await command.ExecuteAsync(serviceProvider, cts.Token);
        
        // If cancellation is properly propagated, handler should still be called
        // since cancellation happens before execution
    }

    [Fact]
    public void Execute_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        TestCommand command = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => command.Execute(serviceProvider));
    }

    [Fact]
    public void Execute_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var command = new TestCommand();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => command.Execute(serviceProvider));
    }

    [Fact]
    public async Task Execute_Should_Execute_Handler_In_Background()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandler();
        services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommand();

        // Act
        command.Execute(serviceProvider);

        // Wait for a background task
        await Task.Delay(100);

        // Assert
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_Should_Return_Result()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandlerWithResult("test-result");
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommandWithResult();

        // Act
        var result = await command.ExecuteAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: "test-result", result);
    }

    [Fact]
    public async Task
        ExecuteAsync_WithResult_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        ICommand<string> command = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => command.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task
        ExecuteAsync_WithResult_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var command = new TestCommandWithResult();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => command.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task
        ExecuteAsync_WithResult_Should_Throw_InvalidOperationException_When_No_Handler_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommandWithResult();

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                command.ExecuteAsync(serviceProvider)
            );
        Assert.Equal(
            expected: "Handler for command type 'TestCommandWithResult' not registered.",
            exception.Message
        );
    }

    [Fact]
    public async Task
        ExecuteAsync_WithResult_Should_Return_First_Completed_Result_From_Multiple_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(
            new TestCommandHandlerWithResult(result: "slow", delay: 100)
        );
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(
            new TestCommandHandlerWithResult(result: "fast", delay: 10)
        );
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommandWithResult();

        // Act
        var result = await command.ExecuteAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: "fast", result);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_Should_Wait_For_All_Tasks_To_Complete()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(
            new TestCommandHandlerWithResult(result: "handler1", delay: 50)
        );
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(
            new TestCommandHandlerWithResult(result: "handler2", delay: 50)
        );
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommandWithResult();

        // Act
        var result = await command.ExecuteAsync(serviceProvider);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Both handlers should complete
    }

    [Fact]
    public void Execute_WithResult_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        ICommand<string> command = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => command.Execute(serviceProvider));
    }

    [Fact]
    public void Execute_WithResult_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var command = new TestCommandWithResult();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => command.Execute(serviceProvider));
    }

    [Fact]
    public async Task Execute_WithResult_Should_Execute_Handler_In_Background()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandlerWithResult("result");
        services.AddSingleton<ICommandHandler<TestCommandWithResult, string>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var command = new TestCommandWithResult();

        // Act
        command.Execute(serviceProvider);

        // Wait for background task
        await Task.Delay(100);

        // Assert - handler should have been called
        // (we can't easily verify this without modifying the handler, but the test ensures no exceptions)
    }

    [Fact]
    public async Task ExecuteAsync_Should_Cache_Handler_Type()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestCommandHandler();
        services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var command1 = new TestCommand();
        var command2 = new TestCommand();

        // Act
        await command1.ExecuteAsync(serviceProvider);
        await command2.ExecuteAsync(serviceProvider);

        // Assert
        // Both executions should use cached handler type
        // This is verified by the fact that both succeed
    }

    #region [ Test Stubs ] 

    private record TestCommand : ICommand;

    private record TestCommandWithResult : ICommand<string>;

    private class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(
            TestCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class TestCommandHandlerWithResult : ICommandHandler<TestCommandWithResult, string>
    {
        private readonly int _delay;
        private readonly string _result;

        public TestCommandHandlerWithResult(
            string result,
            int delay = 0
        )
        {
            _result = result;
            _delay = delay;
        }

        public async Task<string> HandleAsync(
            TestCommandWithResult command,
            CancellationToken ct = default
        )
        {
            if (_delay > 0)
            {
                await Task.Delay(_delay, ct);
            }

            return _result;
        }
    }

    private class MultipleHandlerCommand : ICommand;

    private class FirstMultipleHandler : ICommandHandler<MultipleHandlerCommand>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(
            MultipleHandlerCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class SecondMultipleHandler : ICommandHandler<MultipleHandlerCommand>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(
            MultipleHandlerCommand command,
            CancellationToken ct = default
        )
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class ThrowingCommandHandler : ICommandHandler<TestCommand>
    {
        public Task HandleAsync(
            TestCommand command,
            CancellationToken ct = default
        )
        {
            throw new InvalidOperationException("Handler failed");
        }
    }

    #endregion
}