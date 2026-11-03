using TemplateApp.Api;
using TemplateApp.Api.Features.Examples.CreateGreeting;
using TemplateApp.Api.Features.Examples.GetExample;
using SliceForge.Observability.DependencyInjection;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Validation.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var applicationAssembly = typeof(ApplicationAssemblyMarker).Assembly;

builder.Services.AddProblemDetails();
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSliceForgeHandlersFromAssembly(applicationAssembly);
builder.Services.AddSliceForgeValidatorsFromAssembly(applicationAssembly);
builder.Services.AddSliceForgeValidation();
builder.Services.AddSliceForgeObservability();

var app = builder.Build();

app.UseExceptionHandler();

GetExampleEndpoint.Map(app);
CreateGreetingEndpoint.Map(app);

app.Run();

public partial class Program
{
}
