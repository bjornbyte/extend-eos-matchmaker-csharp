// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Extensions.Propagators;

using Prometheus;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // Load .env file if it exists - try multiple locations
            string[] possibleEnvPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(AppContext.BaseDirectory, ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env")
            };

            foreach (var envPath in possibleEnvPaths)
            {
                if (File.Exists(envPath))
                {
                    DotNetEnv.Env.Load(envPath);
                    Console.WriteLine($"Loaded .env file from: {envPath}");
                    break;
                }
            }

            OpenTelemetry.Sdk.SetDefaultTextMapPropagator(new B3Propagator());

            string? appServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
            if (appServiceName == null)
                appServiceName = "extend-app-service-extension";
            else
                appServiceName = $"extend-app-{appServiceName.Trim().ToLower()}";

            Metrics.DefaultRegistry.SetStaticLabels(new Dictionary<string, string>()
            {
                { "application", appServiceName }
            });

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables("ABSERVER_");
            builder.WebHost.ConfigureKestrel(opt =>
            {
                opt.AllowAlternateSchemes = true;
            });

            string? appResourceName = Environment.GetEnvironmentVariable("APP_RESOURCE_NAME");
            if (appResourceName == null)
                appResourceName = "SERVICEEXTENSIONEXTENDAPP";

            bool enableAuthorization = builder.Configuration.GetValue<bool>("EnableAuthorization");
            string? strEnableAuth = Environment.GetEnvironmentVariable("PLUGIN_GRPC_SERVER_AUTH_ENABLED");
            if ((strEnableAuth != null) && (strEnableAuth != String.Empty))
                enableAuthorization = (strEnableAuth.Trim().ToLower() == "true");

            // Configure MatchMaker settings
            var matchMakerConfig = new MatchMakerConfig();
            var matchMakerSection = builder.Configuration.GetSection("MatchMaker");
            matchMakerConfig.MatchSize = matchMakerSection.GetValue<int>("MatchSize", 2);
            matchMakerConfig.TickInterval = TimeSpan.FromSeconds(matchMakerSection.GetValue<int>("TickIntervalSeconds", 1));
            matchMakerConfig.RequestTimeout = TimeSpan.FromSeconds(matchMakerSection.GetValue<int>("RequestTimeoutSeconds", 60));
            matchMakerConfig.RetentionPeriod = TimeSpan.FromSeconds(matchMakerSection.GetValue<int>("RetentionPeriodSeconds", 120));

            // Configure and validate session provider mode
            var sessionProviderConfig = builder.Configuration
                .GetSection("SessionProvider")
                .Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            sessionProviderConfig.Validate();
            
            Console.WriteLine($"Session provider mode: {sessionProviderConfig.Mode}");

            builder.Services
                .AddSingleton<IAccelByteServiceProvider, DefaultAccelByteServiceProvider>()
                .Configure<EOSConfig>(builder.Configuration.GetSection("EOS"))
                .AddSingleton<EOSSDKService>()
                .AddHostedService(sp => sp.GetRequiredService<EOSSDKService>())
                
                // INFRASTRUCTURE EXTENSION POINT: IMatchPool
                // Default: In-memory implementation (MatchPool) - suitable for single-instance deployments only.
                // For multi-instance deployments, replace with distributed implementation (Redis, database).
                // See docs/architecture.md#extension-points for examples.
                .AddSingleton<IMatchPool, MatchPool>()
                
                // INFRASTRUCTURE EXTENSION POINT: ICompletedRequestStore
                // Default: In-memory implementation (CompletedRequestStore) - data lost on service restart.
                // For durability across restarts, replace with persistent implementation (database).
                // See docs/architecture.md#extension-points for examples.
                .AddSingleton<ICompletedRequestStore, CompletedRequestStore>();

            // Register session provider based on configuration
            if (sessionProviderConfig.Mode == "create")
            {
                // APPLICATION EXTENSION POINT: ISessionCreator (Create Mode)
                // Default: EOSSessionCreator - creates new EOS sessions for each match.
                // Customize this if you need different session creation logic or use a different backend.
                // See docs/architecture.md#session-provider-modes for mode selection guidance.
                builder.Services.AddSingleton<ISessionCreator, EOSSessionCreator>();
            }
            else // "find"
            {
                var finderConfig = builder.Configuration
                    .GetSection("SessionFinder")
                    .Get<EOSSessionFinderConfig>() ?? new EOSSessionFinderConfig();
                
                builder.Services.AddSingleton(finderConfig);
                
                // INFRASTRUCTURE EXTENSION POINT: IClaimedSessionsCache (Find Mode Only)
                // Default: In-memory implementation - suitable for single-instance deployments only.
                // For multi-instance deployments, replace with distributed cache (Redis).
                // See docs/architecture.md#extension-points for examples.
                builder.Services.AddSingleton<IClaimedSessionsCache, InMemoryClaimedSessionsCache>();
                
                // APPLICATION EXTENSION POINT: ISessionOwnerNotifier (Find Mode Only)
                // Default: StubSessionOwnerNotifier - does nothing (TODO: implement your notification mechanism).
                // Replace with your game server notification implementation (HTTP, gRPC, message queue).
                // See docs/architecture.md#extension-points for examples.
                builder.Services.AddSingleton<ISessionOwnerNotifier, StubSessionOwnerNotifier>();
                
                // APPLICATION EXTENSION POINT: ISessionCreator (Find Mode)
                // Default: EOSSessionFinder - finds and claims existing EOS sessions.
                // Customize this if you need different session finding logic or use a different backend.
                // See docs/architecture.md#session-provider-modes for mode selection guidance.
                builder.Services.AddSingleton<ISessionCreator, EOSSessionFinder>();
            }

            builder.Services
                // APPLICATION EXTENSION POINT: IPlayerNotifier
                // Default: LoggingPlayerNotifier - only logs match notifications to console (TODO: implement your notification mechanism).
                // Replace with your game-specific notification implementation (webhook, push notification, message queue).
                // See docs/architecture.md#extension-points for examples.
                .AddSingleton<IPlayerNotifier, LoggingPlayerNotifier>()
                .AddSingleton(matchMakerConfig)
                .AddSingleton<MatchMaker>()
                .AddHostedService<MatchMaker>()
                .AddOpenTelemetry()
                .WithTracing((traceConfig) =>
                {
                    var asVersion = Assembly.GetEntryAssembly()!.GetName().Version;
                    string version = "0.0.0";
                    if (asVersion != null)
                        version = asVersion.ToString();

                    traceConfig
                        .AddSource(appResourceName)
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(appServiceName, null, version)
                            .AddTelemetrySdk())
                        .AddZipkinExporter()
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation();
                });

            // Additional configuration is required to successfully run gRPC on macOS.
            // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

            builder.Services.AddGrpcHealthChecks()
                .AddCheck("Health", () => HealthCheckResult.Healthy());

            builder.Services.AddGrpc((opts) =>
            {
                opts.Interceptors.Add<ExceptionHandlingInterceptor>();
                if (enableAuthorization)
                    opts.Interceptors.Add<AuthorizationInterceptor>();
                else
                    opts.Interceptors.Add<HeaderUserIdInterceptor>();
                opts.Interceptors.Add<DebugLoggerServerInterceptor>();                
            });
            builder.Services.AddGrpcReflection();
            
            // Register MatchmakingService with completed store
            builder.Services.AddSingleton<MatchmakingService>();

            var app = builder.Build();
            app.UseGrpcMetrics();

            app.MapGrpcReflectionService();
            app.MapGrpcHealthChecksService();
            app.MapGrpcService<MatchmakingService>();
            app.MapMetrics();
            app.Run();
            return 0;
        }
    }
}