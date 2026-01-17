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
2. **Run the test** - Execute tests and **VERIFY they FAIL** (RED phase)
   - If tests don't fail, the test is broken - fix the test
   - Document the failure message to confirm it's failing for the right reason
   - This proves your test actually tests something
3. **Write minimal implementation** - Write just enough code to make tests pass
4. **Run the test again** - Execute tests and **VERIFY they PASS** (GREEN phase)
5. **Refactor if needed** - Clean up code while keeping tests green
6. **Repeat** with the next test until sufficient functionality and test coverage are achieved

**YOU MUST NOT:**
- ❌ Write tests and implementation together
- ❌ Skip the RED phase
- ❌ Proceed to the next task without verifying both RED and GREEN phases
- ❌ Assume tests will fail without actually running them
- ❌ Write implementation code before seeing tests fail

**YOU MUST:**
- ✅ Always see RED before GREEN
- ✅ Document what the failure looked like in the RED phase
- ✅ Write minimal code to pass tests
- ✅ Verify tests pass after implementation

This approach ensures:
- Tests are written first, verifying requirements before implementation
- Implementation is driven by tests, not the other way around
- Each test fails initially, proving it actually tests something
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
- [ ] Tests were run and FAILED initially (RED phase documented)
- [ ] Implementation was written to make tests pass
- [ ] Tests were run again and PASSED (GREEN phase verified)
- [ ] You can explain what the failure looked like in the RED phase

### Common TDD Mistakes to Avoid

**MISTAKES:**
- ❌ Writing tests and implementation in the same step
- ❌ Not running tests before implementing
- ❌ Assuming tests will fail without verifying
- ❌ Writing implementation code that "makes it compile" without seeing RED first
- ❌ Skipping the RED phase to "save time"

**CORRECT APPROACH:**
- ✅ Always see RED before GREEN
- ✅ Document the failure message from RED phase
- ✅ Write minimal code to pass tests
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
- Setting to "completed": State "Completed TDD cycle - verified RED then GREEN phases" and briefly describe what the RED failure looked like

If other tasks have introduced compilation errors or test failures, you MUST fix them as part of completing your current task. The project must always be in a working state.
