# Implementation Plan: Session Finder for Existing Sessions

## Overview

This implementation adds an alternative session provider that finds and claims existing available EOS sessions instead of creating new ones. The implementation includes renaming the interface method, creating the new finder implementation, adding configuration support, and comprehensive testing.

## Tasks

- [x] 1. Rename interface method and update existing implementation
  - Rename `ISessionCreator.CreateSessionAsync` to `GetSessionAsync`
  - Update `EOSSessionCreator` to implement the renamed method
  - Update `MatchMaker` to call `GetSessionAsync` instead of `CreateSessionAsync`
  - Update interface documentation to clarify implementations may create or find sessions
  - Update existing tests to use new method name
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 2. Create configuration classes with TDD
  - [x] 2.1 Write tests for `SessionProviderConfig`
    - Test valid "create" mode
    - Test valid "find" mode
    - Test invalid mode throws exception
    - _Requirements: 9.1, 9.2, 9.3, 9.5_
  
  - [x] 2.2 Implement `SessionProviderConfig` class
    - Add `Mode` property with validation
    - Add `Validate()` method that throws on invalid mode
    - Support "create" and "find" modes
    - Run tests and verify they pass
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_
  
  - [x] 2.3 Write tests for `EOSSessionFinderConfig`
    - Test default values
    - Test property setters
    - _Requirements: 6.4, 7.1, 7.2_
  
  - [x] 2.4 Implement `EOSSessionFinderConfig` class
    - Add `BucketId` property (default: "default")
    - Add `MaxSearchResults` property (default: 10)
    - Add `ClaimedSessionExpirationSeconds` property (default: 300)
    - Run tests and verify they pass
    - _Requirements: 6.4, 7.1, 7.2_

- [x] 3. Create exception classes with TDD
  - [x] 3.1 Write tests for `NoAvailableSessionsException`
    - Test exception message format
    - Test `SessionsSearched` property
    - _Requirements: 5.1_
  
  - [x] 3.2 Implement `NoAvailableSessionsException`
    - Include `SessionsSearched` property
    - Provide descriptive error message
    - Run tests and verify they pass
    - _Requirements: 5.1_

- [x] 4. Create claimed sessions cache with TDD
  - [x] 4.1 Write tests for `IClaimedSessionsCache` interface
    - Test `IsSessionClaimed` returns false for unclaimed sessions
    - Test `IsSessionClaimed` returns true for claimed sessions
    - Test `AddClaimedSession` adds session to cache
    - Test `RemoveExpiredEntries` removes old entries
    - _Requirements: 2.1, 2.2, 2.5_
  
  - [x] 4.2 Implement `InMemoryClaimedSessionsCache` class
    - Use `ConcurrentDictionary` for thread safety
    - Implement `IsSessionClaimed` with automatic expiration cleanup
    - Implement `AddClaimedSession` with timestamp
    - Implement `RemoveExpiredEntries` based on configured expiration time
    - Run tests and verify they pass
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 5. Create session owner notifier with TDD
  - [x] 5.1 Write tests for `ISessionOwnerNotifier` interface
    - Test notification is called with correct parameters (sessionId, Match, connectionInfo)
    - Test notification includes full Match object with match ID, user IDs, request IDs
    - _Requirements: 3.1, 3.2, 3.3, 3.4_
  
  - [x] 5.2 Implement `StubSessionOwnerNotifier` class
    - Log notification details including session ID and all Match fields
    - Include TODO comment for developers to implement
    - Return completed task (fire and forget)
    - Run tests and verify they pass
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [-] 6. Implement EOSSessionFinder with TDD
  - [x] 6.1 Write tests for class skeleton - Completed TDD cycle - verified RED (compile) → RED (assertions: NotImplementedException) → GREEN phases
    - Test constructor accepts required dependencies
    - Test `GetSessionAsync` method exists
    - _Requirements: 4.1, 4.2, 4.3_
  
  - [x] 6.2 Create `EOSSessionFinder` class skeleton - Completed TDD cycle - verified RED (compile) → RED (assertions: NotImplementedException) → GREEN phases
    - Add constructor with dependencies (logger, EOS service, config, cache, notifier)
    - Add `GetSessionAsync` method stub
    - Run tests and verify they pass
    - _Requirements: 4.1, 4.2, 4.3_
  
  - [x] 6.3 Write tests for session search logic - Tests written, verified RED phase (NotImplementedException)
    - Test search returns available sessions only
    - Test search excludes sessions in claimed cache
    - Test search respects bucket ID filter
    - Test search respects max results
    - _Requirements: 1.1, 1.4, 6.1, 6.3, 6.4, 6.5_
  
  - [x] 6.4 Implement session search logic - Completed TDD cycle - verified RED (compile) → RED (assertions: NotImplementedException from claiming stub) → Implementation done, waiting for claiming logic
    - Create session search handle
    - Set search parameters (available servers, bucket ID)
    - Execute search and iterate results
    - Filter out sessions in claimed cache
    - Run tests and verify they pass
    - _Requirements: 1.1, 1.4, 6.1, 6.3, 6.4, 6.5_
  
  - [x] 6.5 Write tests for session claiming logic
    - Test session is added to claimed cache
    - Test session owner notification is sent
    - Test SessionInfo is returned with correct data
    - _Requirements: 1.2, 2.1, 3.1, 3.2, 3.3, 3.4_
  
  - [x] 6.6 Implement session claiming logic
    - Add session to claimed cache
    - Extract connection info from session details
    - Send notification to session owner with session ID, Match object, and connection info
    - Create and return SessionInfo
    - Run tests and verify they pass
    - _Requirements: 1.2, 2.1, 3.1, 3.2, 3.3, 3.4, 3.5_
  
  - [x] 6.7 Write tests for no available sessions
    - Test exception thrown when no sessions found
    - Test exception thrown when all sessions are claimed
    - Test exception includes session count
    - _Requirements: 1.3, 5.1, 5.2_
  
  - [x] 6.8 Implement no available sessions handling
    - Throw NoAvailableSessionsException when appropriate
    - Include session count in exception
    - Log warning with context
    - Run tests and verify they pass
    - _Requirements: 1.3, 5.1, 5.2, 5.3_

- [x] 7. Update dependency injection configuration with TDD
  - [x] 7.1 Write tests for DI setup
    - Test `EOSSessionCreator` registered for "create" mode
    - Test `EOSSessionFinder` registered for "find" mode
    - Test `IClaimedSessionsCache` registered for "find" mode
    - Test `ISessionOwnerNotifier` registered for "find" mode
    - Test startup fails with invalid mode
    - _Requirements: 4.4, 9.2, 9.3, 9.5_
  
  - [x] 7.2 Implement DI configuration
    - Read `SessionProvider` configuration section
    - Validate configuration at startup
    - Register `EOSSessionCreator` when mode is "create"
    - Register `EOSSessionFinder`, cache, and notifier when mode is "find"
    - Register `EOSSessionFinderConfig` when mode is "find"
    - Fail fast with clear error if mode is invalid
    - Log which session provider mode is being used at startup
    - Run tests and verify they pass
    - _Requirements: 4.4, 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 8. Create test helper for integration tests
  - [x] 8.1 Update `AvailableSessionTestHelper` class
    - Add method to create available session (not started)
    - Add method to create started session (for negative tests)
    - Add cleanup method to destroy test sessions
    - _Requirements: Testing infrastructure_

- [x] 9. Write integration tests
  - [x] 9.1 Test no sessions available
    - Verify `NoAvailableSessionsException` thrown
    - Verify error logging
    - _Requirements: 1.3, 5.1_
  
  - [x] 9.2 Test find and claim available sessions
    - Create N available sessions using test helper
    - Call `GetSessionAsync` N times
    - Verify each call claims a different session
    - Verify each session is added to claimed cache
    - Verify session owner notification sent for each
    - Verify (N+1)th call throws exception
    - _Requirements: 1.1, 1.2, 2.1, 3.1, 3.2, 3.3, 3.4_
  
  - [x] 9.3 Test started sessions are excluded
    - Create sessions in started state
    - Verify `GetSessionAsync` does not return them
    - Verify exception thrown if only started sessions exist
    - _Requirements: 6.2_
  
  - [x] 9.4 Test optimistic concurrency
    - Create a single available session
    - Start two concurrent `GetSessionAsync` calls
    - Verify exactly one succeeds
    - Verify the other finds different session or throws exception
    - Verify both calls never return same session ID
    - _Requirements: 2.3_
  
  - [x] 9.5 Test cache expiration
    - Claim a session
    - Wait for expiration time to pass
    - Verify session removed from cache
    - Verify session can be claimed again (if still available and not started)
    - _Requirements: 2.4, 2.5_

- [x] 10. Update documentation
  - [x] 10.1 Add README section for session provider modes
    - Explain "create" mode with use cases
    - Explain "find" mode with use cases
    - Provide configuration examples for both modes
    - Include session owner notification implementation example
    - Include dedicated server provider extension example
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 11. Checkpoint - Ensure all tests pass
  - Run all unit tests and verify they pass
  - Run all integration tests and verify they pass
  - Verify the project builds successfully
  - Ask the user if questions arise

## Notes

- Follow TDD: Write tests first, then implement to make them pass
- The interface method rename (`CreateSessionAsync` → `GetSessionAsync`) affects existing code
- Integration tests require real EOS SDK connection
- Test helper creates sessions in various states for testing
- Configuration validation happens at startup (fail-fast)
- Both implementations use the same `ISessionCreator` interface
- Session owners are responsible for updating sessions to "started" after receiving notification
- The claimed sessions cache provides concurrency safety with local tracking
- Session owner notification is a stub that developers extend for their game
- EOS automatically excludes started sessions from search results
