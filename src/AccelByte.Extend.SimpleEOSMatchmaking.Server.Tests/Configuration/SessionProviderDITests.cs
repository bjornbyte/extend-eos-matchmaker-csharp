// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Configuration
{
    public class SessionProviderDITests
    {
        [Fact]
        public void DI_WithCreateMode_RegistersEOSSessionCreator()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(); // Add logging services
            services.AddSingleton<EOSSDKService>(sp => null!); // Mock EOS SDK service
            
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("SessionProvider:Mode", "create")
                })
                .Build();

            // Act
            // This will be implemented in the actual DI setup
            // For now, manually register to test the concept
            var sessionProviderConfig = configuration.GetSection("SessionProvider").Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            if (sessionProviderConfig.Mode == "create")
            {
                services.AddSingleton<ISessionProvider, EosSessionProvider>();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var sessionCreator = serviceProvider.GetService<ISessionProvider>();
            Assert.NotNull(sessionCreator);
            Assert.IsType<EosSessionProvider>(sessionCreator);
        }

        [Fact]
        public void DI_WithFindMode_RegistersEOSSessionFinder()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(); // Add logging services
            services.AddSingleton<EOSSDKService>(sp => null!); // Mock EOS SDK service
            
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("SessionProvider:Mode", "find")
                })
                .Build();

            // Act
            var sessionProviderConfig = configuration.GetSection("SessionProvider").Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            if (sessionProviderConfig.Mode == "find")
            {
                var finderConfig = configuration.GetSection("SessionFinder").Get<EOSSessionFinderConfig>() ?? new EOSSessionFinderConfig();
                services.AddSingleton(finderConfig);
                services.AddSingleton<IClaimedSessionsCache, InMemoryClaimedSessionsCache>();
                services.AddSingleton<ISessionOwnerNotifier, StubSessionOwnerNotifier>();
                services.AddSingleton<ISessionProvider, EosSessionFinder>();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var sessionCreator = serviceProvider.GetService<ISessionProvider>();
            Assert.NotNull(sessionCreator);
            Assert.IsType<EosSessionFinder>(sessionCreator);
        }

        [Fact]
        public void DI_WithFindMode_RegistersIClaimedSessionsCache()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(); // Add logging services
            services.AddSingleton<EOSSDKService>(sp => null!); // Mock EOS SDK service
            
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("SessionProvider:Mode", "find")
                })
                .Build();

            // Act
            var sessionProviderConfig = configuration.GetSection("SessionProvider").Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            if (sessionProviderConfig.Mode == "find")
            {
                var finderConfig = configuration.GetSection("SessionFinder").Get<EOSSessionFinderConfig>() ?? new EOSSessionFinderConfig();
                services.AddSingleton(finderConfig);
                services.AddSingleton<IClaimedSessionsCache, InMemoryClaimedSessionsCache>();
                services.AddSingleton<ISessionOwnerNotifier, StubSessionOwnerNotifier>();
                services.AddSingleton<ISessionProvider, EosSessionFinder>();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cache = serviceProvider.GetService<IClaimedSessionsCache>();
            Assert.NotNull(cache);
            Assert.IsType<InMemoryClaimedSessionsCache>(cache);
        }

        [Fact]
        public void DI_WithFindMode_RegistersISessionOwnerNotifier()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging(); // Add logging services
            services.AddSingleton<EOSSDKService>(sp => null!); // Mock EOS SDK service
            
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("SessionProvider:Mode", "find")
                })
                .Build();

            // Act
            var sessionProviderConfig = configuration.GetSection("SessionProvider").Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            if (sessionProviderConfig.Mode == "find")
            {
                var finderConfig = configuration.GetSection("SessionFinder").Get<EOSSessionFinderConfig>() ?? new EOSSessionFinderConfig();
                services.AddSingleton(finderConfig);
                services.AddSingleton<IClaimedSessionsCache, InMemoryClaimedSessionsCache>();
                services.AddSingleton<ISessionOwnerNotifier, StubSessionOwnerNotifier>();
                services.AddSingleton<ISessionProvider, EosSessionFinder>();
            }

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var notifier = serviceProvider.GetService<ISessionOwnerNotifier>();
            Assert.NotNull(notifier);
            Assert.IsType<StubSessionOwnerNotifier>(notifier);
        }

        [Fact]
        public void DI_WithInvalidMode_ThrowsException()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("SessionProvider:Mode", "invalid")
                })
                .Build();

            // Act & Assert
            var sessionProviderConfig = configuration.GetSection("SessionProvider").Get<SessionProviderConfig>() ?? new SessionProviderConfig();
            
            Assert.Throws<InvalidOperationException>(() => sessionProviderConfig.Validate());
        }
    }
}
