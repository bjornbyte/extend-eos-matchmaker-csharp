# Design Document: Project Simplification

## Overview

This design outlines the approach to simplify the Simple EOS Matchmaking Service to make it clearer, more approachable, and easier for game developers to understand and customize. The goal is to reduce cognitive load while maintaining clean architecture, production-ready observability, and clear extension points.

The simplification focuses on three main areas:
1. **Documentation** - Eliminate duplication, improve clarity, provide better examples
2. **Code Structure** - Remove unnecessary abstractions, clarify extension points, make optional features truly optional or mandatory
3. **Developer Experience** - Clear inline comments, better examples, obvious customization points

## Architecture

### Current State Analysis

The current codebase has:
- **Strengths**: Clean separation of concerns, good observability, clear domain models, extension points for customization
- **Areas for Improvement**: Some unnecessary interface abstractions, unclear distinction between extension points and implementation details, documentation duplication, nullable dependencies that are always used

### Design Principles

1. **Clarity over Cleverness**: Prefer straightforward code over clever abstractions
2. **Extension Points are Sacred**: Keep interfaces that developers should implement (INotifier, ISessionCreator, ISessionOwnerNotifier)
3. **Remove Unnecessary Abstractions**: Eliminate interfaces with single implementations that aren't meant for customization
4. **Mandatory is Better than Optional**: If a feature is always used, make it mandatory and simplify the code
5. **Documentation as Code**: Use inline comments to guide developers at customization points

## Components and Interfaces

### Component Classification

We classify components into three categories:

1. **Core Components** (concrete classes, developers modify directly):
   - `MatchmakingService` - gRPC service implementation
   - `MatchMaker` - Background matching service (developers customize the matching algorithm directly)

2. **Extension Points** (keep interfaces, provide default implementations):
   
   **Application-Level Extension Points:**
   - `IPlayerNotifier` (renamed from `INotifier`) - Notification mechanism when matches are found (notifies players)
   - `ISessionCreator` - Session provider abstraction (create vs find modes)
   - `ISessionOwnerNotifier` - Notification for session owners in find mode (notifies game servers)
   
   **Infrastructure-Level Extension Points:**
   - `IMatchPool` - Storage for pending match requests (default: in-memory, consider Redis/database for multi-instance)
   - `ICompletedRequestStore` - Storage for completed requests (default: in-memory, consider Redis/database for durability)
   - `IClaimedSessionsCache` - Session claiming cache for find mode (default: in-memory, consider Redis for multi-instance)

3. **Infrastructure** (keep as-is, well-established patterns):
   - gRPC interceptors (Authorization, Exception Handling, Debug Logging)
   - EOS SDK service wrapper
   - Configuration classes

### Interface Decisions

#### Keep All Interfaces (Extension Points)

We keep all service interfaces as they represent potential customization points for different deployment scenarios.

**Application-Level Extension Points:**

**IPlayerNotifier** (renamed from INotifier) - Keep as extension point
- **Purpose**: Developers implement custom notification mechanisms to notify players when matches are found (webhooks, push notifications, etc.)
- **Current Implementation**: `LoggingPlayerNotifier` (renamed from `LoggingNotifier`, default that logs to console)
- **Rationale for Rename**: Creates clear parallel with `ISessionOwnerNotifier` - makes it immediately obvious that this notifies players while the other notifies session owners (game servers)
- **Documentation Needs**: Mark as extension point, provide webhook example, explain when to customize

**ISessionCreator** - Keep as extension point
- **Purpose**: Abstraction for session provider modes (create vs find)
- **Current Implementations**: `EOSSessionCreator`, `EOSSessionFinder`
- **Documentation Needs**: Explain the two modes clearly, provide decision tree

**ISessionOwnerNotifier** - Keep as extension point
- **Purpose**: Notify game servers when their session is claimed (find mode only)
- **Current Implementation**: `StubSessionOwnerNotifier` (logs only)
- **Documentation Needs**: Mark as extension point, provide HTTP/webhook example, explain it's only for find mode

**Infrastructure-Level Extension Points:**

**IMatchPool** - Keep as extension point
- **Purpose**: Storage for pending match requests
- **Current Implementation**: `MatchPool` (in-memory, thread-safe)
- **When to Customize**: 
  - Multi-instance deployments (use Redis or distributed cache)
  - High availability requirements (persist to database)
  - Large-scale matchmaking (external queue system)
- **Documentation Needs**: Explain in-memory limitations, provide Redis example, discuss trade-offs

**ICompletedRequestStore** - Keep as extension point
- **Purpose**: Storage for completed requests with retention period
- **Current Implementation**: `CompletedRequestStore` (in-memory, thread-safe)
- **When to Customize**:
  - Service restart durability (persist to database)
  - Multi-instance deployments (use Redis or distributed cache)
  - Long retention periods (use database with indexing)
- **Documentation Needs**: Explain in-memory limitations, provide Redis/database examples, discuss retention strategies

**IClaimedSessionsCache** - Keep as extension point
- **Purpose**: Cache for claimed sessions to prevent race conditions (find mode only)
- **Current Implementation**: `InMemoryClaimedSessionsCache`
- **When to Customize**: Multi-instance deployments (use Redis with TTL)
- **Documentation Needs**: Explain purpose, note that distributed cache is needed for multi-instance

#### Remove Interface (Core Logic - Modify Directly)

**IMatchMaker** → Use concrete `MatchMaker` class
- **Rationale**: Developers customize the matching algorithm by modifying the `MatchMaker` class directly, not by implementing an interface
- **Customization Approach**: Developers edit `TryMatchAsync()` method to implement their matching logic (skill-based, region-based, etc.)
- **Impact**: Simplifies the code - no interface layer needed
- **Documentation Needs**: Add inline comments in `MatchMaker` explaining customization points, provide examples of common modifications (skill-based matching, metadata filtering)

## Data Models

No changes needed to data models. They are clear and well-structured:
- `MatchRequest` - Individual matchmaking request
- `Match` - Collection of matched requests
- `SessionInfo` - EOS session information

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Documentation Consistency
*For any* technical concept explained in the documentation, it SHALL appear in exactly one primary location with all other references linking to that location.

**Validates: Requirements 1.1, 1.2**

### Property 2: Extension Point Marking
*For any* interface that is an extension point, the interface SHALL include XML documentation comments explicitly stating it is an extension point and explaining when to implement it.

**Validates: Requirements 13.1, 13.2**

### Property 3: Stub Implementation Marking
*For any* stub implementation of an extension point, the class SHALL include a TODO comment directing developers to replace it with their game-specific implementation.

**Validates: Requirements 2.4, 13.3**

### Property 4: Mandatory Core Dependencies
*For any* core service that is always registered and used AND has no deployment-specific customization needs, the dependency SHALL NOT be nullable in constructor parameters.

**Validates: Requirements 7.1, 7.5**

**Note**: Storage interfaces (IMatchPool, ICompletedRequestStore) remain non-nullable but keep their interfaces for infrastructure customization.

### Property 5: Interface Justification
*For any* interface in the Services directory, it SHALL either be an extension point with documented customization scenarios OR have multiple implementations OR be removed.

**Validates: Requirements 2.1, 2.2**

### Property 6: Configuration Defaults
*For any* configuration parameter that is not a credential, the system SHALL provide a working default value.

**Validates: Requirements 3.1, 3.2**

### Property 7: Observability Presence
*For all* key decision points in the matching algorithm (request submission, match creation, session creation, expiration), the code SHALL include structured log messages.

**Validates: Requirements 11.1, 11.4**

### Property 8: Extension Point Documentation with Deployment Scenarios
*For any* extension point interface, the Architecture Guide SHALL include:
1. A complete working code example showing how to implement a custom version
2. Clear explanation of when and why to customize it
3. For infrastructure extension points (storage, caching), examples of distributed implementations

**Validates: Requirements 12.2, 13.6**

### Property 9: Inline Customization Guidance
*For any* extension point registration in Program.cs, there SHALL be an inline comment explaining what the extension point is for and when to customize it.

**Validates: Requirements 8.5, 13.5**

### Property 10: Session Provider Mode Clarity
*For any* session provider mode (create or find), the documentation SHALL include a clear decision tree explaining when to use that mode.

**Validates: Requirements 4.1, 4.5**

## Error Handling

No changes to error handling approach. Current implementation is clear:
- Use standard gRPC status codes
- Provide descriptive error messages
- Log errors with context

Keep the custom exception types in `MatchmakingExceptions.cs` as they provide clear semantics.

## Testing Strategy

### Unit Testing

Focus unit tests on:
1. **Core matching logic** - FIFO algorithm, expiration, match creation
2. **Request lifecycle** - State transitions (pending → matched/expired/cancelled)
3. **Thread safety** - Concurrent access to MatchPool and CompletedRequestStore
4. **Edge cases** - Empty pool, single request, exact match size

### Integration Testing

Keep existing integration tests that validate:
1. **End-to-end matchmaking flow** - Submit → Match → Status query
2. **EOS session creation** - Actual EOS SDK integration
3. **Request expiration** - Time-based cleanup

### Testing Simplifications

- Remove overly complex mocking scenarios
- Focus on realistic test data
- Ensure tests demonstrate usage patterns clearly

## Implementation Plan

### Phase 1: Code Simplification and Extension Point Marking

1. **Make CompletedRequestStore Mandatory (Keep Interface)**
   - Remove `?` nullable marker from `ICompletedRequestStore` parameters
   - Remove all `if (_completedRequestStore != null)` checks
   - Keep the interface for infrastructure customization
   - Add XML documentation explaining when to implement custom storage

2. **Mark All Extension Points Clearly**
   
   **Application-Level Extension Points:**
   - Add XML documentation to `IPlayerNotifier` (renamed from `INotifier`) marking it as an extension point
   - Add XML documentation to `ISessionCreator` marking it as an extension point
   - Add XML documentation to `ISessionOwnerNotifier` marking it as an extension point
   - Add TODO comments to `LoggingPlayerNotifier` (renamed from `LoggingNotifier`) and `StubSessionOwnerNotifier`
   
   **Infrastructure-Level Extension Points:**
   - Add XML documentation to `IMatchPool` explaining in-memory limitations and when to customize
   - Add XML documentation to `ICompletedRequestStore` explaining durability considerations
   - Add XML documentation to `IClaimedSessionsCache` explaining multi-instance considerations

3. **Remove IMatchMaker Interface**
   - Remove `IMatchMaker` interface definition
   - Change dependency injection to use concrete `MatchMaker` class
   - Update constructor parameters where `IMatchMaker` is injected

4. **Add Inline Customization Comments to MatchMaker**
   - Add comments in `MatchMaker.TryMatchAsync()` explaining the FIFO algorithm
   - Mark customization points (where to add skill-based matching, metadata filtering, etc.)
   - Add examples in comments showing common modifications
   - Note that developers should modify this class directly for custom matching logic

5. **Add Inline Customization Comments**
   - Add comments in `Program.cs` at all extension point registrations
   - Explain which are application-level vs infrastructure-level
   - Add comments in default implementations explaining they're suitable for single-instance deployments
   - Add comments at key decision points in matching algorithm

### Phase 2: Documentation Simplification

1. **Consolidate README.md**
   - Remove detailed explanations, keep only overview and quick start
   - Add clear links to detailed docs
   - Reduce to under 300 lines

2. **Enhance Architecture Guide**
   - Add "Extension Points" section with two subsections:
     - **Application-Level Extension Points** (INotifier, ISessionCreator, ISessionOwnerNotifier)
     - **Infrastructure-Level Extension Points** (IMatchPool, ICompletedRequestStore, IClaimedSessionsCache)
   - Add "Core Customization Points" section explaining how to modify MatchMaker directly
   - Add decision tree for session provider modes
   - Add complete working examples for each extension point:
     - Webhook notifier
     - Redis-based MatchPool for multi-instance deployments
     - Database-backed CompletedRequestStore for durability
   - Add MatchMaker customization examples:
     - Skill-based matching (ELO/MMR)
     - Region-based matching (metadata filtering)
     - Team balancing algorithms
   - Add deployment considerations section explaining:
     - Single-instance vs multi-instance implications
     - In-memory vs distributed storage trade-offs
     - When to customize infrastructure components
   - Consolidate all technical details here (single source of truth)

3. **Improve Setup Guide**
   - Separate required credentials from optional tuning
   - Add "Quick Start" vs "Advanced Configuration" sections
   - Provide better defaults explanation

4. **Enhance Operations Guide**
   - Add clear observability setup guide
   - Separate basic logging from advanced metrics/tracing
   - Add troubleshooting decision tree

5. **Create Extension Point Examples**
   
   **Application-Level Examples:**
   - Webhook player notifier example (HTTP POST to notify players)
   - Message queue player notifier example (RabbitMQ/SQS)
   - Custom session owner notifier example
   
   **Infrastructure-Level Examples:**
   - Redis-based MatchPool for multi-instance deployments
   - Database-backed CompletedRequestStore (SQL Server/PostgreSQL)
   - Distributed ClaimedSessionsCache using Redis
   
   **Core Customization Examples:**
   - Skill-based matching in MatchMaker (ELO/MMR algorithm)
   - Region-based matching with metadata filtering
   - Team balancing algorithm
   
   - Add all examples to Architecture Guide with full code and deployment notes

### Phase 3: Configuration Simplification

1. **Review Configuration Options**
   - Ensure all non-credential options have defaults
   - Document when and why to change each setting
   - Separate required vs optional in documentation

2. **Improve Configuration Documentation**
   - Add decision criteria for each configuration option
   - Provide common configuration patterns
   - Explain trade-offs clearly

### Phase 4: Testing and Validation

1. **Review Test Suite**
   - Ensure tests demonstrate core functionality
   - Remove overly complex test scenarios
   - Add tests that serve as usage examples

2. **Validate Documentation**
   - Ensure no duplication across files
   - Verify all cross-references work
   - Test all code examples

3. **Developer Experience Testing**
   - Have someone unfamiliar with the code review it
   - Verify extension points are obvious
   - Ensure customization examples work

## File Structure Changes

### Files to Modify

**Services/**
- `MatchPool.cs` - Keep `IMatchPool` interface, add XML docs about in-memory limitations and multi-instance considerations
- `CompletedRequestStore.cs` - Keep `ICompletedRequestStore` interface, add XML docs about durability and retention
- `MatchMaker.cs` - Remove `IMatchMaker` interface, add inline comments explaining FIFO algorithm and customization points
- `Notifier.cs` - Rename `INotifier` to `IPlayerNotifier`, rename `LoggingNotifier` to `LoggingPlayerNotifier`, add XML docs marking as extension point, add TODO comment
- `SessionCreator.cs` - Add XML docs marking `ISessionCreator` as extension point
- `SessionOwnerNotifier.cs` - Add XML docs marking `ISessionOwnerNotifier` as extension point, add TODO to stub
- `MatchmakingService.cs` - Remove nullable marker from `ICompletedRequestStore`, remove null checks, update `IPlayerNotifier` reference
- `ClaimedSessionsCache.cs` - Add XML docs about multi-instance considerations

**Program.cs**
- Update `IMatchMaker` registration to use concrete `MatchMaker` class
- Update `INotifier` registration to use `IPlayerNotifier`
- Add inline comments at all extension point registrations
- Distinguish between application-level and infrastructure-level extension points
- Note default implementations are suitable for single-instance deployments

**Documentation/**
- `README.md` - Consolidate, reduce length, add clear links, mention deployment considerations
- `docs/architecture.md` - Add Extension Points section (application vs infrastructure), add Core Customization section for MatchMaker, add deployment considerations, add examples
- `docs/setup.md` - Separate required from optional configuration, add deployment scenarios section
- `docs/operations.md` - Add observability setup guide, add multi-instance deployment considerations

### Files to Keep As-Is

**Classes/** - Infrastructure components are well-structured
**Model/** - Domain models are clear
**Protos/** - gRPC definitions are correct
**Tests/** - Will review but structure is good

## Success Criteria

The simplification is successful when:

1. **A new developer can understand the core matchmaking flow in under 30 minutes**
2. **Extension points are obvious and categorized** (application-level vs infrastructure-level)
3. **Deployment implications are clear** - developers understand single-instance vs multi-instance trade-offs
4. **Documentation has no duplication** - each concept explained once
5. **Infrastructure customization is well-documented** - clear examples for Redis, databases, distributed caching
6. **Observability is maintained** - all metrics, tracing, and logging still work
7. **Examples are complete and working** - developers can copy-paste and adapt for their deployment needs

## Next Steps

After design approval:
1. Create detailed task list for implementation
2. Prioritize tasks (code changes, then documentation)
3. Execute changes incrementally
4. Validate with developer testing
5. Update all documentation to reflect changes
