// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

/// <summary>
/// Common constants used across gRPC interceptors and services.
/// </summary>
public static class GrpcConstants
{
    /// <summary>
    /// Key used to store and retrieve user ID in ServerCallContext.UserState.
    /// This is set by both AuthorizationInterceptor (from JWT token) and 
    /// HeaderUserIdInterceptor (from "user-id" header).
    /// </summary>
    public const string UserIdKey = "user-id";
}
