using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Extensions;

/// <summary>
/// Provides a static context for accessing the application's <see cref="IServiceProvider"/>
/// in order to resolve services without explicitly passing the provider.
/// </summary>
public static class EventSourcingContext
{
    internal static IServiceScopeFactory ScopeFactory { get; private set; } = null!;

    internal static ILogger Logger =>
        GetRequiredService<ILoggerFactory>().CreateLogger(nameof(EventSourcing));

    /// <summary>
    /// Sets the application's <see cref="IServiceScopeFactory"/> for creating new service scopes.
    /// </summary>
    /// <param name="scopeFactory">
    /// The <see cref="IServiceScopeFactory"/> to be set for creating scopes.
    /// </param>
    public static void SetScopeFactory(IServiceScopeFactory scopeFactory)
    {
        ScopeFactory = scopeFactory;
    }

    /// <summary>
    /// Creates a new service scope. The caller MUST dispose of the scope when done.
    /// </summary>
    public static IServiceScope CreateScope()
    {
        return ScopeFactory.CreateScope();
    }

    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> within a new scope.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service of type <typeparamref name="T"/> if available; otherwise, <c>null</c>.</returns>
    public static T? GetService<T>() where T : notnull
    {
        using var scope = ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetService<T>();
    }

    /// <summary>
    /// Resolves a required service of type <typeparamref name="T"/> within a new scope.
    /// Throws an exception if the service cannot be found.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service of type <typeparamref name="T"/>.</returns>
    public static T GetRequiredService<T>() where T : notnull
    {
        using var scope = ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}