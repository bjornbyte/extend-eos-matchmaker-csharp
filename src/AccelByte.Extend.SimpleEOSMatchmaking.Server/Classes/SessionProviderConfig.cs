// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    /// <summary>
    /// Configuration for session provider mode selection
    /// </summary>
    public class SessionProviderConfig
    {
        /// <summary>
        /// Session provider mode: "create" or "find"
        /// </summary>
        public string Mode { get; set; } = "create";

        /// <summary>
        /// Validate the configuration
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(Mode) || (Mode != "create" && Mode != "find"))
            {
                throw new InvalidOperationException(
                    $"Invalid SessionProviderMode '{Mode}'. Must be 'create' or 'find'.");
            }
        }
    }
}
