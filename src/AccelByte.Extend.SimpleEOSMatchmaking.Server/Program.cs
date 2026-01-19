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

            builder.Services
                .AddSingleton<IAccelByteServiceProvider, DefaultAccelByteServiceProvider>()
                .Configure<EOSConfig>(builder.Configuration.GetSection("EOS"))
                .AddSingleton<EOSSDKService>()
                .AddHostedService(sp => sp.GetRequiredService<EOSSDKService>())
                // Register matchmaking services
                .AddSingleton<IMatchPool, MatchPool>()
                .AddSingleton<ISessionCreator, EOSSessionCreator>()
                .AddSingleton<INotifier, LoggingNotifier>()
                .AddSingleton(matchMakerConfig)
                .AddSingleton<IMatchMaker, MatchMaker>()
                .AddHostedService(sp => sp.GetRequiredService<IMatchMaker>() as MatchMaker)
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
                opts.Interceptors.Add<DebugLoggerServerInterceptor>();                
            });
            builder.Services.AddGrpcReflection();

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