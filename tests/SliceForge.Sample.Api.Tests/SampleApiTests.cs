using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SliceForge.Sample.Api.Tests;

public sealed class SampleApiTests
{
    [Fact]
    public async Task Valid_create_request_returns_created_response_with_location_and_id()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/todos",
            new { title = "Buy milk" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        Assert.Equal($"/todos/{id}", response.Headers.Location?.OriginalString);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Invalid_create_request_returns_validation_problem_for_title()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/todos",
            new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        JsonElement root = body.RootElement;

        Assert.Equal((int)HttpStatusCode.BadRequest, root.GetProperty("status").GetInt32());
        Assert.True(root.GetProperty("errors").TryGetProperty("Title", out JsonElement titleErrors));
        Assert.NotEmpty(titleErrors.EnumerateArray());
    }

    [Fact]
    public async Task Created_todo_can_be_retrieved_with_its_details()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        const string title = "Read a book";
        Guid id = await CreateTodoAsync(client, title);

        using HttpResponseMessage response = await client.GetAsync($"/todos/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        JsonElement todo = body.RootElement;

        Assert.Equal(id, todo.GetProperty("id").GetGuid());
        Assert.Equal(title, todo.GetProperty("title").GetString());
        Assert.False(todo.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task Missing_todo_returns_not_found_with_expected_error_code()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/todos/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        Assert.Equal("Todos.NotFound", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Completing_an_open_todo_returns_no_content()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        Guid id = await CreateTodoAsync(client, "Walk the dog");

        using HttpResponseMessage response = await client.PutAsync(
            $"/todos/{id}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Completing_a_todo_twice_returns_conflict_with_expected_error_code()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        Guid id = await CreateTodoAsync(client, "Water the plants");

        using HttpResponseMessage firstResponse = await client.PutAsync(
            $"/todos/{id}/complete",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        using HttpResponseMessage secondResponse = await client.PutAsync(
            $"/todos/{id}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await secondResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Todos.AlreadyCompleted", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_completion_requests_produce_one_success_and_one_conflict()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        Guid id = await CreateTodoAsync(client, "Send a postcard");

        Task<HttpResponseMessage> firstRequest = client.PutAsync(
            $"/todos/{id}/complete",
            content: null);
        Task<HttpResponseMessage> secondRequest = client.PutAsync(
            $"/todos/{id}/complete",
            content: null);
        HttpResponseMessage[] responses = await Task.WhenAll(firstRequest, secondRequest);

        using (responses[0])
        using (responses[1])
        {
            HttpStatusCode[] statusCodes = responses
                .Select(response => response.StatusCode)
                .Order()
                .ToArray();

            Assert.Equal(
                new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }.Order(),
                statusCodes);
        }
    }

    [Fact]
    public async Task Completing_a_missing_todo_returns_not_found_with_expected_error_code()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PutAsync(
            $"/todos/{Guid.Empty}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        Assert.Equal("Todos.NotFound", body.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateTodoAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/todos", new { title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }
}
