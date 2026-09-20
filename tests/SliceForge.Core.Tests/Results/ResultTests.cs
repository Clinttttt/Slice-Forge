using SliceForge.Results;

namespace SliceForge.Core.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_should_have_no_error()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_should_preserve_error_state()
    {
        Error error = new("Sample.Failure", "The operation failed.", ErrorType.Failure);

        Result result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Sample.Failure", result.Error.Code);
        Assert.Equal(ErrorType.Failure, result.Error.Type);
    }

    [Fact]
    public void Failure_should_reject_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void Result_of_T_should_be_assignable_to_Result()
    {
        Result result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }
}
