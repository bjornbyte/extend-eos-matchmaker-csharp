---
inclusion: always
---

# Testing Standards

## Unit Testing Requirements

When implementing any feature task, you MUST write unit tests alongside the implementation:

### Test-Driven Development (TDD) Workflow

Follow this Red-Green-Refactor cycle for all implementation tasks:

1. **Write a test** for the next bit of functionality
2. **Write just enough code** to make it compile
3. **Run the test** and verify it fails in the expected way (RED)
4. **Write just enough code** to make the test pass (GREEN)
5. **Refactor** as needed to keep the code clean and concise
6. **Repeat** with the next test until sufficient functionality and test coverage are achieved

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

Before marking any task as complete, you MUST:
1. Run a full project build and verify it succeeds
2. Run all tests and verify they all pass
3. Fix any compilation errors or test failures before proceeding

If other tasks have introduced compilation errors or test failures, you MUST fix them as part of completing your current task. The project must always be in a working state.
