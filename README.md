# EventSourcing
[![NuGet](https://img.shields.io/nuget/v/EventSourcing.Extensions)](https://www.nuget.org/packages/EventSourcing.Extensions)

## Overview
The `EventSourcing` library for .NET provides a robust, flexible, and efficient framework for implementing event sourcing in your .NET applications. This library facilitates capturing changes to an application's state as a series of events, allowing for easy auditing, debugging, and replaying of events to restore state. It is built with dependency injection (DI) at its core, enabling seamless integration with modern .NET applications.

## Features
- **EventBus**: Efficiently publish and subscribe to events. Supports both manual subscriptions and automatic DI-based handler resolution.
- **CommandBus**: Simplify command handling with a powerful command bus that supports synchronous, asynchronous, and fire-and-forget command execution.
- **Fire-and-Forget Execution**: Execute commands and events in the background without awaiting a response, ideal for long-running or non-critical tasks.
- **Queries**: Separate read and write operations to enhance maintainability, performance, and scalability.
- **Concurrency Control**: Manage concurrent command executions with a built-in semaphore-based mechanism to ensure data integrity.
- **Extensibility**: Easily extend and customize the framework to fit your specific needs.
- **Dependency Injection**: Seamlessly integrate with the .NET DI container. Use `AddEventSourcing()` to automatically scan assemblies and register all your handlers.
- **Centralized Context**: `EventSourcingContext` provides a static entry point for service resolution, simplifying access to your services from anywhere in your application.
- **Logging**: Built-in logging for handler registration and performance monitoring of command, event, and query execution.

### Handlers
- **Visibility**: All handlers must be public.
- **Event Handlers**: Executed in parallel. If no public handlers are found, execution completes without errors.
- **Query Handlers**: Only one handler can be registered per query type.
- **Command Handlers**: Executed in parallel. For commands returning a result, the response from the first handler to complete is returned.
- **Concurrent Command Handlers**: Executed in parallel, with concurrency control applied per handler type using a semaphore.

### Dependency Injection
The `EventSourcingExtensions` class is the cornerstone of the library's DI integration.

- **`AddEventSourcing(params Type[] types)`**: This extension method scans the assemblies of the provided types and automatically registers all found command, event, and query handlers in the DI container. By default, handlers are registered with a transient lifetime.

- **`UseEventSourcing(this IServiceProvider serviceProvider)`**: This method configures the `EventSourcingContext` with the application's `IServiceScopeFactory`, enabling static access to services throughout the application.

### EventSourcingContext
`EventSourcingContext` is a static class that provides a convenient way to resolve services and create service scopes without needing to inject `IServiceProvider` everywhere. This is particularly useful for executing commands, events, and queries directly from your domain objects.

- **`CreateScope()`**: Creates a new `IServiceScope`.
- **`GetService<T>()` / `GetRequiredService<T>()`**: Resolves a service from the DI container within a new scope.
- **`Logger`**: Provides a static logger instance for the library.

### Execution Flow
With `EventSourcingContext` configured, you can execute commands, publish events, and execute queries directly on the respective objects. The library's extension methods will use the context to create a service scope and resolve the necessary handlers.

## Getting Started
For additional examples and usage details, please see the [EventSourcing.Api](https://github.com/covali-pavel-developer/EventSourcing/tree/main/EventSourcing.Api) project.

### Installation
Add the EventSourcing.Extensions library to your project via NuGet:
```bash
dotnet add package EventSourcing.Extensions
```

### Usage
#### 1. Configuration
In your `Program.cs` or startup class, configure Event Sourcing by registering handlers and setting up the context.

```csharp
// Program.cs
using EventSourcing.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 1. Add EventSourcing and scan for handlers in the specified assemblies
builder.Services.AddEventSourcing(typeof(Program)); 

var app = builder.Build();

// 2. Configure the EventSourcingContext
app.Services.UseEventSourcing(); 

// ... rest of the configuration
```

#### 2. Define your Commands, Events, and Queries
```csharp
// In your application's domain layer

// Command with no result
public class RegisterUserCommand : ICommand 
{
    public string UserName { get; set; }
    public string Email { get; set; }
}

// Command with a result
public class GetUserTokenCommand : ICommand<string>
{
    public string UserId { get; set; }
}

// Event
public class UserRegisteredEvent : IEvent 
{
    public string UserId { get; set; }
}

// Query
public record GetUserByIdQuery(string Id) : IQuery<User>;
```

#### 3. Implement Handlers
Handlers are simple classes that implement the corresponding `I...Handler` interface. They will be automatically discovered by `AddEventSourcing`.

```csharp
// Command Handler
public class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand>
{
    public async Task HandleAsync(RegisterUserCommand command, CancellationToken ct = default)
    {
        // ... logic to register a user
        
        // Publish an event after the command is handled
        await new UserRegisteredEvent { UserId = "123" }.PublishAsync();
    }
}

// Event Handler
public class UserRegisteredEventHandler : IEventHandler<UserRegisteredEvent>
{
    public Task HandleAsync(UserRegisteredEvent eventModel, CancellationToken ct = default)
    {
        // ... logic to handle the user registration event (e.g., send a welcome email)
        return Task.CompletedTask;
    }
}

// Query Handler
public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, User>
{
    public async Task<User> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default)
    {
        // ... logic to retrieve a user from the database
        return new User { Id = query.Id, Name = "John Doe" };
    }
}
```

#### 4. Execute from your Application
Now you can execute commands, publish events, and run queries from anywhere in your application, such as an API controller or another service.

```csharp
[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserCommand command)
    {
        // Execute a command that returns a result
        await command.ExecuteAsync();
        return Ok();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        // Execute a query
        var user = await new GetUserByIdQuery(id).ExecuteAsync();
        return Ok(user);
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(string userId)
    {
        // Execute a command that returns a result
        var token = await new GetUserTokenCommand { UserId = userId }.ExecuteAsync();
        return Ok(token);
    }
}
```

#### Fire-and-Forget Execution
For operations that don't need to block the calling thread, use the `Execute()` or `Publish()` methods. These run the operation on a background thread.

```csharp
public void SomeLongRunningProcess()
{
    var command = new SomeLongRunningCommand();
    
    // The method returns immediately
    command.Execute(); 
}
```
Any exceptions during fire-and-forget execution are automatically caught and logged.

#### Concurrent Commands
For commands that require concurrency control, implement `IConcurrentCommand<TResult>`. The library ensures that only a specified number of handlers of the same type can execute concurrently.

```csharp
// Concurrent Command
public class ProcessPaymentCommand : IConcurrentCommand<bool>
{
    public decimal Amount { get; set; }
}

// Concurrent Command Handler
public class ProcessPaymentCommandHandler : IConcurrentCommandHandler<ProcessPaymentCommand, bool>
{
    public async Task<bool> HandleAsync(ProcessPaymentCommand command, CancellationToken ct = default)
    {
        // ... payment processing logic
        return true;
    }
}

// Execution is the same
var paymentResult = await new ProcessPaymentCommand { Amount = 99.99m }.ExecuteAsync();
```

### Manual Bus (Advanced)
While the DI-based approach is recommended, you can still use the manual `EventBus` and `CommandBus` if you need more control over handler subscriptions.

```csharp
var commandBus = new CommandBus();
commandBus.Subscribe(new RegisterUserCommandHandler());
await commandBus.ExecuteAsync(new RegisterUserCommand());
```

### Contributing
I welcome contributions from the community! Please read my contributing guidelines on GitHub to get started.

### License
This project is licensed under the MIT License. See the [LICENSE](https://github.com/covali-pavel-developer/EventSourcing/blob/main/LICENSE.txt) file for more information.
