using SliceForge.Runtime.DependencyInjection;
using SliceForge.Sample.Api.Features.Todos;
using SliceForge.Sample.Api.Features.Todos.Complete;
using SliceForge.Sample.Api.Features.Todos.Create;
using SliceForge.Sample.Api.Features.Todos.GetById;
using SliceForge.Sample.Api.Infrastructure;
using SliceForge.Validation.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddSliceForgeCommandHandler<CreateTodoCommand, Guid, CreateTodoHandler>();
builder.Services.AddSliceForgeCommandHandler<CompleteTodoCommand, CompleteTodoHandler>();
builder.Services.AddSliceForgeQueryHandler<GetTodoQuery, TodoResponse, GetTodoHandler>();
builder.Services.AddSliceForgeValidator<CreateTodoCommand, CreateTodoValidator>();
builder.Services.AddSliceForgeValidation();

var app = builder.Build();

app.UseExceptionHandler();

CreateTodoEndpoint.Map(app);
GetTodoEndpoint.Map(app);
CompleteTodoEndpoint.Map(app);

app.Run();

public partial class Program
{
}
