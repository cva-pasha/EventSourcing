using EventSourcing.Commands;
using EventSourcing.Commands.Extensions;
using EventSourcing.Tests.Unit.Commands.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Commands;

public class DiCommandsTests
{
    #region [ ExecuteAsync ]

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        SampleCommand command = default!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            command.ExecuteAsync(mockServiceProvider.Object)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        IServiceProvider serviceProvider = default!;
        SampleCommand command = default!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            command.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_InvalidOperationException_When_Handler_Is_Not_Registered()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var command = new SampleCommand();
        ICommandHandler<SampleCommand> handler = default!;

        mockServiceProvider
            .Setup(e =>
                e.GetService(typeof(ICommandHandler<SampleCommand>))
            )
            .Returns(handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(mockServiceProvider.Object)
        );
    }

    [Fact]
    public async Task ExecuteAsync_NonPublicHandler_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new SampleCommand();
        var serviceProviderMock = new Mock<IServiceProvider>();

        serviceProviderMock
            .Setup(e => e.GetService(typeof(ICommandHandler<SampleCommand>)))
            .Returns(new InternalCommandHandler());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await command.ExecuteAsync(serviceProviderMock.Object)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithRegisteredHandler_ShouldInvokeHandler()
    {
        // Arrange
        var command = new SampleCommand();
        var handlerMock = new Mock<ICommandHandler<SampleCommand>>();

        var serviceProvider = new ServiceCollection()
            .AddSingleton(handlerMock.Object)
            .BuildServiceProvider();

        // Act
        await command.ExecuteAsync(serviceProvider);

        // Assert
        handlerMock.Verify(h => h.HandleAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region [ Execute ]

    [Fact]
    public void Execute_Should_Throw_ArgumentNullException_When_Command_Is_Null()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        SampleCommand command = default!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            command.Execute(mockServiceProvider.Object)
        );
    }

    [Fact]
    public void Execute_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        IServiceProvider serviceProvider = default!;
        SampleCommand command = default!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            command.Execute(serviceProvider)
        );
    }

    #endregion
}