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
        public string SessionId { get; set; } = string.Empty;
        public List<string> RequestIds { get; set; } = new();
        public List<string> UserIds { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
