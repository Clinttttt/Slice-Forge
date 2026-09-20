using SliceForge.Results;

namespace SliceForge.Core.Tests.Results;

public sealed class ResultOfTTests
{
    [Fact]
    public void Success_should_preserve_value()
    {
        Result<int> result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Success_should_allow_null_when_T_is_nullable()
    {
        Result<string?> result = Result<string?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Failure_should_preserve_error_state()
    {
        Error error = new("Sample.NotFound", "The sample was not found.", ErrorType.NotFound);

        Result<int> result = Result<int>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Sample.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Failure_should_reject_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void Value_should_throw_for_failed_result()
    {
        Result<int> result = Result<int>.Failure(
            new Error("Sample.Failure", "The operation failed.", ErrorType.Failure));

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
