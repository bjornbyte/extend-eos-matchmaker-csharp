# Requirements Document

## Introduction

This feature adds a simple matchmaking system to the service. Users can submit match requests through a single API endpoint, and the system performs basic matchmaking to pair players together. When a match is found, the system creates an EOS session for the matched players and includes the original match request identifiers in the session data.

## Glossary

- **Matchmaking_Service**: The component responsible for receiving match requests, storing them in a queue, and pairing compatible players together.
- **Match_Request**: A request from a user to be matched with other players, containing the user's identifier and optional matching criteria.
- **Match_Pool**: An in-memory collection of pending match requests waiting to be paired.
- **Match**: A successful pairing of two or more match requests that will be placed into a game session together.
- **EOS_Session**: An Epic Online Services session created for matched players.
- **Request_Identifier**: A unique identifier assigned to each match request, used to track the request through the matchmaking process.
- **Completed_Request_Store**: A temporary storage for match requests that have reached a terminal state (matched, expired, or cancelled), allowing status queries for a configurable retention period.
- **Retention_Period**: The configurable duration for which completed match requests remain queryable after reaching a terminal state.

## Requirements

### Requirement 1: Submit Match Request

**User Story:** As a player, I want to submit a match request, so that I can be matched with other players for a game session.

#### Acceptance Criteria

1. WHEN a user submits a match request with valid authentication THEN THE Matchmaking_Service SHALL create a new Match_Request with a unique Request_Identifier and add it to the Match_Pool
2. WHEN a user submits a match request THEN THE Matchmaking_Service SHALL return the Request_Identifier to the user immediately
3. IF a user submits a match request while already having a pending request THEN THE Matchmaking_Service SHALL reject the new request and return an error indicating a pending request exists
4. WHEN a match request is submitted THEN THE Matchmaking_Service SHALL extract the user ID from the authorization token

### Requirement 2: Match Request Validation

**User Story:** As a system operator, I want match requests to be validated, so that only valid requests enter the matchmaking pool.

#### Acceptance Criteria

1. IF a match request is submitted without valid authentication THEN THE Matchmaking_Service SHALL reject the request with an authentication error
2. IF a match request contains invalid or missing required fields THEN THE Matchmaking_Service SHALL reject the request with a validation error

### Requirement 3: Simple Matchmaking Logic

**User Story:** As a player, I want to be matched with other players quickly, so that I can start playing without long wait times.

#### Acceptance Criteria

1. WHEN two or more Match_Requests are in the Match_Pool THEN THE Matchmaking_Service SHALL attempt to create a Match by pairing compatible requests
2. WHEN creating a Match THEN THE Matchmaking_Service SHALL pair the oldest pending requests first (FIFO ordering)
3. WHEN a Match is created THEN THE Matchmaking_Service SHALL remove the matched requests from the Match_Pool
4. THE Matchmaking_Service SHALL support configurable match size (default: 2 players per match)

### Requirement 4: Session Creation on Match

**User Story:** As a player, I want an EOS session to be created when I'm matched, so that I can play with my matched opponents.

#### Acceptance Criteria

1. WHEN a Match is created THEN THE Matchmaking_Service SHALL create an EOS_Session for the matched players
2. WHEN creating an EOS_Session THEN THE Matchmaking_Service SHALL include all matched Request_Identifiers in the session metadata
3. IF session creation fails THEN THE Matchmaking_Service SHALL return the Match_Requests to the Match_Pool for retry

### Requirement 5: Match Status Query

**User Story:** As a player, I want to check the status of my match request, so that I know if I've been matched or am still waiting.

#### Acceptance Criteria

1. WHEN a user queries their match status with a valid Request_Identifier THEN THE Matchmaking_Service SHALL return the current status (pending, matched, or expired)
2. WHEN a match has been found THEN THE Matchmaking_Service SHALL return the session details including the EOS_Session ID
3. IF a user queries with an invalid or unknown Request_Identifier THEN THE Matchmaking_Service SHALL return a not found error

### Requirement 6: Match Request Retention

**User Story:** As a player, I want to query the status of my match request even after it has been matched, so that I can retrieve session details if I missed the initial notification.

#### Acceptance Criteria

1. WHEN a Match_Request reaches a terminal state (matched, expired, or cancelled) THEN THE Matchmaking_Service SHALL move the request to the Completed_Request_Store
2. WHILE a completed Match_Request is within the Retention_Period THEN THE Matchmaking_Service SHALL allow status queries to retrieve the request details
3. WHEN a completed Match_Request exceeds the Retention_Period THEN THE Matchmaking_Service SHALL remove it from the Completed_Request_Store
4. THE Matchmaking_Service SHALL support configurable Retention_Period (default: 120 seconds)
5. WHEN querying status for a completed request within the Retention_Period THEN THE Matchmaking_Service SHALL return the same information as if the request were still in the Match_Pool

### Requirement 7: Match Request Cancellation

**User Story:** As a player, I want to cancel my match request, so that I can stop waiting for a match if I change my mind.

#### Acceptance Criteria

1. WHEN a user cancels their pending match request THEN THE Matchmaking_Service SHALL remove the request from the Match_Pool
2. WHEN a match request is cancelled THEN THE Matchmaking_Service SHALL return a confirmation of cancellation
3. IF a user attempts to cancel a request that has already been matched THEN THE Matchmaking_Service SHALL return an error indicating the request is no longer pending

### Requirement 8: Match Request Expiration

**User Story:** As a system operator, I want match requests to expire after a timeout, so that stale requests don't accumulate in the system.

#### Acceptance Criteria

1. WHILE a Match_Request has been pending longer than the configured timeout THEN THE Matchmaking_Service SHALL mark the request as expired and remove it from the Match_Pool
2. THE Matchmaking_Service SHALL support configurable request timeout (default: 60 seconds)
3. WHEN a request expires THEN THE Matchmaking_Service SHALL update the request status to expired

### Requirement 9: Match Notification

**User Story:** As a developer, I want a pluggable notification mechanism, so that I can integrate my own notification system when matches are found.

#### Acceptance Criteria

1. WHEN a Match is created THEN THE Matchmaking_Service SHALL invoke a Notifier interface to notify matched players
2. THE Matchmaking_Service SHALL provide a default no-op Notifier implementation that logs match events
3. THE Notifier interface SHALL accept the EOS_Session ID, matched user IDs, and matched Request_Identifiers as parameters
