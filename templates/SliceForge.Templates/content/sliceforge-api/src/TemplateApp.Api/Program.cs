#if (UseObservability)
using SliceForge.Observability.DependencyInjection;
#endif
using SliceForge.Runtime.DependencyInjection;
#if (UseValidation)
using SliceForge.Validation.DependencyInjection;
#endif

var builder = WebApplication.CreateBuilder(args);
var applicationAssembly = typeof(TemplateApp.Api.ApplicationAssemblyMarker).Assembly;

builder.Services.AddProblemDetails();
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSliceForgeHandlersFromAssembly(applicationAssembly);
#if (UseValidation)
builder.Services.AddSliceForgeValidatorsFromAssembly(applicationAssembly);
builder.Services.AddSliceForgeValidation();
#endif
#if (UseObservability)
builder.Services.AddSliceForgeObservability();
#endif

var app = builder.Build();

app.UseExceptionHandler();

TemplateApp.Api.Features.Examples.GetExample.GetExampleEndpoint.Map(app);
TemplateApp.Api.Features.Examples.CreateGreeting.CreateGreetingEndpoint.Map(app);

app.Run();

public partial class Program
{
}
