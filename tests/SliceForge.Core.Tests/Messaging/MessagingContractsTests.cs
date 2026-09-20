using System.Reflection;
using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Core.Tests.Messaging;

public sealed class MessagingContractsTests
{
    [Fact]
    public void Both_command_shapes_should_implement_the_common_command_marker()
    {
        Assert.True(typeof(IBaseCommand).IsAssignableFrom(typeof(DeleteUserCommand)));
        Assert.True(typeof(IBaseCommand).IsAssignableFrom(typeof(CreateUserCommand)));
        Assert.False(typeof(IBaseCommand).IsAssignableFrom(typeof(GetUserQuery)));
    }

    [Fact]
    public async Task Non_generic_command_handler_should_pass_through_success()
    {
        DeleteUserHandler handler = new(Result.Success());

        Result result = await handler.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Non_generic_command_handler_should_pass_through_failure()
    {
        Error error = new("User.Delete.Failed", "The user could not be deleted.", ErrorType.Failure);
        DeleteUserHandler handler = new(Result.Failure(error));

        Result result = await handler.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task Generic_command_handler_should_return_the_declared_result_payload()
    {
        Guid userId = Guid.NewGuid();
        CreateUserHandler handler = new(Result<Guid>.Success(userId));

        Result<Guid> result = await handler.HandleAsync(
            new CreateUserCommand("Ada"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value);
    }

    [Fact]
    public async Task Query_handler_should_pass_through_failure()
    {
        Error error = new("User.NotFound", "The user could not be found.", ErrorType.NotFound);
        GetUserHandler handler = new(Result<User>.Failure(error));

        Result<User> result = await handler.HandleAsync(
            new GetUserQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task Nullable_query_response_should_allow_a_successful_null_payload()
    {
        NullableUserQueryHandler handler = new();

        Result<string?> result = await handler.HandleAsync(
            new NullableUserQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Handlers_should_accept_and_receive_the_cancellation_token()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        DeleteUserHandler handler = new(Result.Success());

        await handler.HandleAsync(new DeleteUserCommand(Guid.NewGuid()), cancellationToken);

        Assert.Equal(cancellationToken, handler.ReceivedCancellationToken);
    }

    [Fact]
    public void Public_handler_contracts_should_have_the_approved_result_shapes()
    {
        MethodInfo commandMethod = typeof(ICommandHandler<DeleteUserCommand>)
            .GetMethod(nameof(ICommandHandler<DeleteUserCommand>.HandleAsync))!;
        MethodInfo genericCommandMethod = typeof(ICommandHandler<CreateUserCommand, Guid>)
            .GetMethod(nameof(ICommandHandler<CreateUserCommand, Guid>.HandleAsync))!;
        MethodInfo queryMethod = typeof(IQueryHandler<GetUserQuery, User>)
            .GetMethod(nameof(IQueryHandler<GetUserQuery, User>.HandleAsync))!;

        Assert.Equal(typeof(Task<Result>), commandMethod.ReturnType);
        Assert.Equal(typeof(Task<Result<Guid>>), genericCommandMethod.ReturnType);
        Assert.Equal(typeof(Task<Result<User>>), queryMethod.ReturnType);
    }

    [Fact]
    public void Generic_relationships_should_preserve_command_and_query_response_types()
    {
        Assert.Contains(
            typeof(ICommand),
            typeof(ICommandHandler<>).GetGenericArguments()[0].GetGenericParameterConstraints());

        Type[] genericCommandConstraints = typeof(ICommandHandler<,>)
            .GetGenericArguments()[0]
            .GetGenericParameterConstraints();
        Type[] queryConstraints = typeof(IQueryHandler<,>)
            .GetGenericArguments()[0]
            .GetGenericParameterConstraints();

        Assert.Contains(genericCommandConstraints, constraint =>
            constraint.IsGenericType && constraint.GetGenericTypeDefinition() == typeof(ICommand<>));
        Assert.Contains(queryConstraints, constraint =>
            constraint.IsGenericType && constraint.GetGenericTypeDefinition() == typeof(IQuery<>));
    }

    [Fact]
    public void Approved_variance_should_be_limited_to_request_type_parameters()
    {
        Assert.Equal(
            GenericParameterAttributes.Contravariant,
            typeof(ICommandHandler<>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.Contravariant,
            typeof(ICommandHandler<,>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.Contravariant,
            typeof(IQueryHandler<,>).GetGenericArguments()[0].GenericParameterAttributes);

        Assert.Equal(
            GenericParameterAttributes.None,
            typeof(ICommand<>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.None,
            typeof(ICommandHandler<,>).GetGenericArguments()[1].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.None,
            typeof(IQuery<>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.None,
            typeof(IQueryHandler<,>).GetGenericArguments()[1].GenericParameterAttributes);
    }

    private sealed record DeleteUserCommand(Guid UserId) : ICommand;

    private sealed record CreateUserCommand(string Name) : ICommand<Guid>;

    private sealed record GetUserQuery(Guid UserId) : IQuery<User>;

    private sealed record NullableUserQuery : IQuery<string?>;

    private sealed record User(Guid Id);

    private sealed class DeleteUserHandler(Result result) : ICommandHandler<DeleteUserCommand>
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }

    private sealed class CreateUserHandler(Result<Guid> result) : ICommandHandler<CreateUserCommand, Guid>
    {
        public Task<Result<Guid>> HandleAsync(
            CreateUserCommand command,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class GetUserHandler(Result<User> result) : IQueryHandler<GetUserQuery, User>
    {
        public Task<Result<User>> HandleAsync(
            GetUserQuery query,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class NullableUserQueryHandler : IQueryHandler<NullableUserQuery, string?>
    {
        public Task<Result<string?>> HandleAsync(
            NullableUserQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<string?>.Success(null));
    }
}
