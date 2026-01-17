# SessionCreator Integration Tests

This document explains how to run the SessionCreator integration tests to verify that EOS sessions are being created correctly.

## Prerequisites

Before running these tests, you need to set up your EOS credentials. You can do this in two ways:

### Option 1: Using a .env File (Recommended)

Create a `.env` file in the project root directory with your EOS credentials:

```env
EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-client-id
EOS_CLIENT_SECRET=your-client-secret
```

The tests will automatically load this file if it exists.

### Option 2: Using Environment Variables

Set the environment variables directly in your shell:

```bash
# Windows (PowerShell)
$env:EOS_PRODUCT_ID="your-product-id"
$env:EOS_SANDBOX_ID="your-sandbox-id"
$env:EOS_DEPLOYMENT_ID="your-deployment-id"
$env:EOS_CLIENT_ID="your-client-id"
$env:EOS_CLIENT_SECRET="your-client-secret"

# Windows (CMD)
set EOS_PRODUCT_ID=your-product-id
set EOS_SANDBOX_ID=your-sandbox-id
set EOS_DEPLOYMENT_ID=your-deployment-id
set EOS_CLIENT_ID=your-client-id
set EOS_CLIENT_SECRET=your-client-secret

# Linux/Mac
export EOS_PRODUCT_ID="your-product-id"
export EOS_SANDBOX_ID="your-sandbox-id"
export EOS_DEPLOYMENT_ID="your-deployment-id"
export EOS_CLIENT_ID="your-client-id"
export EOS_CLIENT_SECRET="your-client-secret"
```

### Where to Find Your EOS Credentials

You can find these values in your EOS Developer Portal:
- Go to https://dev.epicgames.com/portal/
- Navigate to your product
- Go to Product Settings > Clients
- Create or use an existing client with the "Trusted Server" policy

## Running the Tests

The integration tests are marked with `Skip` by default to prevent accidental execution without proper credentials.

### Option 1: Remove the Skip Attribute

1. Open `SessionCreatorTests.cs`
2. Find the test you want to run (e.g., `CreateSessionAsync_WithValidMatch_ShouldCreateSession`)
3. Remove or comment out the `Skip` parameter:

```csharp
// Before:
[Fact(Skip = "Integration test - requires valid EOS credentials. Remove Skip attribute to run.")]

// After:
[Fact]
```

4. Run the test:

```bash
dotnet test --filter "FullyQualifiedName~SessionCreatorTests.CreateSessionAsync_WithValidMatch_ShouldCreateSession"
```

### Option 2: Run All SessionCreator Tests

```bash
# Remove Skip attributes from all tests first, then:
dotnet test --filter "FullyQualifiedName~SessionCreatorTests"
```

### Option 3: Run from Visual Studio / Rider

1. Open the test file in your IDE
2. Remove the `Skip` attribute from the test you want to run
3. Right-click on the test method
4. Select "Run Test" or "Debug Test"

## What the Tests Do

### CreateSessionAsync_WithValidMatch_ShouldCreateSession

This test:
1. Creates a match with 2 players
2. Calls `CreateSessionAsync` to create an EOS session
3. Verifies that:
   - A session ID is returned
   - The session contains the correct request IDs
   - The session contains the correct user IDs
4. Outputs the session details to the test console

**Expected Output:**
```
Creating session for match: <match-id>
Request IDs: <request-id-1>, <request-id-2>
User IDs: user-1, user-2
✓ Session created successfully!
  Session ID: <session-id>
  Request IDs: <request-id-1>, <request-id-2>
  User IDs: user-1, user-2
  Created At: <timestamp>

You can verify this session in the EOS Developer Portal:
  https://dev.epicgames.com/portal/
```

### CreateSessionAsync_WithMultiplePlayers_ShouldCreateSession

This test creates a session with 4 players to verify that the system can handle larger matches.

## Verifying in the EOS Portal

After running a test successfully:

1. Go to https://dev.epicgames.com/portal/
2. Navigate to your product
3. Go to Game Services > Sessions
4. Look for the session with the ID shown in the test output
5. Verify that:
   - The session exists
   - The session has the correct number of players
   - The session attributes contain `match_request_ids` and `match_id`

## Troubleshooting

### "EOS Platform is not initialized"

- Make sure all EOS environment variables are set correctly (either in `.env` file or as system environment variables)
- Verify your credentials are valid in the EOS Developer Portal
- Check that your client has the "Trusted Server" policy
- If using a `.env` file, ensure it's in the project root directory (same level as the solution file)

### ".env file not found"

- The `.env` file should be in the project root directory: `extend-eos-matchmaker-csharp/.env`
- Alternatively, you can set environment variables directly in your shell
- The test output will show where it's looking for the `.env` file

### "Failed to create session modification"

- Verify your EOS credentials have the correct permissions
- Check that your deployment is properly configured
- Ensure your product has the Sessions feature enabled

### "Session creation timeout"

- Check your network connection
- Verify that the EOS services are accessible from your network
- Try increasing the timeout in the test if needed

## Unit Tests (No EOS Required)

The following tests don't require EOS credentials and will run automatically:

- `CreateSessionAsync_WithNullMatch_ShouldThrowArgumentNullException`
- `CreateSessionAsync_WithEmptyRequests_ShouldThrowArgumentException`
- `CreateSessionAsync_WithNullRequests_ShouldThrowArgumentException`

These tests verify input validation without making actual EOS API calls.
