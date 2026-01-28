// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    /// <summary>
    /// Exception thrown when a user attempts to submit a match request while already having a pending request
    /// </summary>
    public class DuplicateRequestException(string existingRequestId)
        : Exception($"User already has a pending match request: {existingRequestId}")
    {
        /// <summary>
        /// The ID of the existing pending request
        /// </summary>
        public string ExistingRequestId { get; } = existingRequestId;
    }

    /// <summary>
    /// Exception thrown when a match request is not found
    /// </summary>
    public class MatchRequestNotFoundException() : Exception("Match request not found");

    /// <summary>
    /// Exception thrown when attempting to cancel a request that has already been matched
    /// </summary>
    public class RequestAlreadyMatchedException() : Exception("Cannot cancel - request has already been matched");

    /// <summary>
    /// Exception thrown when session creation fails
    /// </summary>
    public class SessionCreationException : Exception
    {
        public SessionCreationException()
            : base("Failed to create session")
        {
        }

        public SessionCreationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when no available sessions are found
    /// </summary>
    public class NoAvailableSessionsException(int sessionsSearched)
        : Exception($"No available sessions found after searching {sessionsSearched} sessions")
    {
        public int SessionsSearched { get; } = sessionsSearched;
    }

    /// <summary>
    /// Exception thrown when session claiming fails after maximum retry attempts
    /// </summary>
    public class SessionClaimFailedException(int retryAttempts)
        : Exception($"Failed to claim session after {retryAttempts} retry attempts")
    {
        public int RetryAttempts { get; } = retryAttempts;
    }
}
