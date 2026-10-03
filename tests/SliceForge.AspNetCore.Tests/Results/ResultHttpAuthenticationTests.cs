using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SliceForge.AspNetCore.Results;
using SliceForge.Results;

namespace SliceForge.AspNetCore.Tests.Results;

public sealed class ResultHttpAuthenticationTests
{
    private const string TestScheme = "SliceForgeTest";

    [Fact]
    public async Task Unauthorized_result_executes_the_configured_authentication_challenge()
    {
        AuthenticationRecorder recorder = new();
        using ServiceProvider services = CreateServices(recorder);
        ApplicationBuilder application = new(services);
        application.Run(async context =>
        {
            IResult result = Result.Failure(CreateError(ErrorType.Unauthorized))
                .ToHttpResult(() => Microsoft.AspNetCore.Http.Results.Ok());
            await result.ExecuteAsync(context);
        });

        DefaultHttpContext context = new();
        context.RequestServices = services;
        await application.Build()(context);

        Assert.True(recorder.ChallengeExecuted);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_result_executes_the_configured_authentication_forbid()
    {
        AuthenticationRecorder recorder = new();
        using ServiceProvider services = CreateServices(recorder);
        DefaultHttpContext context = new();
        context.RequestServices = services;

        IResult result = Result<int>.Failure(CreateError(ErrorType.Forbidden))
            .ToHttpResult(_ => Microsoft.AspNetCore.Http.Results.Ok());
        await result.ExecuteAsync(context);

        Assert.True(recorder.ForbidExecuted);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static ServiceProvider CreateServices(AuthenticationRecorder recorder)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(recorder);
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultForbidScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RecordingAuthenticationHandler>(TestScheme, _ => { });

        return services.BuildServiceProvider();
    }

    private static Error CreateError(ErrorType type)
    {
        return new Error($"Test.{type}", "A safe description.", type);
    }

    private sealed class AuthenticationRecorder
    {
        public bool ChallengeExecuted { get; set; }

        public bool ForbidExecuted { get; set; }
    }

    private sealed class RecordingAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly AuthenticationRecorder _recorder;

        public RecordingAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            AuthenticationRecorder recorder)
            : base(options, logger, encoder)
        {
            _recorder = recorder;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            _recorder.ChallengeExecuted = true;
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            _recorder.ForbidExecuted = true;
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
