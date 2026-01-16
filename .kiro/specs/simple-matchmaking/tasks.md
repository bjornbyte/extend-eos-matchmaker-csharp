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

- [ ] 4. Define Proto Messages and Generate Code
  - Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Protos/matchmaking.proto` with all message types and service definition
  - Define `SubmitMatchRequestRequest`, `SubmitMatchRequestResponse`, `GetMatchStatusRequest`, `GetMatchStatusResponse`, `CancelMatchRequestRequest`, `CancelMatchRequestResponse`
  - Define `MatchRequestStatus` enum
  - Define `Matchmaking` service with RPC methods
  - Update `.csproj` to include proto compilation
  - _Requirements: 1.1, 1.2, 5.1, 5.2, 6.1, 6.2_

- [ ] 5. Implement Match Request Data Model
  - [ ] 5.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Model/MatchRequest.cs` with MatchRequest class and status enum
    - Define MatchRequest class with RequestId, UserId, Status, CreatedAt, MatchedAt, SessionId, Metadata
    - Define MatchRequestStatus enum (Pending, Matched, Expired, Cancelled)
    - Constructor generates unique GUID for RequestId
    - _Requirements: 1.1, 5.1_

  - [ ] 5.2 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Model/Match.cs` with Match class
    - Define Match class with MatchId, Requests list, CreatedAt
    - _Requirements: 3.1_

- [ ] 6. Implement Match Pool
  - [ ] 6.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchPool.cs` with IMatchPool interface and implementation
    - Implement thread-safe in-memory storage using ConcurrentDictionary and lock
    - Implement Add, Remove, Get, GetByUserId, GetOldest, RemoveExpired, Count methods
    - Use Dictionary for O(1) lookups by request ID and user ID
    - Use List for maintaining insertion order (FIFO)
    - _Requirements: 1.1, 3.2, 3.3, 6.1, 7.1_

  - [ ]* 6.2 Write property test for Match Pool FIFO ordering
    - **Property 5: FIFO Match Ordering**
    - **Validates: Requirements 3.2**

  - [ ]* 6.3 Write property test for Match Pool uniqueness
    - **Property 1: Request ID Uniqueness**
    - **Validates: Requirements 1.1**

- [ ] 7. Implement Session Creator Interface
  - [ ] 7.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/SessionCreator.cs` with ISessionCreator interface
    - Define ISessionCreator interface with CreateSessionAsync method
    - Define SessionInfo class with SessionId, RequestIds, UserIds, CreatedAt
    - Implement EOSSessionCreator that creates sessions via EOS SDK
    - Include matched request IDs in session attributes/metadata
    - _Requirements: 4.1, 4.2_

  - [ ]* 7.2 Write property test for session creation with request IDs
    - **Property 6: Session Creation with Request Identifiers**
    - **Validates: Requirements 4.1, 4.2**

- [ ] 8. Implement Notifier Interface
  - [ ] 8.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/Notifier.cs` with INotifier interface and LoggingNotifier
    - Define INotifier interface with NotifyMatchAsync method
    - Implement LoggingNotifier that logs match events (default no-op)
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 8.2 Write property test for notifier invocation
    - **Property 14: Notifier Receives Complete Match Information**
    - **Validates: Requirements 8.1, 8.3**

- [ ] 9. Implement Match Maker
  - [ ] 9.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchMaker.cs` with IMatchMaker interface and implementation
    - Define MatchMakerConfig with MatchSize, TickInterval, RequestTimeout
    - Implement IHostedService for background processing
    - Implement TryMatchAsync that creates matches from oldest requests
    - Handle session creation failure by returning requests to pool
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.3, 7.1, 7.2, 7.3_

  - [ ]* 9.2 Write property test for match creation and pool invariant
    - **Property 4: Match Creation and Pool Invariant**
    - **Validates: Requirements 3.1, 3.3, 3.4**

  - [ ]* 9.3 Write property test for session failure recovery
    - **Property 7: Session Failure Recovery**
    - **Validates: Requirements 4.3**

  - [ ]* 9.4 Write property test for expiration handling
    - **Property 13: Expiration Updates Status and Removes from Pool**
    - **Validates: Requirements 7.1, 7.3**

- [ ] 10. Checkpoint - Ensure core components work
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Implement Matchmaking Service
  - [ ] 11.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Services/MatchmakingService.cs` with gRPC service implementation
    - Implement SubmitMatchRequest: extract user ID from context, check for duplicates, add to pool, return request ID
    - Implement GetMatchStatus: lookup request, return status and session details if matched
    - Implement CancelMatchRequest: validate pending status, remove from pool, return confirmation
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3_

  - [ ]* 11.2 Write property test for duplicate user rejection
    - **Property 3: Duplicate User Rejection with Existing Request ID**
    - **Validates: Requirements 1.3**

  - [ ]* 11.3 Write property test for immediate response with request ID
    - **Property 2: Immediate Response with Request ID**
    - **Validates: Requirements 1.2**

  - [ ]* 11.4 Write property test for status query validity
    - **Property 8: Status Query Validity**
    - **Validates: Requirements 5.1**

  - [ ]* 11.5 Write property test for matched status contains session details
    - **Property 9: Matched Status Contains Session Details**
    - **Validates: Requirements 5.2**

  - [ ]* 11.6 Write property test for unknown request ID error
    - **Property 10: Unknown Request ID Error**
    - **Validates: Requirements 5.3**

  - [ ]* 11.7 Write property test for cancellation
    - **Property 11: Cancellation Removes and Confirms**
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 11.8 Write property test for cannot cancel matched request
    - **Property 12: Cannot Cancel Matched Request**
    - **Validates: Requirements 6.3**

- [ ] 12. Implement Exception Types
  - [ ] 12.1 Create `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/Classes/MatchmakingExceptions.cs`
    - Add DuplicateRequestException with ExistingRequestId property
    - Add MatchRequestNotFoundException
    - Add RequestAlreadyMatchedException
    - Add SessionCreationException
    - _Requirements: 1.3, 5.3, 6.3_

- [ ] 13. Wire Up Dependency Injection and Startup
  - [ ] 13.1 Update `Program.cs` to register matchmaking services
    - Register IMatchPool as singleton
    - Register ISessionCreator
    - Register INotifier (LoggingNotifier)
    - Register IMatchMaker as hosted service
    - Register MatchmakingService as gRPC service
    - Configure MatchMakerConfig from appsettings
    - _Requirements: 3.4, 7.2_

  - [ ] 13.2 Add configuration to `appsettings.json`
    - Add MatchMaker section with MatchSize, TickIntervalSeconds, RequestTimeoutSeconds
    - Add EOS SDK configuration section
    - _Requirements: 3.4, 7.2_

- [ ] 14. Checkpoint - Integration testing
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 15. Update Documentation
  - [ ] 15.1 Update `README.md` with matchmaking documentation
    - Add matchmaking API documentation
    - Document configuration options
    - Add usage examples
    - _Requirements: All_

- [ ] 16. Final Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional property-based tests that can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck for .NET
- Unit tests use xUnit
- The implementation follows AccelByte Extend Service Extension patterns

