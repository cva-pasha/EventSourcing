using EventSourcing.Queries;
using EventSourcing.Queries.Extensions;
using EventSourcing.Tests.Unit.Queries.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Queries;

public sealed class QueryTests
{
    [Fact]
    public async Task ExecuteAsync_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        IQuery<string> query = default!;
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await query.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_NullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var query = new TestQuery();
        IServiceProvider serviceProvider = default!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await query.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_HandlerNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = new TestQuery();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await query.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_MultipleHandlersRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = new TestQuery();

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IQueryHandler<TestQuery, string>, TestQueryHandler>()
            .AddSingleton<IQueryHandler<TestQuery, string>, TestQueryHandler>()
            .BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await query.ExecuteAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task ExecuteAsync_NonPublicHandler_CallsHandleAsync()
    {
        // Arrange
        var query = new TestQuery();
        var nonPublicHandler = new NonPublicQueryHandler();

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IQueryHandler<TestQuery, string>, NonPublicQueryHandler>(_ => nonPublicHandler)
            .BuildServiceProvider();

        // Act
        var result = await query.ExecuteAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: "TestResult", result);
        Assert.Equal(expected: 1, nonPublicHandler.InvokeCount);
    }

    [Fact]
    public async Task ExecuteAsync_ValidHandler_CallsHandleAsync()
    {
        // Arrange
        var query = new TestQuery();
        var handlerMock = new Mock<IQueryHandler<TestQuery, string>>();

        handlerMock
            .Setup(x => x.HandleAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync("TestResult");

        var serviceProvider = new ServiceCollection()
            .AddSingleton(handlerMock.Object)
            .BuildServiceProvider();

        // Act
        var result = await query.ExecuteAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: "TestResult", result);
        handlerMock.Verify(x => x.HandleAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }
}