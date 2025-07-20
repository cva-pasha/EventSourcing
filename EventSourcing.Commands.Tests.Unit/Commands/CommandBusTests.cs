using System.Collections.Concurrent;
using System.Reflection;
using EventSourcing.Commands;
using EventSourcing.Tests.Unit.Commands.Stubs;

namespace EventSourcing.Tests.Unit.Commands;

public class CommandBusTests
{
    #region [ Subscribe ]

    [Fact]
    public void Subscribe_Should_Register_Handler()
    {
        // Arrange
        var handlerMock = new Mock<ICommandHandler<SampleCommand>>();
        var commandBus = new CommandBus();

        // Act
        commandBus.Subscribe(handlerMock.Object);

        // Assert
        var handlersField = typeof(CommandBus).GetField(name: "_handlers", BindingFlags.NonPublic | BindingFlags.Instance);
        var handlers = (ConcurrentDictionary<string, object>?)handlersField?.GetValue(commandBus);

        Assert.NotNull(handlers);
        Assert.True(handlers.ContainsKey(nameof(SampleCommand)));
    }

    [Fact]
    public void Subscribe_Should_Throw_ArgumentNullException_When_Handler_Is_Null()
    {
        // Arrange
        ICommandHandler<SampleCommand> handler = default!;
        var commandBus = new CommandBus();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => commandBus.Subscribe(handler));
    }
    
    #endregion

    #region [ ExecuteAsync ]

    [Fact]
    public async Task ExecuteAsync_Should_Execute_Registered_Command()
    {
        // Arrange
        var handlerMock = new Mock<ICommandHandler<SampleCommand>>();
        var commandBus = new CommandBus();
        var command = new SampleCommand();

        handlerMock.Setup(e =>
                e.HandleAsync(It.IsAny<SampleCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        commandBus.Subscribe(handlerMock.Object);

        // Act
        await commandBus.ExecuteAsync(command);

        // Assert
        handlerMock.Verify(
            e =>
                e.HandleAsync(command, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        SampleCommand command = default!;
        var commandBus = new CommandBus();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => commandBus.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_InvalidOperationException_When_Handler_Is_Not_Registered()
    {
        // Arrange
        var command = new SampleCommand();
        var commandBus = new CommandBus();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => commandBus.ExecuteAsync(command));
    }

    #endregion

    #region [ Execute ]

    [Fact]
    public async Task Execute_Should_Execute_Registered_Command()
    {
        // Arrange
        var handlerMock = new Mock<ICommandHandler<SampleCommand>>();
        var commandBus = new CommandBus();
        var command = new SampleCommand();

        handlerMock.Setup(e =>
                e.HandleAsync(It.IsAny<SampleCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        commandBus.Subscribe(handlerMock.Object);

        // Act
        commandBus.Execute(command);

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
        SampleCommand command = default!;
        var commandBus = new CommandBus();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => commandBus.Execute(command));
    }

    #endregion
}