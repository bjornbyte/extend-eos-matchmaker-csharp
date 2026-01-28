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
    public class MatchmakingService(
        IMatchPool matchPool,
        ICompletedRequestStore completedRequestStore,
        ILogger<MatchmakingService> logger)
        : Matchmaking.MatchmakingBase
    {
        private readonly IMatchPool MatchPool = matchPool ?? throw new ArgumentNullException(nameof(matchPool));
        private readonly ICompletedRequestStore CompletedRequestStore = completedRequestStore ?? throw new ArgumentNullException(nameof(completedRequestStore));
        private readonly ILogger<MatchmakingService> Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Submit a new match request
        /// </summary>
        public override async Task<SubmitMatchRequestResponse> SubmitMatchRequest(
            SubmitMatchRequestRequest request,
            ServerCallContext context)
        {
            string userId = ExtractUserIdFromContext(context);

            var existingRequest = MatchPool.GetByUserId(userId);
            if (existingRequest != null)
            {
                Logger.LogWarning("User {UserId} already has a pending match request: {RequestId}", 
                    userId, existingRequest.RequestId);
                
                throw new RpcException(new Status(
                    StatusCode.FailedPrecondition,
                    $"User already has a pending match request: {existingRequest.RequestId}"));
            }

            var metadata = request.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var matchRequest = new MatchRequest(userId, metadata);

            MatchPool.Add(matchRequest);

            Logger.LogInformation("Created match request {RequestId} for user {UserId}", 
                matchRequest.RequestId, userId);

            return await Task.FromResult(new SubmitMatchRequestResponse
            {
                RequestId = matchRequest.RequestId
            });
        }

        /// <summary>
        /// Get the status of a match request
        /// </summary>
        public override async Task<GetMatchStatusResponse> GetMatchStatus(
            GetMatchStatusRequest request,
            ServerCallContext context)
        {
            var matchRequest = MatchPool.Get(request.RequestId) ?? CompletedRequestStore.Get(request.RequestId);

            if (matchRequest == null)
            {
                Logger.LogWarning("Match request not found: {RequestId}", request.RequestId);
                throw new RpcException(new Status(StatusCode.NotFound, "Match request not found"));
            }

            var response = new GetMatchStatusResponse
            {
                RequestId = matchRequest.RequestId,
                Status = ConvertStatus(matchRequest.Status)
            };

            if (matchRequest.Status == Model.MatchRequestStatus.Matched && !string.IsNullOrEmpty(matchRequest.SessionId))
            {
                response.SessionId = matchRequest.SessionId;
            }

            return await Task.FromResult(response);
        }

        /// <summary>
        /// Cancel a pending match request
        /// </summary>
        public override async Task<CancelMatchRequestResponse> CancelMatchRequest(
            CancelMatchRequestRequest request,
            ServerCallContext context)
        {
            var matchRequest = MatchPool.Get(request.RequestId);
            if (matchRequest == null)
            {
                Logger.LogWarning("Match request not found for cancellation: {RequestId}", request.RequestId);
                throw new RpcException(new Status(StatusCode.NotFound, "Match request not found"));
            }

            if (matchRequest.Status != Model.MatchRequestStatus.Pending)
            {
                Logger.LogWarning("Cannot cancel non-pending request {RequestId} with status {Status}", 
                    request.RequestId, matchRequest.Status);
                
                throw new RpcException(new Status(
                    StatusCode.FailedPrecondition,
                    $"Cannot cancel request with status: {matchRequest.Status}"));
            }

            var removed = MatchPool.Remove(request.RequestId);
            if (removed != null)
            {
                removed.Status = Model.MatchRequestStatus.Cancelled;
                removed.CompletedAt = DateTime.UtcNow;
                CompletedRequestStore.Add(removed);
                
                Logger.LogInformation("Cancelled match request {RequestId}", request.RequestId);
            }

            return await Task.FromResult(new CancelMatchRequestResponse
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
        private MatchRequestStatus ConvertStatus(Model.MatchRequestStatus status)
        {
            return status switch
            {
                Model.MatchRequestStatus.Pending => MatchRequestStatus.Pending,
                Model.MatchRequestStatus.Matched => MatchRequestStatus.Matched,
                Model.MatchRequestStatus.Expired => MatchRequestStatus.Expired,
                Model.MatchRequestStatus.Cancelled => MatchRequestStatus.Cancelled,
                _ => MatchRequestStatus.Pending
            };
        }
    }
}
