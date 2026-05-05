using FluentAssertions;
using SaaSonic.Application.Auth.Commands;

namespace SaaSonic.Application.Tests.Auth.Commands;

/// <summary>
/// Tests for FluentValidation validators.
/// Validators run before the handler (via MediatR pipeline behaviour), so they
/// are a separate unit: they do not need a database or mocks.
/// </summary>
public class ValidatorTests
{
    // ── LoginCommandValidator ─────────────────────────────────────────────────

    [Fact]
    public void LoginValidator_ValidInput_PassesValidation()
    {
        var result = new LoginCommandValidator()
            .Validate(new LoginCommand("user@test.com", "Password1!"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    public void LoginValidator_InvalidEmail_FailsValidation(string email)
    {
        var result = new LoginCommandValidator()
            .Validate(new LoginCommand(email, "Password1!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public void LoginValidator_EmptyPassword_FailsValidation()
    {
        var result = new LoginCommandValidator()
            .Validate(new LoginCommand("user@test.com", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }

    // ── RegisterCommandValidator ──────────────────────────────────────────────

    [Fact]
    public void RegisterValidator_ValidInput_PassesValidation()
    {
        var result = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@test.com", "Password1!", "Alice"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password1!", "Alice")]       // empty email
    [InlineData("not-an-email", "Password1!", "Alice")]  // invalid email format
    [InlineData("user@test.com", "", "Alice")]    // empty password
    [InlineData("user@test.com", "short1A", "Alice")]  // password too short (< 8 chars)
    [InlineData("user@test.com", "nouppercase1", "Alice")]  // missing uppercase
    [InlineData("user@test.com", "NoDigits!", "Alice")]    // missing digit
    [InlineData("user@test.com", "Password1!", "")]        // empty display name
    public void RegisterValidator_InvalidInput_FailsValidation(
        string email, string password, string displayName)
    {
        var result = new RegisterCommandValidator()
            .Validate(new RegisterCommand(email, password, displayName));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RegisterValidator_PasswordTooShort_HasCorrectErrorMessage()
    {
        var result = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@test.com", "Ab1!", "Alice"));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterCommand.Password) &&
            e.ErrorMessage.Contains("8 characters"));
    }

    [Fact]
    public void RegisterValidator_PasswordMissingUppercase_HasCorrectErrorMessage()
    {
        var result = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@test.com", "nouppercase1", "Alice"));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterCommand.Password) &&
            e.ErrorMessage.Contains("uppercase"));
    }

    [Fact]
    public void RegisterValidator_PasswordMissingDigit_HasCorrectErrorMessage()
    {
        var result = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@test.com", "NoDigitsHere!", "Alice"));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterCommand.Password) &&
            e.ErrorMessage.Contains("digit"));
    }

    // ── ResetPasswordCommandValidator ────────────────────────────────────────

    [Fact]
    public void ResetPasswordValidator_ValidInput_PassesValidation()
    {
        var result = new ResetPasswordCommandValidator()
            .Validate(new ResetPasswordCommand("some-token", "NewPassword1!"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordValidator_EmptyToken_FailsValidation()
    {
        var result = new ResetPasswordCommandValidator()
            .Validate(new ResetPasswordCommand("", "NewPassword1!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.Token));
    }

    [Theory]
    [InlineData("nouppercase1")]  // no uppercase
    [InlineData("NoDigits!")]     // no digit
    [InlineData("Ab1!")]          // too short
    public void ResetPasswordValidator_WeakPassword_FailsValidation(string password)
    {
        var result = new ResetPasswordCommandValidator()
            .Validate(new ResetPasswordCommand("some-token", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordCommand.NewPassword));
    }
}
