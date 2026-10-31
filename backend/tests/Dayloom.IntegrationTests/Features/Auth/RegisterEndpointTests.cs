using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dayloom.Api.Features.Auth;
using Dayloom.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dayloom.IntegrationTests.Features.Auth;

public class RegisterEndpointTests(DayloomApiFactory factory) : IClassFixture<DayloomApiFactory>
{
    private const string Password = "correct horse";

    [Fact]
    public async Task Creates_an_account_and_stores_only_a_password_hash()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email, Password, "America/New_York");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal(email, body!.Email);

        await using var db = factory.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == body.Id);
        Assert.Equal(email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Equal("America/New_York", user.TimeZoneId);
        Assert.True(user.LockoutEnabled);
        Assert.NotNull(user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Rejects_an_email_that_is_already_registered_ignoring_case()
    {
        var email = NewEmail();
        await RegisterAsync(email, Password);

        var response = await RegisterAsync(email.ToUpperInvariant(), Password);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Returns_a_validation_problem_listing_the_invalid_fields()
    {
        var response = await RegisterAsync("not-an-email", "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("auth.invalid_registration", problem.GetProperty("code").GetString());
        var fields = problem.GetProperty("errors").EnumerateObject().Select(field => field.Name).Order();
        Assert.Equal(["email", "password"], fields);
    }

    [Fact]
    public async Task Rejects_a_malformed_json_body()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/register", new StringContent("{ not json", null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_sign_ups_with_the_same_email_create_exactly_one_account()
    {
        var email = NewEmail();

        // Identity checks for a duplicate with a SELECT before inserting, so parallel requests can all pass that
        // check. Only the unique index can guarantee a single account; the losers must get 409, not 500.
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RegisterAsync(email, Password)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant()));
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string password, string? timeZoneId = null) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, timeZoneId));

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";
}
