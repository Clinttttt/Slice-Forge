using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Runtime.Tests.Messaging;

public sealed class MessageSenderTests
{
    [Fact]
    public async Task Non_generic_command_dispatches_and_propagates_success()
    {
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<DeleteUserCommand, DeleteUserHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            Result result = await sender.SendCommandAsync(new DeleteUserCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
        }
    }

    [Fact]
    public async Task Typed_command_dispatches_and_preserves_result_shape()
    {
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<CreateUserCommand, Guid, CreateUserHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            Result<Guid> result = await sender.SendCommandAsync<Guid>(
                new CreateUserCommand(),
                CancellationToken.None);

            Assert.Equal(typeof(Result<Guid>), result.GetType());
            Assert.Equal(CreateUserHandler.UserId, result.Value);
        }
    }

    [Fact]
    public async Task Query_dispatches_and_propagates_success()
    {
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeQueryHandler<GetUserQuery, string, GetUserHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            Result<string> result = await sender.SendQueryAsync(
                new GetUserQuery(),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Ada", result.Value);
        }
    }

    [Fact]
    public async Task Failed_result_is_returned_without_conversion()
    {
        Error error = new("users.invalid", "The user is invalid.", ErrorType.Validation);
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<FailingCommand, FailingCommandHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            Result result = await sender.SendCommandAsync(new FailingCommand(error), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Same(error, result.Error);
        }
    }

    [Fact]
    public async Task Nullable_typed_success_is_preserved()
    {
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeQueryHandler<NullableQuery, string?, NullableQueryHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            Result<string?> result = await sender.SendQueryAsync<string?>(
                new NullableQuery(),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }
    }

    [Fact]
    public async Task Cancellation_token_is_propagated_to_handler()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<CancellableCommand, CancellableHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await sender.SendCommandAsync(new CancellableCommand(), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, CancellableHandler.ReceivedToken);
        }
    }

    [Fact]
    public async Task Unregistered_message_throws_invalid_operation_exception()
    {
        ServiceProvider provider = CreateProvider();

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendCommandAsync(new DeleteUserCommand(), CancellationToken.None));
        }
    }

    [Fact]
    public void Duplicate_route_registration_throws_clear_configuration_exception()
    {
        ServiceCollection services = new();

        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<DeleteUserCommand, DeleteUserHandler>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeCommandHandler<DeleteUserCommand, DeleteUserHandler>());

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_handlers_throws_invalid_operation_exception()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<DeleteUserCommand, DeleteUserHandler>();
        services.RemoveAll<ICommandHandler<DeleteUserCommand>>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new DeleteUserCommand(), CancellationToken.None));

        Assert.Contains("No handler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Multiple_handlers_throws_invalid_operation_exception()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<DeleteUserCommand, DeleteUserHandler>();
        services.AddTransient<ICommandHandler<DeleteUserCommand>, SecondDeleteUserHandler>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new DeleteUserCommand(), CancellationToken.None));

        Assert.Contains("Multiple handlers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Derived_runtime_command_requires_exact_concrete_route()
    {
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<BaseCommand, BaseCommandHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendCommandAsync(new DerivedCommand(), CancellationToken.None));
        }
    }

    [Fact]
    public void Command_and_query_route_collision_throws_configuration_exception()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<CollidingMessage, CollidingCommandHandler>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeQueryHandler<CollidingMessage, string, CollidingQueryHandler>());

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_executes_exactly_once()
    {
        ExactlyOnceHandler.ExecutionCount = 0;
        ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeCommandHandler<ExactlyOnceCommand, ExactlyOnceHandler>());

        using (provider)
        {
            using IServiceScope scope = provider.CreateScope();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await sender.SendCommandAsync(new ExactlyOnceCommand(), CancellationToken.None);
        }

        Assert.Equal(1, ExactlyOnceHandler.ExecutionCount);
    }

    private static ServiceProvider CreateProvider(
        Action<ServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
}

public sealed record DeleteUserCommand : ICommand;

public sealed class DeleteUserHandler : ICommandHandler<DeleteUserCommand>
{
    public Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class SecondDeleteUserHandler : ICommandHandler<DeleteUserCommand>
{
    public Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed record CreateUserCommand : ICommand<Guid>;

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, Guid>
{
    public static readonly Guid UserId = Guid.Parse("7f7d9e6a-3ecf-44d2-9cc6-6db2c9d184db");

    public Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result<Guid>.Success(UserId));
}

public sealed record GetUserQuery : IQuery<string>;

public sealed class GetUserHandler : IQueryHandler<GetUserQuery, string>
{
    public Task<Result<string>> HandleAsync(GetUserQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string>.Success("Ada"));
}

public sealed record FailingCommand(Error Error) : ICommand;

public sealed class FailingCommandHandler : ICommandHandler<FailingCommand>
{
    public Task<Result> HandleAsync(FailingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(command.Error));
}

public sealed record NullableQuery : IQuery<string?>;

public sealed class NullableQueryHandler : IQueryHandler<NullableQuery, string?>
{
    public Task<Result<string?>> HandleAsync(NullableQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string?>.Success(null));
}

public sealed record CancellableCommand : ICommand;

public sealed class CancellableHandler : ICommandHandler<CancellableCommand>
{
    public static CancellationToken ReceivedToken { get; private set; }

    public Task<Result> HandleAsync(CancellableCommand command, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        return Task.FromResult(Result.Success());
    }
}

public class BaseCommand : ICommand;

public sealed class DerivedCommand : BaseCommand;

public sealed class BaseCommandHandler : ICommandHandler<BaseCommand>
{
    public Task<Result> HandleAsync(BaseCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class CollidingMessage : ICommand, IQuery<string>;

public sealed class CollidingCommandHandler : ICommandHandler<CollidingMessage>
{
    public Task<Result> HandleAsync(CollidingMessage command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class CollidingQueryHandler : IQueryHandler<CollidingMessage, string>
{
    public Task<Result<string>> HandleAsync(CollidingMessage query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string>.Success("collision"));
}

public sealed record ExactlyOnceCommand : ICommand;

public sealed class ExactlyOnceHandler : ICommandHandler<ExactlyOnceCommand>
{
    public static int ExecutionCount { get; set; }

    public Task<Result> HandleAsync(ExactlyOnceCommand command, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        return Task.FromResult(Result.Success());
    }
}
