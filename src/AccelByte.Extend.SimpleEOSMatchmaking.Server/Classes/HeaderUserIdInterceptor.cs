// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

/// <summary>
/// Interceptor that extracts user ID from the "user-id" header when authentication is disabled.
/// This is a simpler alternative to AuthorizationInterceptor for development/testing scenarios.
/// </summary>
public class HeaderUserIdInterceptor : Interceptor
{
    private readonly ILogger<HeaderUserIdInterceptor> _logger;

    public HeaderUserIdInterceptor(ILogger<HeaderUserIdInterceptor> logger)
    {
        _logger = logger;
    }

    private void ExtractUserIdFromHeader(ServerCallContext context)
    {
        var userIdHeader = context.RequestHeaders.GetValue(GrpcConstants.UserIdKey);
        
        if (string.IsNullOrEmpty(userIdHeader))
        {
            _logger.LogWarning("Missing or empty user-id header");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "User-id header is required"));
        }
        
        context.UserState.Add(GrpcConstants.UserIdKey, userIdHeader);
        _logger.LogDebug("Extracted user ID from header: {UserId}", userIdHeader);
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ExtractUserIdFromHeader(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractUserIdFromHeader(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractUserIdFromHeader(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractUserIdFromHeader(context);
        await continuation(requestStream, responseStream, context);
    }
}
