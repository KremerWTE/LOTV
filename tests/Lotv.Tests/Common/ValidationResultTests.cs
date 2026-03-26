using Lotv.Core.Common;

namespace Lotv.Tests.Common;

public class ValidationResultTests
{
    // ── Success factory ───────────────────────────────────────────────────────

    [Fact]
    public void Success_IsValid()
    {
        var result = ValidationResult.Success();
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Success_HasNoErrors()
    {
        var result = ValidationResult.Success();
        Assert.Empty(result.Errors);
    }

    // ── AddError ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddError_MakesResultInvalid()
    {
        var result = new ValidationResult();
        result.AddError("Email", "Email is required");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AddError_AppearsInErrorsList()
    {
        var result = new ValidationResult();
        result.AddError("Email", "Email is required");

        Assert.Single(result.Errors);
        Assert.Equal("Email", result.Errors[0].Field);
        Assert.Equal("Email is required", result.Errors[0].Message);
    }

    [Fact]
    public void AddError_MultipleErrors_AllAppear()
    {
        var result = new ValidationResult();
        result.AddError("Email",    "Email is required");
        result.AddError("Password", "Password too short");
        result.AddError("Phone",    "Invalid phone format");

        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void AddError_MultipleErrors_AllFieldsPreserved()
    {
        var result = new ValidationResult();
        result.AddError("FirstName", "Required");
        result.AddError("LastName",  "Required");

        var fields = result.Errors.Select(e => e.Field).ToList();
        Assert.Contains("FirstName", fields);
        Assert.Contains("LastName", fields);
    }

    // ── Default constructor (no errors) ───────────────────────────────────────

    [Fact]
    public void DefaultConstructor_IsValid()
    {
        var result = new ValidationResult();
        Assert.True(result.IsValid);
    }

    // ── Errors is read-only ───────────────────────────────────────────────────

    [Fact]
    public void Errors_IsReadOnlyList()
    {
        var result = new ValidationResult();
        // IReadOnlyList<ValidationError> — confirms type
        Assert.IsAssignableFrom<IReadOnlyList<ValidationError>>(result.Errors);
    }

    // ── ValidationError record ────────────────────────────────────────────────

    [Fact]
    public void ValidationError_EqualityByValue()
    {
        var a = new ValidationError("Email", "Required");
        var b = new ValidationError("Email", "Required");

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValidationError_InequalityOnMessage()
    {
        var a = new ValidationError("Email", "Required");
        var b = new ValidationError("Email", "Invalid format");

        Assert.NotEqual(a, b);
    }
}
