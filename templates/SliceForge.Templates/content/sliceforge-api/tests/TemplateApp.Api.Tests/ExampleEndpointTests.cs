using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TemplateApp.Api.Tests;

public sealed class ExampleEndpointTests
{
    [Fact]
    public async Task Get_example_returns_the_vertical_slice_response()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/examples");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("SliceForge is ready.", body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Post_greeting_returns_the_greeting_response()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/examples/greetings",
            new { name = "Ada" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hello, Ada!", body.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_greeting_returns_validation_problem_for_a_missing_name(string name)
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/examples/greetings",
            new { name });

        await AssertValidationProblem(response, "Name is required.");
    }

    [Fact]
    public async Task Post_greeting_returns_validation_problem_for_an_excessively_long_name()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/examples/greetings",
            new { name = new string('A', 101) });

        await AssertValidationProblem(response, "Name must be 100 characters or fewer.");
    }

    private static async Task AssertValidationProblem(
        HttpResponseMessage response,
        string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Validation.Failed", body.RootElement.GetProperty("code").GetString());
        Assert.Contains(
            body.RootElement.GetProperty("errors").GetProperty("Name").EnumerateArray(),
            error => error.GetString() == expectedMessage);
    }
}
