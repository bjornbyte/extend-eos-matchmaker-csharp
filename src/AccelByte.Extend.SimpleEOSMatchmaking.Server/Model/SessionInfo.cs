// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Model
{
    /// <summary>
    /// Information about a created session
    /// </summary>
    public class SessionInfo
    {
        public string SessionId { get; init; } = string.Empty;
        public List<string> RequestIds { get; init; } = [];
        public List<string> UserIds { get; init; } = [];
        public DateTime CreatedAt { get; init; }
    }
}
