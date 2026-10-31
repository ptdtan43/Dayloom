using Dayloom.Application.Common;
using Dayloom.Application.Common.Interfaces;
using Dayloom.Application.Common.Results;
using Dayloom.Application.Features.Auth;
using Dayloom.Application.Features.Auth.Register;

namespace Dayloom.UnitTests.Application.Auth;

public class RegisterUserHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);
    private const string ValidPassword = "correct horse";

    private readonly FakeIdentityService _identity = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests() => _handler = new RegisterUserHandler(_identity, new FixedClock(Now));

    [Fact]
    public async Task Creates_the_user_with_a_trimmed_email_and_the_current_time()
    {
        var result = await _handler.HandleAsync(new RegisterUserCommand("  tan@example.com ", ValidPassword, "Asia/Ho_Chi_Minh"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new RegisteredUser(_identity.CreatedId, "tan@example.com"), result.Value);
        Assert.Equal(new NewUser("tan@example.com", "Asia/Ho_Chi_Minh", Now), _identity.LastUser);
        Assert.Equal(ValidPassword, _identity.LastPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Requires_an_email(string? email)
    {
        var error = await FailAsync(new RegisterUserCommand(email, ValidPassword, null));

        Assert.Equal(["email"], error.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("tan@")]
    [InlineData("@example.com")]
    [InlineData("Tan <tan@example.com>")]
    public async Task Rejects_an_invalid_email(string email)
    {
        var error = await FailAsync(new RegisterUserCommand(email, ValidPassword, null));

        Assert.Equal(["email"], error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Rejects_an_email_longer_than_256_characters()
    {
        var email = new string('a', 245) + "@example.com"; // 257 characters

        var error = await FailAsync(new RegisterUserCommand(email, ValidPassword, null));

        Assert.Equal(["email"], error.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    [InlineData(129)]
    public async Task Rejects_a_password_outside_8_to_128_characters(int? length)
    {
        var password = length is null ? null : new string('p', length.Value);

        var error = await FailAsync(new RegisterUserCommand("tan@example.com", password, null));

        Assert.Equal(["password"], error.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public async Task Accepts_a_password_of_exactly_8_or_128_characters(int length)
    {
        var result = await _handler.HandleAsync(new RegisterUserCommand("tan@example.com", new string('p', length), null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Reports_every_invalid_field_at_once()
    {
        var error = await FailAsync(new RegisterUserCommand("bad", "short", null));

        Assert.Equal(["email", "password"], error.FieldErrors!.Keys.Order());
    }

    [Theory]
    [InlineData(null, TimeZones.Default)]
    [InlineData("America/New_York", "America/New_York")]
    [InlineData("Not/AZone", TimeZones.Default)]
    [InlineData("SE Asia Standard Time", TimeZones.Default)] // a Windows id, not IANA
    public async Task Stores_a_valid_iana_time_zone_or_falls_back_to_vietnam(string? requested, string expected)
    {
        await _handler.HandleAsync(new RegisterUserCommand("tan@example.com", ValidPassword, requested));

        Assert.Equal(expected, _identity.LastUser!.TimeZoneId);
    }

    [Fact]
    public async Task Passes_on_the_email_taken_error()
    {
        _identity.NextResult = AuthErrors.EmailTaken;

        var result = await _handler.HandleAsync(new RegisterUserCommand("tan@example.com", ValidPassword, null));

        Assert.Equal(AuthErrors.EmailTaken, result.Error);
    }

    private async Task<Error> FailAsync(RegisterUserCommand command)
    {
        var result = await _handler.HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("auth.invalid_registration", result.Error.Code);
        Assert.Null(_identity.LastUser); // invalid input never reaches the user store
        return result.Error;
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public Guid CreatedId { get; } = Guid.NewGuid();
        public Result<Guid>? NextResult { get; set; }
        public NewUser? LastUser { get; private set; }
        public string? LastPassword { get; private set; }

        public Task<Result<Guid>> CreateUserAsync(NewUser user, string password)
        {
            LastUser = user;
            LastPassword = password;
            return Task.FromResult(NextResult ?? CreatedId);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
