# Implementation Plan: Simple Matchmaking

## Overview

This implementation plan breaks down the simple matchmaking feature into discrete coding tasks. The implementation uses the AccelByte Extend Service Extension for C# template, with gRPC services and EOS SDK integration for session creation.

## Tasks

- [x] 1. Rename Project to SimpleEOSMatchmaking
  - Rename `src/AccelByte.Extend.ServiceExtension.Server/` directory to `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/`
  - Rename `AccelByte.Extend.ServiceExtension.Server.csproj` to `AccelByte.Extend.SimpleEOSMatchmaking.Server.csproj`
  - Update namespace declarations in all `.cs` files from `AccelByte.Extend.ServiceExtension.Server` to `AccelByte.Extend.SimpleEOSMatchmaking.Server`
  - Update solution file `extend-service-extension-server.sln` to reference the new project name
  - Update assembly name and root namespace in `.csproj` file
  - _Requirements: All (project setup)_

- [x] 2. Setup Test Project and Dependencies
  - Create test project `src/AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests/`
  - Add xUnit test framework package
  - Add FsCheck package for property-based testing
  - Add reference to main project
  - Add to solution file
  - _Requirements: All (testing infrastructure)_

- [x] 3. Add EOS SDK Package
  - Add Epic Online Services SDK NuGet package to main project
  - Configure EOS SDK initialization in Program.cs or separate configuration class
  - Add EOS configuration section to appsettings.json (ProductId, SandboxId, DeploymentId, ClientId, ClientSecret)
  - _Requirements: 4.1, 4.2_

- [x] 4. Remove Guild Progress Functionality
  - Delete `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MyService.cs`
  - Delete `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Model/GuildProgressData.cs`
  - Delete `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Protos/service.proto`
  - Remove `app.MapGrpcService<MyService>();` from `Program.cs`
  - Remove CloudSave SDK usage and dependencies if not needed for matchmaking
  - _Requirements: All (cleanup template functionality)_

- [x] 5. Define Proto Messages and Generate Code
  - Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Protos/matchmaking.proto` with all message types and service definition
  - Define `SubmitMatchRequestRequest`, `SubmitMatchRequestResponse`, `GetMatchStatusRequest`, `GetMatchStatusResponse`, `CancelMatchRequestRequest`, `CancelMatchRequestResponse`
  - Define `MatchRequestStatus` enum
  - Define `Matchmaking` service with RPC methods
  - Update `.csproj` to include proto compilation
  - _Requirements: 1.1, 1.2, 5.1, 5.2, 6.1, 6.2_

- [x] 6. Implement Match Request Data Model
  - [x] 6.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Model/MatchRequest.cs` with MatchRequest class and status enum
    - Define MatchRequest class with RequestId, UserId, Status, CreatedAt, MatchedAt, SessionId, Metadata
    - Define MatchRequestStatus enum (Pending, Matched, Expired, Cancelled)
    - Constructor generates unique GUID for RequestId
    - _Requirements: 1.1, 5.1_

  - [x] 6.2 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Model/Match.cs` with Match class
    - Define Match class with MatchId, Requests list, CreatedAt
    - _Requirements: 3.1_

- [x] 7. Implement Match Pool
  - [x] 7.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchPool.cs` with IMatchPool interface and implementation
    - Implement thread-safe in-memory storage using ConcurrentDictionary and lock
    - Implement Add, Remove, Get, GetByUserId, GetOldest, RemoveExpired, Count methods
    - Use Dictionary for O(1) lookups by request ID and user ID
    - Use List for maintaining insertion order (FIFO)
    - _Requirements: 1.1, 3.2, 3.3, 6.1, 7.1_

- [x] 8. Implement Completed Request Store
  - [x] 8.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/CompletedRequestStore.cs` with ICompletedRequestStore interface and implementation
    - Implement thread-safe in-memory storage for completed requests
    - Implement Add, Get, RemoveExpired, Count methods
    - Use Dictionary for O(1) lookups by request ID
    - Track CompletedAt timestamp for retention period calculation
    - _Requirements: 6.1, 6.2, 6.3_
    - **TDD Completed**: RED phase verified (enum conversion errors between proto and model namespaces), GREEN phase verified (all 8 tests passed)

- [x] 9. Update Match Request Model for Retention
  - [x] 9.1 Add CompletedAt property to MatchRequest class
    - Add nullable DateTime? CompletedAt property
    - Set CompletedAt when request reaches terminal state
    - _Requirements: 6.1, 6.3_
    - **Note**: Completed as part of Task 8.1

- [x] 10. Update Match Maker to Use Completed Request Store
  - [x] 10.1 Inject ICompletedRequestStore into MatchMaker
    - Add ICompletedRequestStore parameter to constructor
    - _Requirements: 6.1_
    - **TDD Completed**: RED phase verified (constructor signature mismatch, missing RetentionPeriod config), GREEN phase verified (all 10 MatchMaker tests passed)

  - [x] 10.2 Move matched requests to completed store
    - After successful match and session creation, set CompletedAt and move requests to completed store
    - _Requirements: 6.1_
    - **TDD Completed**: Verified with TryMatchAsync_MovesMatchedRequestsToCompletedStore test

  - [x] 10.3 Move expired requests to completed store
    - When removing expired requests, set CompletedAt and move to completed store
    - _Requirements: 6.1, 7.3_
    - **TDD Completed**: Verified with TryMatchAsync_MovesExpiredRequestsToCompletedStore test

  - [x] 10.4 Add cleanup of expired completed requests
    - In background tick, call RemoveExpired on completed store with retention period
    - _Requirements: 6.3_
    - **TDD Completed**: Verified with TryMatchAsync_CleansUpExpiredCompletedRequests test

- [x] 11. Update Matchmaking Service to Query Completed Store
  - [x] 11.1 Inject ICompletedRequestStore into MatchmakingService
    - Add ICompletedRequestStore parameter to constructor
    - _Requirements: 6.2, 6.5_
    - **TDD Completed**: RED phase verified (constructor signature mismatch), GREEN phase verified (all 12 MatchmakingService tests passed)

  - [x] 11.2 Update GetMatchStatus to check completed store
    - First check Match_Pool, then check Completed_Request_Store if not found
    - Return same response format for both pending and completed requests
    - _Requirements: 6.2, 6.5_
    - **TDD Completed**: Verified with GetMatchStatus_ChecksCompletedStoreWhenNotInPool and GetMatchStatus_ThrowsNotFoundWhenNotInPoolOrCompletedStore tests

  - [x] 11.3 Update CancelMatchRequest to move to completed store
    - When cancelling, set CompletedAt and move to completed store instead of just removing
    - _Requirements: 6.1_
    - **TDD Completed**: Verified with CancelMatchRequest_MovesToCompletedStore test

- [x] 12. Update Configuration for Retention Period
  - [x] 12.1 Add RetentionPeriod to MatchMakerConfig
    - Add RetentionPeriodSeconds property (default: 120)
    - _Requirements: 6.4_
    - **Note**: Completed as part of Task 10.1

  - [x] 12.2 Update appsettings.json with retention period
    - Add RetentionPeriodSeconds to MatchMaker configuration section
    - _Requirements: 6.4_
    - **Note**: Configuration loading added in Program.cs

- [x] 13. Update Dependency Injection for Completed Store
  - [x] 13.1 Register ICompletedRequestStore in Program.cs
    - Register as singleton
    - _Requirements: 6.1_
    - **Note**: Completed as part of Task 10.1

- [x] 14. Checkpoint - Ensure retention feature works
  - All tests pass (98 tests passing)
  - Retention feature fully implemented:
    - CompletedRequestStore stores completed requests with timestamps
    - MatchMaker moves matched, expired requests to completed store
    - MatchMaker cleans up expired completed requests based on retention period
    - MatchmakingService queries completed store when request not in pool
    - MatchmakingService moves cancelled requests to completed store
    - Configuration supports RetentionPeriodSeconds (default: 120)
  - **Status**: Feature complete and verified

- [x] 15. Implement Session Creator Interface
  - [x] 15.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/SessionCreator.cs` with ISessionCreator interface
    - Define ISessionCreator interface with CreateSessionAsync method
    - Define SessionInfo class with SessionId, RequestIds, UserIds, CreatedAt
    - Implement EOSSessionCreator that creates sessions via EOS SDK
    - Include matched request IDs in session attributes/metadata
    - _Requirements: 4.1, 4.2_

- [x] 16. Implement Notifier Interface
  - [x] 16.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/Notifier.cs` with INotifier interface and LoggingNotifier
    - Define INotifier interface with NotifyMatchAsync method
    - Implement LoggingNotifier that logs match events (default no-op)
    - _Requirements: 8.1, 8.2, 8.3_
    - **TDD Completed**: RED phase verified (compilation errors for missing types), GREEN phase verified (all 6 tests passed)

- [x] 17. Implement Match Maker
  - [x] 17.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchMaker.cs` with IMatchMaker interface and implementation
    - Define MatchMakerConfig with MatchSize, TickInterval, RequestTimeout
    - Implement IHostedService for background processing
    - Implement TryMatchAsync that creates matches from oldest requests
    - Handle session creation failure by returning requests to pool
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.3, 7.1, 7.2, 7.3_

- [x] 18. Checkpoint - Ensure core components work
  - Ensure all tests pass, ask the user if questions arise.

- [x] 19. Implement Matchmaking Service
  - [x] 19.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchmakingService.cs` with gRPC service implementation
    - Implement SubmitMatchRequest: extract user ID from context, check for duplicates, add to pool, return request ID
    - Implement GetMatchStatus: lookup request, return status and session details if matched
    - Implement CancelMatchRequest: validate pending status, remove from pool, return confirmation
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3_
    - **TDD Completed**: RED phase verified (ServerCallContext mocking errors - "Non-overridable members may not be used in setup expressions"), GREEN phase verified (all 8 tests passed after creating TestServerCallContext helper class)

- [x] 20. Implement Exception Types
  - [x] 20.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Classes/MatchmakingExceptions.cs`
    - Add DuplicateRequestException with ExistingRequestId property
    - Add MatchRequestNotFoundException
    - Add RequestAlreadyMatchedException
    - Add SessionCreationException
    - _Requirements: 1.3, 5.3, 6.3_

- [-] 21. Wire Up Dependency Injection and Startup
  - [x] 21.1 Update `Program.cs` to register matchmaking services
    - Register IMatchPool as singleton
    - Register ISessionCreator
    - Register INotifier (LoggingNotifier)
    - Register IMatchMaker as hosted service
    - Register MatchmakingService as gRPC service
    - Configure MatchMakerConfig from appsettings
    - _Requirements: 3.4, 7.2_

  - [x] 21.2 Add configuration to `appsettings.json`
    - Add MatchMaker section with MatchSize, TickIntervalSeconds, RequestTimeoutSeconds
    - Add EOS SDK configuration section
    - _Requirements: 3.4, 7.2_

- [x] 22. Checkpoint - Integration testing
  - Ensure all tests pass, ask the user if questions arise.
  - **Status**: All 71 tests passing, build succeeds

- [x] 23. Update Documentation
  - [x] 23.1 Update `README.md` with matchmaking documentation
    - Add matchmaking API documentation
    - Document configuration options
    - Add usage examples
    - _Requirements: All_
    - **Status**: README.md completely rewritten with comprehensive matchmaking documentation including API endpoints, configuration, testing guide, architecture overview, and deployment instructions

- [x] 24. Final Checkpoint
  - Ensure all tests pass, ask the user if questions arise.
  - **Status**: All 71 tests passing, build succeeds, documentation complete

## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Unit tests use xUnit
- The implementation follows AccelByte Extend Service Extension patterns

