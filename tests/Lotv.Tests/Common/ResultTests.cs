using Lotv.Core.Common;

namespace Lotv.Tests.Common;

public class ResultTests
{
    // ── Result<T> — Ok ────────────────────────────────────────────────────────

    [Fact]
    public void ResultT_Ok_IsSuccessTrue()
    {
        var result = Result<string>.Ok("hello");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ResultT_Ok_ValueIsSet()
    {
        var result = Result<int>.Ok(42);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ResultT_Ok_ErrorIsNull()
    {
        var result = Result<string>.Ok("hello");
        Assert.Null(result.Error);
    }

    // ── Result<T> — Fail ──────────────────────────────────────────────────────

    [Fact]
    public void ResultT_Fail_IsSuccessFalse()
    {
        var result = Result<string>.Fail("something went wrong");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ResultT_Fail_ErrorIsSet()
    {
        var result = Result<string>.Fail("not found");
        Assert.Equal("not found", result.Error);
    }

    [Fact]
    public void ResultT_Fail_ValueIsDefault()
    {
        var result = Result<int>.Fail("bad");
        Assert.Equal(default, result.Value);
    }

    // ── Non-generic Result — Ok ───────────────────────────────────────────────

    [Fact]
    public void Result_Ok_IsSuccessTrue()
    {
        var result = Result.Ok();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Result_Ok_ErrorIsNull()
    {
        var result = Result.Ok();
        Assert.Null(result.Error);
    }

    // ── Non-generic Result — Fail ─────────────────────────────────────────────

    [Fact]
    public void Result_Fail_IsSuccessFalse()
    {
        var result = Result.Fail("database unavailable");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Result_Fail_ErrorIsSet()
    {
        var result = Result.Fail("unauthorized");
        Assert.Equal("unauthorized", result.Error);
    }

    // ── Reference type value ──────────────────────────────────────────────────

    [Fact]
    public void ResultT_Ok_WithReferenceType_ValueIsSet()
    {
        var obj    = new object();
        var result = Result<object>.Ok(obj);

        Assert.Same(obj, result.Value);
    }
}
