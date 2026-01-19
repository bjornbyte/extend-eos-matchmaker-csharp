// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    /// <summary>
    /// Exception thrown when a user attempts to submit a match request while already having a pending request
    /// </summary>
    public class DuplicateRequestException : Exception
    {
        /// <summary>
        /// The ID of the existing pending request
        /// </summary>
        public string ExistingRequestId { get; }

        public DuplicateRequestException(string existingRequestId)
            : base($"User already has a pending match request: {existingRequestId}")
        {
            ExistingRequestId = existingRequestId;
        }
    }

    /// <summary>
    /// Exception thrown when a match request is not found
    /// </summary>
    public class MatchRequestNotFoundException : Exception
    {
        public MatchRequestNotFoundException()
            : base("Match request not found")
        {
        }

        public MatchRequestNotFoundException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when attempting to cancel a request that has already been matched
    /// </summary>
    public class RequestAlreadyMatchedException : Exception
    {
        public RequestAlreadyMatchedException()
            : base("Cannot cancel - request has already been matched")
        {
        }

        public RequestAlreadyMatchedException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Exception thrown when session creation fails
    /// </summary>
    public class SessionCreationException : Exception
    {
        public SessionCreationException()
            : base("Failed to create session")
        {
        }

        public SessionCreationException(string message)
            : base(message)
        {
        }

        public SessionCreationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
