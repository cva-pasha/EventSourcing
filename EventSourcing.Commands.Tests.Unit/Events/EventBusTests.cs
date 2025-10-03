using System.Collections.Concurrent;
using System.Reflection;
using EventSourcing.Events;
using EventSourcing.Tests.Unit.Events.Stubs;

namespace EventSourcing.Tests.Unit.Events;

public class EventBusTests
{
    [Fact]
    public void Subscribe_Should_Register_Handler()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();

        // Act
        eventBus.Subscribe(handlerMock.Object);

        // Assert
        var handlersField = typeof(EventBus).GetField(name: "_handlers", BindingFlags.NonPublic | BindingFlags.Instance);
        var handlers = (ConcurrentDictionary<string, List<object>>?)handlersField?.GetValue(eventBus);

        Assert.NotNull(handlers);
        Assert.True(handlers.ContainsKey(nameof(SampleEvent)));
        Assert.Single(handlers[nameof(SampleEvent)]);
    }

    [Fact]
    public void Subscribe_Should_Throw_ArgumentNullException_When_Handler_Is_Null()
    {
        // Arrange
        IEventHandler<SampleEvent> handler = default!;
        var eventBus = new EventBus();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => eventBus.Subscribe(handler));
    }

    [Fact]
    public async Task PublishAsync_Should_Execute_Registered_EventHandler()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();
        var eventModel = new SampleEvent();

        handlerMock.Setup(e =>
                e.HandleAsync(It.IsAny<SampleEvent>())
            )
            .Returns(Task.CompletedTask);

        eventBus.Subscribe(handlerMock.Object);

        // Act
        await eventBus.PublishAsync(eventModel);

        // Assert
        handlerMock.Verify(
            e =>
                e.HandleAsync(eventModel),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Throw_InvalidOperationException_When_Handler_Is_Not_Registered()
    {
        // Arrange
        var eventModel = new SampleEvent();
        var eventBus = new EventBus();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => eventBus.PublishAsync(eventModel));
    }

    [Fact]
    public async Task Publish_Should_Execute_Registered_EventHandler()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();
        var eventModel = new SampleEvent();

        handlerMock.Setup(e =>
                e.HandleAsync(It.IsAny<SampleEvent>())
            )
            .Returns(Task.CompletedTask);

        eventBus.Subscribe(handlerMock.Object);

        // Act
        eventBus.Publish(eventModel);

        await Task.Delay(100);

        // Assert
        handlerMock.Verify(
            e =>
                e.HandleAsync(eventModel),
            Times.Once
        );
    }

    [Fact]
    public void Publish_Should_Throw_ArgumentNullException_When_Event_Is_Null()
    {
        // Arrange
        var eventBus = new EventBus();
        SampleEvent eventModel = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => eventBus.Publish(eventModel));
    }

    [Fact]
    public async Task Publish_Should_Handle_Handler_Exceptions_Gracefully()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();
        var eventModel = new SampleEvent();

        handlerMock.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        eventBus.Subscribe<SampleEvent>(handlerMock.Object);

        // Act - fire and forget should not throw immediately
        eventBus.Publish(eventModel);

        // Wait for async execution
        await Task.Delay(100);

        // Assert
        handlerMock.Verify(h => h.HandleAsync(eventModel), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Execute_Multiple_Handlers()
    {
        // Arrange
        var handler1 = new Mock<IEventHandler<SampleEvent>>();
        var handler2 = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();
        var eventModel = new SampleEvent();

        handler1.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>())).Returns(Task.CompletedTask);
        handler2.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>())).Returns(Task.CompletedTask);

        eventBus.Subscribe<SampleEvent>(handler1.Object);
        eventBus.Subscribe<SampleEvent>(handler2.Object);

        // Act
        await eventBus.PublishAsync(eventModel);

        // Assert
        handler1.Verify(h => h.HandleAsync(eventModel), Times.Once);
        handler2.Verify(h => h.HandleAsync(eventModel), Times.Once);
    }

    [Fact]
    public async Task Subscribe_Should_Add_Multiple_Handlers_For_Same_Event()
    {
        // Arrange
        var handler1 = new Mock<IEventHandler<SampleEvent>>();
        var handler2 = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();

        handler1.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>())).Returns(Task.CompletedTask);
        handler2.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>())).Returns(Task.CompletedTask);

        // Act
        eventBus.Subscribe<SampleEvent>(handler1.Object);
        eventBus.Subscribe<SampleEvent>(handler2.Object);

        var eventModel = new SampleEvent();
        await eventBus.PublishAsync(eventModel);

        // Assert
        handler1.Verify(h => h.HandleAsync(eventModel), Times.Once);
        handler2.Verify(h => h.HandleAsync(eventModel), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Propagate_Handler_Exceptions()
    {
        // Arrange
        var handlerMock = new Mock<IEventHandler<SampleEvent>>();
        var eventBus = new EventBus();
        var eventModel = new SampleEvent();

        handlerMock.Setup(h => h.HandleAsync(It.IsAny<SampleEvent>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        eventBus.Subscribe<SampleEvent>(handlerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => eventBus.PublishAsync(eventModel));
    }

    [Fact]
    public async Task PublishAsync_Should_Throw_ArgumentNullException_When_Event_Is_Null()
    {
        // Arrange
        var eventBus = new EventBus();
        SampleEvent eventModel = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => eventBus.PublishAsync(eventModel));
    }
}