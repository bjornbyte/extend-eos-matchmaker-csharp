using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Grpc.Core;
using Moq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Logging;
using ModelMatchRequestStatus = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class MatchmakingServiceTests
    {
        private readonly Mock<IMatchPool> _mockMatchPool;
        private readonly Mock<ILogger<MatchmakingService>> _mockLogger;
        private readonly MatchmakingService _service;

        public MatchmakingServiceTests()
        {
            _mockMatchPool = new Mock<IMatchPool>();
            _mockLogger = new Mock<ILogger<MatchmakingService>>();
            _service = new MatchmakingService(_mockMatchPool.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SubmitMatchRequest_WithValidRequest_ReturnsRequestId()
        {
            // Arrange
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.SubmitMatchRequestRequest
            {
                Metadata = { { "key", "value" } }
            };
            
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.GetByUserId("user123")).Returns((MatchRequest?)null);

            // Act
            var response = await _service.SubmitMatchRequest(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.RequestId);
            _mockMatchPool.Verify(p => p.Add(It.IsAny<MatchRequest>()), Times.Once);
        }

        [Fact]
        public async Task SubmitMatchRequest_WithDuplicateUser_ThrowsRpcException()
        {
            // Arrange
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.SubmitMatchRequestRequest();
            var context = CreateMockContext("user123");
            
            var existingRequest = new MatchRequest("user123");
            _mockMatchPool.Setup(p => p.GetByUserId("user123")).Returns(existingRequest);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RpcException>(
                () => _service.SubmitMatchRequest(request, context));
            
            Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.Contains(existingRequest.RequestId, exception.Status.Detail);
        }

        [Fact]
        public async Task GetMatchStatus_WithValidPendingRequest_ReturnsStatus()
        {
            // Arrange
            var matchRequest = new MatchRequest("user123");
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusRequest
            {
                RequestId = matchRequest.RequestId
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get(matchRequest.RequestId)).Returns(matchRequest);

            // Act
            var response = await _service.GetMatchStatus(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(matchRequest.RequestId, response.RequestId);
            Assert.Equal(AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Pending, response.Status);
            Assert.Empty(response.SessionId);
        }

        [Fact]
        public async Task GetMatchStatus_WithMatchedRequest_ReturnsSessionDetails()
        {
            // Arrange
            var matchRequest = new MatchRequest("user123");
            matchRequest.Status = ModelMatchRequestStatus.Matched;
            matchRequest.SessionId = "session123";
            matchRequest.MatchedAt = DateTime.UtcNow;
            
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusRequest
            {
                RequestId = matchRequest.RequestId
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get(matchRequest.RequestId)).Returns(matchRequest);

            // Act
            var response = await _service.GetMatchStatus(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Matched, response.Status);
            Assert.Equal("session123", response.SessionId);
        }

        [Fact]
        public async Task GetMatchStatus_WithUnknownRequestId_ThrowsRpcException()
        {
            // Arrange
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusRequest
            {
                RequestId = "unknown-id"
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get("unknown-id")).Returns((MatchRequest?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RpcException>(
                () => _service.GetMatchStatus(request, context));
            
            Assert.Equal(StatusCode.NotFound, exception.StatusCode);
        }

        [Fact]
        public async Task CancelMatchRequest_WithPendingRequest_ReturnsSuccess()
        {
            // Arrange
            var matchRequest = new MatchRequest("user123");
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestRequest
            {
                RequestId = matchRequest.RequestId
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get(matchRequest.RequestId)).Returns(matchRequest);
            _mockMatchPool.Setup(p => p.Remove(matchRequest.RequestId)).Returns(matchRequest);

            // Act
            var response = await _service.CancelMatchRequest(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            _mockMatchPool.Verify(p => p.Remove(matchRequest.RequestId), Times.Once);
        }

        [Fact]
        public async Task CancelMatchRequest_WithMatchedRequest_ThrowsRpcException()
        {
            // Arrange
            var matchRequest = new MatchRequest("user123");
            matchRequest.Status = ModelMatchRequestStatus.Matched;
            matchRequest.SessionId = "session123";
            
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestRequest
            {
                RequestId = matchRequest.RequestId
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get(matchRequest.RequestId)).Returns(matchRequest);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RpcException>(
                () => _service.CancelMatchRequest(request, context));
            
            Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
        }

        [Fact]
        public async Task CancelMatchRequest_WithUnknownRequestId_ThrowsRpcException()
        {
            // Arrange
            var request = new AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestRequest
            {
                RequestId = "unknown-id"
            };
            var context = CreateMockContext("user123");
            
            _mockMatchPool.Setup(p => p.Get("unknown-id")).Returns((MatchRequest?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RpcException>(
                () => _service.CancelMatchRequest(request, context));
            
            Assert.Equal(StatusCode.NotFound, exception.StatusCode);
        }

        private ServerCallContext CreateMockContext(string userId)
        {
            var metadata = new Metadata
            {
                { GrpcConstants.UserIdKey, userId }
            };
            
            var context = new TestServerCallContext(metadata);
            // Simulate what the HeaderUserIdInterceptor does
            context.UserState.Add(GrpcConstants.UserIdKey, userId);
            return context;
        }
    }

    /// <summary>
    /// Test implementation of ServerCallContext for unit testing
    /// </summary>
    internal class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;

        public TestServerCallContext(Metadata requestHeaders)
        {
            _requestHeaders = requestHeaders;
        }

        protected override string MethodCore => "TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "127.0.0.1";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new Metadata();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => throw new NotImplementedException();

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
