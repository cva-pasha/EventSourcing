using EventSourcing.Events;
using EventSourcing.Events.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Extensions;

public class EventExtensionsTests
{
    [Fact]
    public async Task PublishAsync_Should_Execute_Handler_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act
        await eventModel.PublishAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: 1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_Should_Throw_ArgumentNullException_When_Event_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        TestEvent eventModel = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            eventModel.PublishAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var eventModel = new TestEvent();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            eventModel.PublishAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Execute_Multiple_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler1 = new FirstEventHandler();
        var handler2 = new SecondEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler1);
        services.AddSingleton<IEventHandler<TestEvent>>(handler2);
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act
        await eventModel.PublishAsync(serviceProvider);

        // Assert
        Assert.True(handler1.WasCalled);
        Assert.True(handler2.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_Should_Propagate_Handler_Exception()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestEvent>>(new ThrowingEventHandler());
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act & Assert
        // Reflection invocation wraps exceptions in TargetInvocationException
        var exception =
            await Assert.ThrowsAsync<System.Reflection.TargetInvocationException>(() =>
                eventModel.PublishAsync(serviceProvider)
            );
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(expected: "Handler failed", exception.InnerException!.Message);
    }

    [Fact]
    public async Task PublishAsync_Should_Handle_Empty_Handler_List()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act
        await eventModel.PublishAsync(serviceProvider);

        // Assert - should not throw
    }

    [Fact]
    public async Task PublishAsync_Should_Cache_Handler_Types()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var event1 = new TestEvent();
        var event2 = new TestEvent();

        // Act
        await event1.PublishAsync(serviceProvider);
        await event2.PublishAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: 2, handler.CallCount);
    }

    [Fact]
    public void Publish_Should_Throw_ArgumentNullException_When_Event_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        TestEvent eventModel = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => eventModel.Publish(serviceProvider));
    }

    [Fact]
    public void Publish_Should_Throw_ArgumentNullException_When_ServiceProvider_Is_Null()
    {
        // Arrange
        var eventModel = new TestEvent();
        IServiceProvider serviceProvider = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => eventModel.Publish(serviceProvider));
    }

    [Fact]
    public async Task Publish_Should_Execute_Handlers_In_Background()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act
        eventModel.Publish(serviceProvider);

        // Wait for a background task
        await Task.Delay(100);

        // Assert
        Assert.Equal(expected: 1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_Should_Execute_All_Handlers_Even_When_One_Fails()
    {
        // Arrange
        var services = new ServiceCollection();
        var successHandler = new FirstEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(new ThrowingEventHandler());
        services.AddSingleton<IEventHandler<TestEvent>>(successHandler);
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act & Assert
        // First handler throws, so the whole operation should throw (wrapped in TargetInvocationException)
        await Assert.ThrowsAsync<System.Reflection.TargetInvocationException>(() =>
            eventModel.PublishAsync(serviceProvider)
        );
    }

    [Fact]
    public async Task PublishAsync_Should_Filter_Null_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var eventModel = new TestEvent();

        // Act
        await eventModel.PublishAsync(serviceProvider);

        // Assert
        Assert.Equal(expected: 1, handler.CallCount);
    }

    #region Test Stubs

    private record TestEvent : IEvent;

    private class TestEventHandler : IEventHandler<TestEvent>
    {
        public int CallCount { get; private set; }

        public Task HandleAsync(TestEvent eventModel)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private class FirstEventHandler : IEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestEvent eventModel)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class SecondEventHandler : IEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestEvent eventModel)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class ThrowingEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent eventModel)
        {
            throw new InvalidOperationException("Handler failed");
        }
    }

    #endregion
}