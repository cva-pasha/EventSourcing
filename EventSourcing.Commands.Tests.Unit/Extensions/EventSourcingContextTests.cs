using EventSourcing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Tests.Unit.Extensions;

public class EventSourcingContextTests
{
    [Fact]
    public void SetScopeFactory_Should_Set_ScopeFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Assert
        // Verify by accessing ServiceProvider
        var sp = EventSourcingContext.CreateScope().ServiceProvider;
        Assert.NotNull(sp);
    }

    [Fact]
    public void ServiceProvider_Should_Return_Scoped_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var sp1 = EventSourcingContext.CreateScope().ServiceProvider;
        var sp2 = EventSourcingContext.CreateScope().ServiceProvider;

        // Assert
        Assert.NotNull(sp1);
        Assert.NotNull(sp2);
        Assert.NotSame(sp2, sp1); // Different scopes
    }

    [Fact]
    public void GetService_Should_Return_Service_When_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service = EventSourcingContext.GetService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.Equal(expected: "test-value", service.GetValue());
    }

    [Fact]
    public void GetService_Should_Return_Null_When_Not_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service = EventSourcingContext.GetService<ITestService>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetRequiredService_Should_Return_Service_When_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service = EventSourcingContext.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.Equal(expected: "test-value", service.GetValue());
    }

    [Fact]
    public void GetRequiredService_Should_Throw_When_Not_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            EventSourcingContext.GetRequiredService<ITestService>
        );
    }

    [Fact]
    public void GetService_Should_Create_New_Scope_For_Each_Call()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service1 = EventSourcingContext.GetService<ITestService>();
        var service2 = EventSourcingContext.GetService<ITestService>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        // For scoped services, different scopes mean different instances
        Assert.NotSame(service2, service1);
    }

    [Fact]
    public void GetRequiredService_Should_Create_New_Scope_For_Each_Call()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service1 = EventSourcingContext.GetRequiredService<ITestService>();
        var service2 = EventSourcingContext.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        // For scoped services, different scopes mean different instances
        Assert.NotSame(service2, service1);
    }

    [Fact]
    public void GetService_Should_Return_Singleton_Instance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        EventSourcingContext.SetScopeFactory(scopeFactory);

        // Act
        var service1 = EventSourcingContext.GetService<ITestService>();
        var service2 = EventSourcingContext.GetService<ITestService>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        // Singleton instances should be the same across different scopes
        Assert.Same(service2, service1);
    }

    private interface ITestService
    {
        string GetValue();
    }

    private class TestService : ITestService
    {
        public string GetValue()
        {
            return "test-value";
        }
    }
}