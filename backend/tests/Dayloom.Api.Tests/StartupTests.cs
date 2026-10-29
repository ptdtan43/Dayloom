using Microsoft.AspNetCore.Mvc.Testing;

namespace Dayloom.Api.Tests;

public class StartupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_to_start_when_connection_string_is_blank(string connectionString)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Default", connectionString));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Connection string 'Default' is missing or blank", exception.Message);
    }
}
