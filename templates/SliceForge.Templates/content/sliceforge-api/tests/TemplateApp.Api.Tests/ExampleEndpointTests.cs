using System.Net;
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
}
