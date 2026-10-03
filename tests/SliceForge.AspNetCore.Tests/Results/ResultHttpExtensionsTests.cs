using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.AspNetCore.Results;
using SliceForge.Results;

namespace SliceForge.AspNetCore.Tests.Results;

public sealed class ResultHttpExtensionsTests
{
    [Fact]
    public void ToHttpResult_successful_non_generic_result_invokes_callback_once_and_returns_its_result()
    {
        Result result = Result.Success();
        IResult expectedHttpResult = Microsoft.AspNetCore.Http.Results.NoContent();
        int callbackCount = 0;

        IResult actualHttpResult = result.ToHttpResult(() =>
        {
            callbackCount++;
            return expectedHttpResult;
        });

        Assert.Equal(1, callbackCount);
        Assert.Same(expectedHttpResult, actualHttpResult);
    }

    [Fact]
    public void ToHttpResult_successful_typed_result_passes_exact_value_and_returns_callback_result()
    {
        object value = new();
        Result<object> result = Result<object>.Success(value);
        IResult expectedHttpResult = Microsoft.AspNetCore.Http.Results.Created("/items/1", value);
        object? actualValue = null;
        int callbackCount = 0;

        IResult actualHttpResult = result.ToHttpResult(response =>
        {
            callbackCount++;
            actualValue = response;
            return expectedHttpResult;
        });

        Assert.Equal(1, callbackCount);
        Assert.Same(value, actualValue);
        Assert.Same(expectedHttpResult, actualHttpResult);
    }

    [Fact]
    public void ToHttpResult_successful_nullable_typed_result_passes_null()
    {
        Result<string?> result = Result<string?>.Success(null);
        string? actualValue = "unexpected";

        IResult actualHttpResult = result.ToHttpResult(value =>
        {
            actualValue = value;
            return Microsoft.AspNetCore.Http.Results.NoContent();
        });

        Assert.Null(actualValue);
        Assert.NotNull(actualHttpResult);
    }

    [Fact]
    public void ToHttpResult_rejects_null_non_generic_result()
    {
        Result result = null!;

        Assert.Throws<ArgumentNullException>(() => result.ToHttpResult(() => Microsoft.AspNetCore.Http.Results.Ok()));
    }

    [Fact]
    public void ToHttpResult_rejects_null_non_generic_success_callback()
    {
        Func<IResult> onSuccess = null!;

        Assert.Throws<ArgumentNullException>(() => Result.Success().ToHttpResult(onSuccess));
    }

    [Fact]
    public void ToHttpResult_rejects_null_typed_result()
    {
        Result<string> result = null!;

        Assert.Throws<ArgumentNullException>(() => result.ToHttpResult(_ => Microsoft.AspNetCore.Http.Results.Ok()));
    }

    [Fact]
    public void ToHttpResult_rejects_null_typed_success_callback()
    {
        Func<string, IResult> onSuccess = null!;

        Assert.Throws<ArgumentNullException>(() => Result<string>.Success("value").ToHttpResult(onSuccess));
    }

    [Fact]
    public void ToHttpResult_throws_when_non_generic_success_callback_returns_null()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Result.Success().ToHttpResult(() => null!));

        Assert.Contains("callback returned a null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToHttpResult_throws_when_typed_success_callback_returns_null()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Result<int>.Success(1).ToHttpResult(_ => null!));

        Assert.Contains("callback returned a null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToHttpResult_propagates_non_generic_success_callback_exception_unchanged()
    {
        InvalidOperationException expectedException = new("callback failed");

        InvalidOperationException actualException = Assert.Throws<InvalidOperationException>(
            () => Result.Success().ToHttpResult(() => throw expectedException));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public void ToHttpResult_propagates_typed_success_callback_exception_unchanged()
    {
        InvalidOperationException expectedException = new("callback failed");

        InvalidOperationException actualException = Assert.Throws<InvalidOperationException>(
            () => Result<int>.Success(1).ToHttpResult(_ => throw expectedException));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task ToHttpResult_maps_failure_to_400_problem_details_without_serializing_error_type()
    {
        int callbackCount = 0;
        IResult result = Result.Failure(CreateError(ErrorType.Failure))
            .ToHttpResult(() =>
            {
                callbackCount++;
                return Microsoft.AspNetCore.Http.Results.Ok();
            });

        (int statusCode, JsonDocument body) = await ExecuteAsync(result);
        using (body)
        {
            JsonElement root = body.RootElement;
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            Assert.Equal(StatusCodes.Status400BadRequest, root.GetProperty("status").GetInt32());
            Assert.Equal("A safe description.", root.GetProperty("detail").GetString());
            Assert.Equal("Test.Failure", root.GetProperty("code").GetString());
            Assert.False(root.TryGetProperty("errorType", out _));
            Assert.Equal(0, callbackCount);
        }
    }

    [Fact]
    public async Task ToHttpResult_maps_validation_to_400_with_grouped_deduplicated_errors_and_object_key()
    {
        Error error = new(
            "Test.Validation",
            "The request is invalid.",
            ErrorType.Validation,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["Name"] = new[] { "Name is required.", "Name is required.", "Name is too short." },
                [string.Empty] = new[] { "The request is invalid as a whole.", "The request is invalid as a whole." }
            });

        (int statusCode, JsonDocument body) = await ExecuteAsync(Result<int>.Failure(error).ToHttpResult(_ =>
            Microsoft.AspNetCore.Http.Results.Ok()));
        using (body)
        {
            JsonElement root = body.RootElement;
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            Assert.Equal(StatusCodes.Status400BadRequest, root.GetProperty("status").GetInt32());
            Assert.Equal("The request is invalid.", root.GetProperty("detail").GetString());
            Assert.Equal("Test.Validation", root.GetProperty("code").GetString());

            JsonElement errors = root.GetProperty("errors");
            Assert.Equal(
                new[] { "Name is required.", "Name is too short." },
                errors.GetProperty("Name").EnumerateArray().Select(message => message.GetString()));
            Assert.Equal(
                new[] { "The request is invalid as a whole." },
                errors.GetProperty(string.Empty).EnumerateArray().Select(message => message.GetString()));
        }
    }

    [Fact]
    public async Task ToHttpResult_maps_not_found_to_404_problem_details()
    {
        (int statusCode, JsonDocument body) = await ExecuteAsync(
            Result.Failure(CreateError(ErrorType.NotFound)).ToHttpResult(() =>
                Microsoft.AspNetCore.Http.Results.Ok()));
        using (body)
        {
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            Assert.Equal("Test.NotFound", body.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task ToHttpResult_maps_conflict_to_409_problem_details()
    {
        (int statusCode, JsonDocument body) = await ExecuteAsync(
            Result<int>.Failure(CreateError(ErrorType.Conflict)).ToHttpResult(_ =>
                Microsoft.AspNetCore.Http.Results.Ok()));
        using (body)
        {
            Assert.Equal(StatusCodes.Status409Conflict, statusCode);
            Assert.Equal("Test.Conflict", body.RootElement.GetProperty("code").GetString());
        }
    }

    private static Error CreateError(ErrorType type)
    {
        return new Error($"Test.{type}", "A safe description.", type);
    }

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IResult result)
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging();
        serviceCollection.AddProblemDetails();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        DefaultHttpContext context = new();
        context.RequestServices = services;
        using MemoryStream responseBody = new();
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        responseBody.Position = 0;
        JsonDocument body = await JsonDocument.ParseAsync(responseBody);
        return (context.Response.StatusCode, body);
    }
}
