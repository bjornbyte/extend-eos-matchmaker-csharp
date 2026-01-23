# Implementation Plan: Project Simplification

## Overview

This task list implements the simplification of the Simple EOS Matchmaking Service to make it clearer and more approachable for game developers. The focus is on improving documentation, clarifying extension points, and removing unnecessary abstractions while maintaining production-ready quality.

## Tasks

- [-] 1. Make CompletedRequestStore mandatory
  - Remove nullable marker from `ICompletedRequestStore` parameter in `MatchmakingService` constructor
  - Remove nullable marker from `ICompletedRequestStore` parameter in `MatchMaker` constructor
  - Remove all `if (_completedRequestStore != null)` null checks in `MatchmakingService.cs`
  - Remove all `if (_completedRequestStore != null)` null checks in `MatchMaker.cs`
  - _Requirements: 7.1_

- [ ] 2. Remove IMatchMaker interface
  - [ ] 2.1 Remove `IMatchMaker` interface definition from `MatchMaker.cs`
    - Keep the concrete `MatchMaker` class
    - _Requirements: 2.1_
  
  - [ ] 2.2 Update dependency injection in `Program.cs`
    - Change `.AddSingleton<IMatchMaker, MatchMaker>()` to `.AddSingleton<MatchMaker>()`
    - Change `.AddHostedService(sp => sp.GetRequiredService<IMatchMaker>() as MatchMaker)` to `.AddHostedService<MatchMaker>()`
    - _Requirements: 2.1_

- [ ] 3. Add XML documentation to extension point interfaces
  - [ ] 3.1 Add XML docs to `INotifier` interface
    - Mark as application-level extension point
    - Explain when to implement custom notifier
    - Include example scenarios (webhooks, push notifications, message queues)
    - _Requirements: 13.1, 13.2_
  
  - [ ] 3.2 Add XML docs to `ISessionCreator` interface
    - Mark as application-level extension point
    - Explain the two modes (create vs find)
    - Reference architecture guide for decision tree
    - _Requirements: 13.1, 13.2_
  
  - [ ] 3.3 Add XML docs to `ISessionOwnerNotifier` interface
    - Mark as application-level extension point
    - Explain it's only for find mode
    - Provide example notification mechanisms
    - _Requirements: 13.1, 13.2_
  
  - [ ] 3.4 Add XML docs to `IMatchPool` interface
    - Mark as infrastructure-level extension point
    - Explain in-memory limitations (single-instance only)
    - Note when to implement distributed version (Redis, database)
    - _Requirements: 13.1, 13.2_
  
  - [ ] 3.5 Add XML docs to `ICompletedRequestStore` interface
    - Mark as infrastructure-level extension point
    - Explain durability considerations (lost on restart)
    - Note when to implement persistent version (database)
    - _Requirements: 13.1, 13.2_
  
  - [ ] 3.6 Add XML docs to `IClaimedSessionsCache` interface
    - Mark as infrastructure-level extension point
    - Explain multi-instance considerations
    - Note when to use distributed cache (Redis)
    - _Requirements: 13.1, 13.2_

- [ ] 4. Add TODO comments to stub implementations
  - [ ] 4.1 Add TODO comment to `LoggingNotifier` class
    - Add comment: "// TODO: Replace with your game-specific notification implementation (webhook, push notification, etc.)"
    - _Requirements: 2.4, 13.3_
  
  - [ ] 4.2 Add TODO comment to `StubSessionOwnerNotifier` class
    - Add comment: "// TODO: Replace with your notification mechanism to game servers (HTTP, gRPC, message queue, etc.)"
    - _Requirements: 2.4, 13.3_

- [ ] 5. Add inline comments to MatchMaker for customization
  - Add comment at top of `TryMatchAsync()` method explaining FIFO algorithm
  - Add comment before request selection explaining where to add skill-based matching
  - Add comment before match creation explaining where to add metadata filtering
  - Add inline examples showing common modifications (skill-based, region-based)
  - _Requirements: 8.1, 8.4_

- [ ] 6. Add inline comments to Program.cs for extension points
  - [ ] 6.1 Add comment for IMatchPool registration
    - Explain it's infrastructure-level extension point
    - Note default is in-memory (single-instance only)
    - _Requirements: 13.5_
  
  - [ ] 6.2 Add comment for ICompletedRequestStore registration
    - Explain it's infrastructure-level extension point
    - Note default is in-memory (no durability across restarts)
    - _Requirements: 13.5_
  
  - [ ] 6.3 Add comment for INotifier registration
    - Explain it's application-level extension point
    - Note default just logs to console
    - _Requirements: 13.5_
  
  - [ ] 6.4 Add comment for ISessionCreator registration
    - Explain it's application-level extension point
    - Note the two modes (create vs find)
    - _Requirements: 13.5_
  
  - [ ] 6.5 Add comment for ISessionOwnerNotifier registration (find mode)
    - Explain it's application-level extension point
    - Note it's only used in find mode
    - _Requirements: 13.5_

- [ ] 7. Checkpoint - Ensure all tests pass
  - Run `dotnet test src/extend-service-extension-server.sln`
  - Verify all unit tests pass after code changes
  - Ask the user if questions arise

- [ ] 8. Consolidate README.md
  - [ ] 8.1 Reduce README to under 300 lines
    - Keep only: overview, key features, quick start, links to detailed docs
    - Remove detailed explanations (move to architecture.md)
    - Remove duplicate configuration examples (link to setup.md)
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [ ] 8.2 Add clear section links
    - Add prominent links to all /docs files at top
    - Use progressive disclosure pattern (brief + link to details)
    - _Requirements: 1.2_

- [ ] 9. Enhance docs/architecture.md
  - [ ] 9.1 Add "Extension Points" section
    - Create subsection: "Application-Level Extension Points"
    - Create subsection: "Infrastructure-Level Extension Points"
    - List all extension point interfaces with descriptions
    - _Requirements: 13.4_
  
  - [ ] 9.2 Add "Core Customization Points" section
    - Explain how to modify MatchMaker directly
    - Provide inline code examples for common modifications
    - _Requirements: 12.1, 12.2_
  
  - [ ] 9.3 Add deployment considerations section
    - Explain single-instance vs multi-instance implications
    - Explain in-memory vs distributed storage trade-offs
    - Provide decision criteria for when to customize infrastructure
    - _Requirements: 4.1, 13.6_
  
  - [ ] 9.4 Add complete working examples
    - Webhook notifier example (HTTP POST)
    - Redis-based MatchPool example
    - Database-backed CompletedRequestStore example
    - Skill-based MatchMaker modification example
    - _Requirements: 12.2, 12.4_
  
  - [ ] 9.5 Add session provider decision tree
    - Create clear decision tree for create vs find mode
    - Explain use cases for each mode
    - _Requirements: 4.5_
  
  - [ ] 9.6 Consolidate technical details
    - Ensure all technical explanations are in architecture.md
    - Remove duplicates from other files
    - _Requirements: 1.1, 1.5_

- [ ] 10. Improve docs/setup.md
  - [ ] 10.1 Separate required from optional configuration
    - Create "Required Configuration" section (credentials only)
    - Create "Optional Configuration" section (tuning parameters)
    - _Requirements: 3.3_
  
  - [ ] 10.2 Add deployment scenarios section
    - Single-instance deployment (default configuration)
    - Multi-instance deployment (what to customize)
    - _Requirements: 3.5_
  
  - [ ] 10.3 Explain configuration defaults
    - Document default value for each optional parameter
    - Explain when and why to change each setting
    - _Requirements: 3.1, 3.5_

- [ ] 11. Enhance docs/operations.md
  - [ ] 11.1 Add observability setup guide
    - Separate basic logging from advanced metrics/tracing
    - Provide step-by-step setup for Prometheus and Zipkin
    - _Requirements: 11.2, 11.3_
  
  - [ ] 11.2 Add multi-instance deployment considerations
    - Explain which components need distributed implementations
    - Link to architecture guide examples
    - _Requirements: 11.6_

- [ ] 12. Verify documentation consistency
  - [ ] 12.1 Check for duplicate explanations
    - Search for concepts explained in multiple files
    - Consolidate to single source of truth
    - Add cross-references
    - _Requirements: 1.1, 1.2_
  
  - [ ] 12.2 Verify all cross-references work
    - Test all links between documentation files
    - Verify anchor links work correctly
    - _Requirements: 1.2_
  
  - [ ] 12.3 Ensure consistent terminology
    - Use same terms throughout all documentation
    - Update glossary if needed
    - _Requirements: 1.5_

- [ ] 13. Final validation
  - [ ] 13.1 Run all tests
    - Execute `dotnet test src/extend-service-extension-server.sln`
    - Verify all tests pass
    - _Requirements: 5.1_
  
  - [ ] 13.2 Build and run service locally
    - Execute `docker compose up --build`
    - Verify service starts without errors
    - Test basic matchmaking flow
    - _Requirements: 3.1_
  
  - [ ] 13.3 Review extension point clarity
    - Verify all extension points are clearly marked
    - Verify stub implementations have TODO comments
    - Verify Program.cs has inline comments
    - _Requirements: 13.1, 13.3, 13.5_

## Notes

- Focus on documentation improvements and code clarity
- No breaking changes to API or configuration
- All interfaces preserved (except IMatchMaker)
- Maintain production-ready observability
- Examples should be complete and working
