# Implementation Plan: Session Finder for Existing Sessions

## Overview

This implementation adds an alternative session provider that finds and claims existing empty EOS sessions instead of creating new ones. The implementation includes renaming the interface method, creating the new finder implementation, adding configuration support, and comprehensive testing.

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
    - _Requirements: 2.5, 6.3, 7.1, 7.2_
  
  - [x] 2.4 Implement `EOSSessionFinderConfig` class
    - Add `MaxRetryAttempts` property (default: 3)
    - Add `BucketId` property (default: "default")
    - Add `MaxSearchResults` property (default: 10)
    - Run tests and verify they pass
    - _Requirements: 2.5, 6.3, 7.1, 7.2_

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
  
  - [x] 3.3 Write tests for `SessionClaimFailedException`
    - Test exception message format
    - Test `RetryAttempts` property
    - _Requirements: 5.2_
  
  - [x] 3.4 Implement `SessionClaimFailedException`
    - Include `RetryAttempts` property
    - Provide descriptive error message
    - Run tests and verify they pass
    - _Requirements: 5.2_

- [-] 4. Implement EOSSessionFinder with TDD
  - [x] 4.1 Write tests for class skeleton
  - [x] 4.2 Create `EOSSessionFinder` class skeleton
  - [x] 4.3 Write tests for session search logic
  - [x] 4.4 Implement session search logic (simplified)
  - [x] 4.5 Write tests for session claiming logic
  - [ ] 4.6 Implement session claiming logic (requires EOS SDK - deferred to integration tests)
  - [ ] 4.7 Write tests for retry logic (requires EOS SDK - deferred to integration tests)
  - [ ] 4.8 Implement retry logic in GetSessionAsync (requires EOS SDK - deferred to integration tests)
  
  **Note**: Tasks 4.6-4.8 require actual EOS SDK integration which is complex to mock in unit tests.
  The current implementation (tasks 4.1-4.5) provides a solid foundation with proper TDD cycles.
  Full EOS SDK implementation will be completed as part of integration testing (task 7).

- [-] 5. Update dependency injection configuration with TDD
  - [x] 5.1 Write tests for DI setup
    - Test `EOSSessionCreator` registered for "create" mode
    - Test `EOSSessionFinder` registered for "find" mode
    - Test startup fails with invalid mode
    - _Requirements: 4.4, 9.2, 9.3, 9.5_
    - **TDD Status**: ✅ Completed RED (compile) → RED (assertions) → GREEN cycle
    - RED Phase 1: Tests compiled successfully
    - RED Phase 2: Tests failed with "Unable to resolve service for type ILogger" and "Unable to resolve service for type EOSSDKService"
    - GREEN Phase: All 3 tests pass after adding logging and EOS SDK service registrations
  
  - [-] 5.2 Implement DI configuration
    - Read `SessionProvider` configuration section
    - Validate configuration at startup
    - Register `EOSSessionCreator` when mode is "create"
    - Register `EOSSessionFinder` when mode is "find"
    - Register `EOSSessionFinderConfig` when mode is "find"
    - Fail fast with clear error if mode is invalid
    - Log which session provider mode is being used at startup
    - Run tests and verify they pass
    - _Requirements: 4.4, 9.1, 9.2, 9.3, 9.4, 9.5_

- [ ] 6. Create test helper for integration tests
  - [ ] 6.1 Create `EmptySessionTestHelper` class
    - Add method to create empty session without `match_id`
    - Reuse parts of `EOSSessionCreator` but skip `match_id` attribute
    - Add method to create empty session with `match_id` (for negative tests)
    - Add cleanup method to destroy test sessions
    - _Requirements: Testing infrastructure_

- [ ] 7. Write integration tests
  - [ ] 7.1 Test no sessions available
    - Verify `NoAvailableSessionsException` thrown
    - Verify error logging
    - _Requirements: 1.3, 5.1_
  
  - [ ] 7.2 Test find and claim empty sessions
    - Create N empty sessions using test helper
    - Call `GetSessionAsync` N times
    - Verify each call claims a different session
    - Verify metadata attributes set correctly
    - Verify (N+1)th call throws exception
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 3.1, 3.2, 3.3, 3.4_
  
  - [ ] 7.3 Test sessions with match_id are excluded
    - Create sessions with `match_id` already set
    - Verify `GetSessionAsync` does not return them
    - Verify exception thrown if only claimed sessions exist
    - _Requirements: 6.2_
  
  - [ ] 7.4 Test optimistic concurrency
    - Create a single empty session
    - Start two concurrent `GetSessionAsync` calls
    - Verify exactly one succeeds
    - Verify the other finds different session or throws exception
    - Verify claimed session has only one `match_id`
    - Verify both calls never return same session ID
    - _Requirements: 2.4_

- [ ] 8. Update documentation
  - [ ] 8.1 Add README section for session provider modes
    - Explain "create" mode with use cases
    - Explain "find" mode with use cases
    - Provide configuration examples for both modes
    - Include dedicated server provider extension example
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [ ] 9. Checkpoint - Ensure all tests pass
  - Run all unit tests and verify they pass
  - Run all integration tests and verify they pass
  - Verify the project builds successfully
  - Ask the user if questions arise

## Notes

- Follow TDD: Write tests first, then implement to make them pass
- The interface method rename (`CreateSessionAsync` → `GetSessionAsync`) affects existing code
- Integration tests require real EOS SDK connection
- Test helper creates sessions without `match_id` for testing
- Configuration validation happens at startup (fail-fast)
- Both implementations use the same `ISessionCreator` interface
