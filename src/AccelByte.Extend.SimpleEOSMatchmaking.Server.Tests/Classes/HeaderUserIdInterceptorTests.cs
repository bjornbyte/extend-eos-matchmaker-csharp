// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Classes;

public class HeaderUserIdInterceptorTests
{
    private readonly Mock<ILogger<HeaderUserIdInterceptor>> _mockLogger;
    private readonly HeaderUserIdInterceptor _interceptor;

    public HeaderUserIdInterceptorTests()
    {
        _mockLogger = new Mock<ILogger<HeaderUserIdInterceptor>>();
        _interceptor = new HeaderUserIdInterceptor(_mockLogger.Object);
    }

    [Fact]
    public async Task UnaryServerHandler_WithUserIdHeader_SetsUserStateCorrectly()
    {
        // Arrange
        var testUserId = "test-user-123";
        var requestHeaders = new Metadata
        {
            { GrpcConstants.UserIdKey, testUserId }
        };
        
        var mockContext = new TestServerCallContext(requestHeaders);
        var request = new object();
        var expectedResponse = new object();
        
        UnaryServerMethod<object, object> continuation = (req, ctx) =>
        {
            // Verify user-id was added to UserState
            Assert.True(ctx.UserState.ContainsKey(GrpcConstants.UserIdKey));
            Assert.Equal(testUserId, ctx.UserState[GrpcConstants.UserIdKey]);
            return Task.FromResult(expectedResponse);
        };

        // Act
        var response = await _interceptor.UnaryServerHandler(request, mockContext, continuation);

        // Assert
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task UnaryServerHandler_WithoutUserIdHeader_ThrowsUnauthenticated()
    {
        // Arrange
        var requestHeaders = new Metadata(); // No user-id header
        var mockContext = new TestServerCallContext(requestHeaders);
        var request = new object();
        
        UnaryServerMethod<object, object> continuation = (req, ctx) =>
        {
            return Task.FromResult(new object());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => _interceptor.UnaryServerHandler(request, mockContext, continuation));
        
        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        Assert.Contains("user-id", exception.Status.Detail.ToLower());
    }

    [Fact]
    public async Task UnaryServerHandler_WithEmptyUserIdHeader_ThrowsUnauthenticated()
    {
        // Arrange
        var requestHeaders = new Metadata
        {
            { GrpcConstants.UserIdKey, "" }
        };
        var mockContext = new TestServerCallContext(requestHeaders);
        var request = new object();
        
        UnaryServerMethod<object, object> continuation = (req, ctx) =>
        {
            return Task.FromResult(new object());
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => _interceptor.UnaryServerHandler(request, mockContext, continuation));
        
        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        Assert.Contains("user-id", exception.Status.Detail.ToLower());
    }

    // Helper class to create a testable ServerCallContext
    private class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;

        public TestServerCallContext(Metadata requestHeaders)
        {
            _requestHeaders = requestHeaders;
        }

        protected override string MethodCore => "/TestService/TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new Metadata();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new AuthContext(string.Empty, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        {
            throw new NotImplementedException();
        }

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            return Task.CompletedTask;
        }
    }
}
