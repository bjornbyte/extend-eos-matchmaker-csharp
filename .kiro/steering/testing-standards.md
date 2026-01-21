---
inclusion: always
---

# Testing Standards

## CRITICAL: TDD is MANDATORY, Not Optional

**Test-Driven Development (TDD) is required for ALL implementation tasks.**

The RED phase is not optional - it validates that:
- Your tests actually test something
- Your tests fail for the right reasons  
- You're not getting false positives
- The test framework is working correctly

**If you skip the RED phase, you are not doing TDD.**

## Unit Testing Requirements

When implementing any feature task, you MUST write unit tests alongside the implementation:

### Test-Driven Development (TDD) Workflow

**For EVERY task involving code implementation, you MUST follow these steps IN ORDER:**

1. **Write the test first** - Create test file with test cases for the functionality
2. **RED Phase 1: Make it compile** - Write just enough implementation code (stubs, empty methods, default returns) to make the test compile
   - Run the test and verify it compiles but doesn't pass yet
   - This creates the minimal API surface needed by the test
3. **RED Phase 2: Verify assertions fail correctly** - Execute tests and **VERIFY they FAIL in the expected way**
   - The test must run and fail due to incorrect behavior, not compilation errors
   - Document the failure message to confirm it's failing for the right reason
   - Verify the assertions are actually being checked (not skipped or bypassed)
   - This proves your test actually tests something meaningful
   - If tests don't fail as expected, the test is broken - fix the test
4. **Write minimal implementation** - Write just enough code to make tests pass
5. **GREEN Phase: Run the test again** - Execute tests and **VERIFY they PASS**
6. **Refactor if needed** - Clean up code while keeping tests green
7. **Repeat** with the next test until sufficient functionality and test coverage are achieved

**YOU MUST NOT:**
- ❌ Write tests and implementation together
- ❌ Skip the assertion validation RED phase
- ❌ Proceed to the next task without verifying both RED phases and GREEN phase
- ❌ Assume tests will fail without actually running them
- ❌ Write full implementation before seeing assertions fail correctly
- ❌ Accept a RED phase where tests fail to compile as sufficient validation

**YOU MUST:**
- ✅ Always see RED (compile) → RED (assertions fail correctly) → GREEN
- ✅ Document what the assertion failure looked like in RED Phase 2
- ✅ Verify assertions are actually being checked and failing as expected
- ✅ Write minimal stub code first to make tests compile
- ✅ Then write minimal implementation to make assertions pass
- ✅ Verify tests pass after implementation

This approach ensures:
- Tests are written first, verifying requirements before implementation
- Implementation is driven by tests, not the other way around
- Each test compiles first with stub implementations
- Each test then fails with meaningful assertion errors, proving it actually tests something
- Assertions are validated to fail in the expected way before writing real implementation
- Code is only written to satisfy tests, avoiding over-engineering
- Code remains clean, maintainable, and well-structured through continuous refactoring

### Requirements
- Write unit tests for all new functions, classes, and services
- Test specific examples that demonstrate correct behavior
- Test important edge cases (empty inputs, boundary values, null handling)
- Test error conditions and exception handling
- Use descriptive test names that explain what is being tested

### TDD Verification Checkpoints

Before marking any implementation task complete, you MUST confirm:
- [ ] Tests were written BEFORE implementation
- [ ] RED Phase 1: Stub code was written to make tests compile
- [ ] RED Phase 2: Tests were run and assertions FAILED in the expected way (documented)
- [ ] You verified the assertions were actually being checked (not bypassed)
- [ ] Implementation was written to make tests pass
- [ ] GREEN Phase: Tests were run again and PASSED (verified)
- [ ] You can explain what the assertion failure looked like in RED Phase 2

### Common TDD Mistakes to Avoid

**MISTAKES:**
- ❌ Writing tests and implementation in the same step
- ❌ Not running tests before implementing
- ❌ Assuming tests will fail without verifying
- ❌ Treating compilation errors as sufficient RED validation
- ❌ Skipping the assertion validation step (RED Phase 2)
- ❌ Writing full implementation without first seeing assertions fail
- ❌ Accepting tests that pass immediately after compilation (no RED Phase 2)
- ❌ Skipping the RED phases to "save time"

**CORRECT APPROACH:**
- ✅ Always see RED (compile) → RED (assertions fail) → GREEN
- ✅ Write stub code first to make tests compile
- ✅ Run tests and verify assertions fail in the expected way
- ✅ Document the assertion failure message from RED Phase 2
- ✅ Write minimal implementation to make assertions pass
- ✅ Verify each phase explicitly

### Test Organization
- Co-locate tests with source files using `.Tests` project
- Follow naming convention: `{ClassName}Tests.cs`
- Group related tests using test classes or nested classes

### Coverage Focus
- Core business logic (MatchPool, MatchMaker, MatchmakingService)
- Edge cases (empty pool, single request, exact match_size)
- Error paths (invalid IDs, duplicate requests, cancellation scenarios)
- Integration points (mocked dependencies)

### Testing Framework
- Use xUnit for unit tests
- Use Moq or NSubstitute for mocking dependencies
- Keep tests fast and isolated

## Property-Based Testing (Optional)

Property tests marked with `*` in task lists are optional and can be skipped for faster MVP delivery:

- Use FsCheck when implementing property tests
- Minimum 100 iterations per property test
- Tag each test with: `Feature: {feature_name}, Property {number}: {property_text}`
- Property tests validate universal correctness properties across all inputs

## Testing Balance

- **Unit tests**: Focus on specific examples, edge cases, and error conditions
- **Property tests**: Validate universal properties across randomized inputs
- Both are complementary - unit tests catch concrete bugs, property tests verify general correctness
- Avoid writing too many unit tests - focus on critical paths and edge cases

## Task Completion Criteria

**CRITICAL RULE**: A task can NEVER be considered complete if:
1. The entire project does not build successfully (no compilation errors)
2. All tests do not pass (no failing tests)
3. The TDD Red-Green-Refactor cycle was not followed

Before marking any task as complete, you MUST:
1. Confirm tests were written FIRST and failed (RED phase)
2. Confirm implementation made tests pass (GREEN phase)
3. Run a full project build and verify it succeeds
4. Run all tests and verify they all pass
5. Fix any compilation errors or test failures before proceeding

**When updating task status:**
- Setting to "in_progress": State "Starting TDD cycle - writing tests first"
- Setting to "completed": State "Completed TDD cycle - verified RED (compile) → RED (assertions) → GREEN phases" and briefly describe what the assertion failure looked like in RED Phase 2

If other tasks have introduced compilation errors or test failures, you MUST fix them as part of completing your current task. The project must always be in a working state.

## E2E Testing

**Running E2E Tests:**
- E2E tests run against a live service instance
- If you make changes to the service implementation, you MUST restart the service before running E2E tests
- To restart the service: `docker compose down && docker compose up --build`
- E2E tests are NOT backward compatible - they test the current implementation
- Always rebuild the Docker image when testing new features
