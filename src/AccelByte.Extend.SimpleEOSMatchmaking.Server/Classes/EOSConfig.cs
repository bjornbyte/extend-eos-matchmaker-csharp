// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    public class EOSConfig
    {
        public string ProductId { get; set; } = string.Empty;
        public string SandboxId { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Reads EOS configuration from environment variables with EOS_ prefix
        /// </summary>
        public void ReadEnvironmentVariables()
        {
            string? eosProductId = Environment.GetEnvironmentVariable("EOS_PRODUCT_ID");
            if (!string.IsNullOrWhiteSpace(eosProductId))
                ProductId = eosProductId.Trim();

            string? eosSandboxId = Environment.GetEnvironmentVariable("EOS_SANDBOX_ID");
            if (!string.IsNullOrWhiteSpace(eosSandboxId))
                SandboxId = eosSandboxId.Trim();

            string? eosDeploymentId = Environment.GetEnvironmentVariable("EOS_DEPLOYMENT_ID");
            if (!string.IsNullOrWhiteSpace(eosDeploymentId))
                DeploymentId = eosDeploymentId.Trim();

            string? eosClientId = Environment.GetEnvironmentVariable("EOS_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(eosClientId))
                ClientId = eosClientId.Trim();

            string? eosClientSecret = Environment.GetEnvironmentVariable("EOS_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(eosClientSecret))
                ClientSecret = eosClientSecret.Trim();
        }
    }
}
