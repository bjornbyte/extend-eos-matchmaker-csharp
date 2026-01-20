using System;
using System.Linq;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Sdk.Core;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// gRPC service implementation for matchmaking operations
    /// </summary>
    public class MatchmakingService : AccelByte.Extend.SimpleEOSMatchmaking.Matchmaking.MatchmakingBase
    {
        private readonly IMatchPool _matchPool;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(IMatchPool matchPool, ILogger<MatchmakingService> logger)
        {
            _matchPool = matchPool ?? throw new ArgumentNullException(nameof(matchPool));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Submit a new match request
        /// </summary>
        public override async Task<AccelByte.Extend.SimpleEOSMatchmaking.SubmitMatchRequestResponse> SubmitMatchRequest(
            AccelByte.Extend.SimpleEOSMatchmaking.SubmitMatchRequestRequest request,
            ServerCallContext context)
        {
            // Extract user ID from context
            string userId = ExtractUserIdFromContext(context);

            // Check for duplicate request
            var existingRequest = _matchPool.GetByUserId(userId);
            if (existingRequest != null)
            {
                _logger.LogWarning("User {UserId} already has a pending match request: {RequestId}", 
                    userId, existingRequest.RequestId);
                
                throw new RpcException(new Status(
                    StatusCode.FailedPrecondition,
                    $"User already has a pending match request: {existingRequest.RequestId}"));
            }

            // Create new match request
            var metadata = request.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var matchRequest = new MatchRequest(userId, metadata);

            // Add to pool
            _matchPool.Add(matchRequest);

            _logger.LogInformation("Created match request {RequestId} for user {UserId}", 
                matchRequest.RequestId, userId);

            return await Task.FromResult(new AccelByte.Extend.SimpleEOSMatchmaking.SubmitMatchRequestResponse
            {
                RequestId = matchRequest.RequestId
            });
        }

        /// <summary>
        /// Get the status of a match request
        /// </summary>
        public override async Task<AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusResponse> GetMatchStatus(
            AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusRequest request,
            ServerCallContext context)
        {
            // Lookup request
            var matchRequest = _matchPool.Get(request.RequestId);
            if (matchRequest == null)
            {
                _logger.LogWarning("Match request not found: {RequestId}", request.RequestId);
                throw new RpcException(new Status(StatusCode.NotFound, "Match request not found"));
            }

            // Build response
            var response = new AccelByte.Extend.SimpleEOSMatchmaking.GetMatchStatusResponse
            {
                RequestId = matchRequest.RequestId,
                Status = ConvertStatus(matchRequest.Status)
            };

            // Add session details if matched
            if (matchRequest.Status == Model.MatchRequestStatus.Matched && !string.IsNullOrEmpty(matchRequest.SessionId))
            {
                response.SessionId = matchRequest.SessionId;
                
                // Note: In a real implementation, we would need to track matched user IDs and request IDs
                // For now, we'll leave these empty as they would be populated by the MatchMaker
            }

            return await Task.FromResult(response);
        }

        /// <summary>
        /// Cancel a pending match request
        /// </summary>
        public override async Task<AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestResponse> CancelMatchRequest(
            AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestRequest request,
            ServerCallContext context)
        {
            // Lookup request
            var matchRequest = _matchPool.Get(request.RequestId);
            if (matchRequest == null)
            {
                _logger.LogWarning("Match request not found for cancellation: {RequestId}", request.RequestId);
                throw new RpcException(new Status(StatusCode.NotFound, "Match request not found"));
            }

            // Validate pending status
            if (matchRequest.Status != Model.MatchRequestStatus.Pending)
            {
                _logger.LogWarning("Cannot cancel non-pending request {RequestId} with status {Status}", 
                    request.RequestId, matchRequest.Status);
                
                throw new RpcException(new Status(
                    StatusCode.FailedPrecondition,
                    $"Cannot cancel request with status: {matchRequest.Status}"));
            }

            // Remove from pool
            var removed = _matchPool.Remove(request.RequestId);
            if (removed != null)
            {
                removed.Status = Model.MatchRequestStatus.Cancelled;
                _logger.LogInformation("Cancelled match request {RequestId}", request.RequestId);
            }

            return await Task.FromResult(new AccelByte.Extend.SimpleEOSMatchmaking.CancelMatchRequestResponse
            {
                Success = removed != null
            });
        }

        /// <summary>
        /// Extract user ID from the gRPC context
        /// </summary>
        private string ExtractUserIdFromContext(ServerCallContext context)
        {
            if (context.UserState.TryGetValue(GrpcConstants.UserIdKey, out var userIdValue))
            {
                return userIdValue as string ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            throw new RpcException(new Status(StatusCode.InvalidArgument, "Missing user ID"));
        }

        /// <summary>
        /// Convert internal status enum to proto status enum
        /// </summary>
        private AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus ConvertStatus(Model.MatchRequestStatus status)
        {
            return status switch
            {
                Model.MatchRequestStatus.Pending => AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Pending,
                Model.MatchRequestStatus.Matched => AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Matched,
                Model.MatchRequestStatus.Expired => AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Expired,
                Model.MatchRequestStatus.Cancelled => AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Cancelled,
                _ => AccelByte.Extend.SimpleEOSMatchmaking.MatchRequestStatus.Pending
            };
        }
    }
}
