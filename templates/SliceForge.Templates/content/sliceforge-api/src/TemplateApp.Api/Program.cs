using TemplateApp.Api.Features.Examples.GetExample;
using SliceForge.Observability.DependencyInjection;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Validation.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSliceForgeQueryHandler<GetExampleQuery, ExampleResponse, GetExampleHandler>();
builder.Services.AddSliceForgeValidation();
builder.Services.AddSliceForgeObservability();

var app = builder.Build();

app.UseExceptionHandler();

GetExampleEndpoint.Map(app);

app.Run();

public partial class Program
{
}
