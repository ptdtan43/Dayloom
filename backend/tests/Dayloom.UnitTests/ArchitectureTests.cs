using System.Reflection;
using Dayloom.Application.Common.Interfaces;
using Dayloom.Domain.Auth;

namespace Dayloom.UnitTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule: dependencies only point inwards (Api → Infrastructure →
/// Application → Domain). Project references already block most mistakes; these tests also catch a framework
/// package sneaking into an inner layer.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Session).Assembly;
    private static readonly Assembly Application = typeof(IAppDbContext).Assembly;

    [Theory]
    [InlineData("Dayloom.Application")]
    [InlineData("Dayloom.Infrastructure")]
    [InlineData("Dayloom.Api")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    public void Domain_does_not_depend_on(string forbidden)
    {
        AssertNoReference(Domain, forbidden);
    }

    [Theory]
    [InlineData("Dayloom.Infrastructure")]
    [InlineData("Dayloom.Api")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    public void Application_does_not_depend_on(string forbidden)
    {
        AssertNoReference(Application, forbidden);
    }

    private static void AssertNoReference(Assembly assembly, string forbiddenPrefix)
    {
        var offending = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.True(offending.Count == 0, $"{assembly.GetName().Name} must not reference: {string.Join(", ", offending)}");
    }
}
