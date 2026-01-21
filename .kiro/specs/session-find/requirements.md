# Requirements Document

## Introduction

This feature adds an alternative session provider implementation that finds and claims existing available EOS sessions instead of creating new ones. The two implementations serve different architectural patterns:

- **EOSSessionCreator** (existing): Creates new sessions. Use when the matchmaker should create sessions (e.g., for P2P gameplay, or when integrating with a dedicated server provider that allocates servers after session creation)
- **EOSSessionFinder** (new): Finds existing sessions. Use when sessions are pre-created by game servers or hosts and the matchmaker assigns player groups to available sessions (e.g., for player-hosted servers or dedicated servers that create their own sessions)

## Glossary

- **Session_Finder**: The new component that finds and claims existing available EOS sessions for matched players.
- **Session_Creator**: The existing component that creates new EOS sessions for matched players.
- **Available_Session**: An EOS session that has been created by a game server or host but has no players assigned to it yet.
- **Session_Claim**: The process of marking an available session as reserved for a specific match to prevent concurrent matches from using the same session.
- **Session_Search**: The process of querying EOS for existing available sessions that can be claimed.
- **Session_Metadata**: Attributes stored in an EOS session including match identifiers, player lists, and claim status.
- **Concurrent_Access**: Multiple match creation operations happening simultaneously that might attempt to claim the same session.
- **Session_Provider_Mode**: Configuration setting that determines whether the system creates new sessions or finds existing ones.

## Requirements

### Requirement 1: Find Existing Available Sessions

**User Story:** As a system operator, I want the matchmaking system to find existing available sessions created by game servers or hosts, so that matched players can be assigned to available sessions.

#### Acceptance Criteria

1. WHEN obtaining a session for a match THEN THE Session_Finder SHALL search for existing Available_Sessions
2. WHEN an Available_Session is found THEN THE Session_Finder SHALL return that session
3. IF no Available_Session is found THEN THE Session_Finder SHALL return an error indicating no available sessions
4. WHEN searching for sessions THEN THE Session_Finder SHALL only consider sessions that have no active players assigned

### Requirement 2: Session Claiming for Concurrency Safety

**User Story:** As a system operator, I want sessions to be claimed with local tracking, so that concurrent match operations don't assign players to the same session.

#### Acceptance Criteria

1. WHEN an Available_Session is selected THEN THE Session_Finder SHALL add it to a local claimed sessions cache
2. WHEN searching for sessions THEN THE Session_Finder SHALL exclude sessions that are in the local claimed sessions cache
3. WHEN a session is successfully claimed THEN THE Session_Finder SHALL ensure no other concurrent operation can claim the same session
4. THE Session_Finder SHALL support configurable expiration time for claimed session cache entries
5. THE Session_Finder SHALL automatically remove expired entries from the claimed sessions cache

### Requirement 3: Session Owner Notification

**User Story:** As a developer, I want the session owner to be notified when their session is claimed, so that the game server can update the session to started and prepare for players.

#### Acceptance Criteria

1. WHEN a session is claimed for a match THEN THE Session_Finder SHALL send a notification to the session owner with match information
2. THE notification SHALL include the match identifier
3. THE notification SHALL include the list of matched user identifiers
4. THE notification SHALL include the list of matched request identifiers
5. THE Session_Finder SHALL use the connection information from the session to send the notification
6. THE notification mechanism SHALL be a stub implementation that developers can extend for their game

### Requirement 4: Interface Compatibility

**User Story:** As a developer, I want the new session finder to be compatible with the existing interface, so that I can swap implementations based on deployment model without changing other code.

#### Acceptance Criteria

1. THE Session_Finder SHALL implement the existing ISessionCreator interface
2. WHEN the Session_Finder returns a session THEN THE return type SHALL be SessionInfo with the same structure as the current implementation
3. THE Session_Finder SHALL accept the same Match parameter as the current CreateSessionAsync method
4. THE Session_Finder SHALL be registered as an alternative implementation of ISessionCreator in dependency injection

### Requirement 5: Error Handling for No Available Sessions

**User Story:** As a system operator, I want clear error messages when no sessions are available, so that I can monitor server capacity and scale appropriately.

#### Acceptance Criteria

1. IF no Available_Session is found after searching THEN THE Session_Finder SHALL throw a NoAvailableSessionsException
2. WHEN throwing an exception THEN THE Session_Finder SHALL log the error with relevant context
3. THE exception messages SHALL include information about the number of sessions searched

### Requirement 6: Session Search Criteria

**User Story:** As a system operator, I want to control which sessions are considered for assignment, so that only appropriate sessions are selected.

#### Acceptance Criteria

1. WHEN searching for sessions THEN THE Session_Finder SHALL only consider sessions with zero registered players
2. WHEN searching for sessions THEN THE Session_Finder SHALL exclude sessions that are in the started state
3. WHEN searching for sessions THEN THE Session_Finder SHALL exclude sessions that are in the local claimed sessions cache
4. WHEN searching for sessions THEN THE Session_Finder SHALL support filtering by session bucket identifier
5. WHEN searching for sessions THEN THE Session_Finder SHALL query EOS using the Sessions interface search functionality

### Requirement 7: Configuration and Observability

**User Story:** As a system operator, I want to configure and monitor session finding behavior, so that I can optimize performance and troubleshoot issues.

#### Acceptance Criteria

1. THE Session_Finder SHALL support configuration for claimed session cache expiration time
2. THE Session_Finder SHALL support configuration for session search bucket identifier
3. THE Session_Finder SHALL log when a session is successfully found and claimed
4. THE Session_Finder SHALL log when session search fails to find available sessions
5. THE Session_Finder SHALL log when notifying session owners of claimed sessions

### Requirement 8: Interface Method Naming

**User Story:** As a developer, I want the interface method name to reflect that it may find or create sessions, so that the API is clear for both implementations.

#### Acceptance Criteria

1. THE ISessionCreator interface method SHALL be renamed from CreateSessionAsync to GetSessionAsync
2. WHEN renaming the method THEN THE existing EOSSessionCreator implementation SHALL be updated to use the new method name
3. WHEN renaming the method THEN THE MatchMaker SHALL be updated to call the new method name
4. THE method documentation SHALL clarify that implementations may create new sessions or find existing ones

### Requirement 9: Configuration-Based Session Provider Selection

**User Story:** As a system operator, I want to configure which session provider implementation to use, so that I can easily switch between different architectural patterns.

#### Acceptance Criteria

1. THE system SHALL support a configuration option to select between session provider implementations
2. WHEN the configuration specifies "create" mode THEN THE system SHALL use EOSSessionCreator
3. WHEN the configuration specifies "find" mode THEN THE system SHALL use EOSSessionFinder
4. THE configuration option SHALL be named "SessionProviderMode" with allowed values "create" or "find"
5. THE system SHALL validate the configuration value at startup and fail fast with a clear error if invalid

### Requirement 10: Deployment Pattern Documentation

**User Story:** As a developer, I want clear documentation explaining the two session provider modes and how to extend the system, so that I can choose and adapt the appropriate pattern for my deployment.

#### Acceptance Criteria

1. THE system SHALL include documentation explaining "create" mode and example use cases (P2P gameplay, integration with dedicated server providers)
2. THE system SHALL include documentation explaining "find" mode and example use cases (player-hosted servers, dedicated servers that create their own sessions)
3. THE documentation SHALL explain how to configure the SessionProviderMode setting
4. THE documentation SHALL include examples of configuration for both modes
5. THE documentation SHALL include an example of how to extend "create" mode with a dedicated server provider that allocates servers after session creation
