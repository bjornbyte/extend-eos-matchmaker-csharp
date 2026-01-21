# Matchmaking Service Test Script
# Tests the service running on localhost:8080 according to testing_guide.md

# Use 127.0.0.1 instead of localhost because Docker port mapping only exposes IPv4
$baseUrl = "http://127.0.0.1:8000/matchmaking"
$testUserId1 = "test-user-1"
$testUserId2 = "test-user-2"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Matchmaking Service Test Suite" -ForegroundColor Cyan
Write-Host "Service URL: $baseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Helper function to make API calls
function Invoke-MatchmakingApi {
    param(
        [string]$Method,
        [string]$Endpoint,
        [string]$UserId,
        [string]$Body = $null
    )
    
    $uri = "$baseUrl$Endpoint"
    $headers = @{
        "Content-Type" = "application/json"
        "grpc-metadata-user-id" = $UserId
    }
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -Body $Body -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -ErrorAction Stop
        }
        
        $stopwatch.Stop()
        
        return @{ 
            Success = $true
            Data = $response
            Duration = $stopwatch.Elapsed.TotalMilliseconds
        }
    } catch {
        $stopwatch.Stop()
        
        return @{ 
            Success = $false
            Error = $_.Exception.Message
            StatusCode = $_.Exception.Response.StatusCode.value__
            Duration = $stopwatch.Elapsed.TotalMilliseconds
        }
    }
}

# Test 1: Submit Match Request (Player 1)
Write-Host "`n[TEST 1] Submit Match Request - Player 1" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$requestBody1 = @{
    metadata = @{
        region = "us-west"
        skill_level = "intermediate"
    }
} | ConvertTo-Json

$result1 = Invoke-MatchmakingApi -Method "POST" -Endpoint "/v1/request" -UserId $testUserId1 -Body $requestBody1

if ($result1.Success) {
    $requestId1 = $result1.Data.requestId
    Write-Host "✓ SUCCESS: Request submitted" -ForegroundColor Green
    Write-Host "  Request ID: $requestId1" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result1.Duration, 2))ms" -ForegroundColor Gray
} else {
    Write-Host "✗ FAILED: $($result1.Error)" -ForegroundColor Red
    Write-Host "  Status Code: $($result1.StatusCode)" -ForegroundColor Red
    Write-Host "  Duration: $([math]::Round($result1.Duration, 2))ms" -ForegroundColor Gray
    exit 1
}

# Test 2: Check Status (Should be PENDING)
Write-Host "`n[TEST 2] Check Match Status - Player 1 (Should be PENDING)" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

Start-Sleep -Milliseconds 500

$result2 = Invoke-MatchmakingApi -Method "GET" -Endpoint "/v1/request/$requestId1" -UserId $testUserId1

if ($result2.Success) {
    Write-Host "✓ SUCCESS: Status retrieved" -ForegroundColor Green
    Write-Host "  Status: $($result2.Data.status)" -ForegroundColor Gray
    Write-Host "  Request ID: $($result2.Data.requestId)" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result2.Duration, 2))ms" -ForegroundColor Gray
    
    if ($result2.Data.status -eq "PENDING") {
        Write-Host "  ✓ Status is PENDING as expected" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Unexpected status: $($result2.Data.status)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ FAILED: $($result2.Error)" -ForegroundColor Red
    Write-Host "  Duration: $([math]::Round($result2.Duration, 2))ms" -ForegroundColor Gray
}

# Test 3: Submit Match Request (Player 2)
Write-Host "`n[TEST 3] Submit Match Request - Player 2" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$requestBody2 = @{
    metadata = @{
        region = "us-west"
        skill_level = "beginner"
    }
} | ConvertTo-Json

$result3 = Invoke-MatchmakingApi -Method "POST" -Endpoint "/v1/request" -UserId $testUserId2 -Body $requestBody2

if ($result3.Success) {
    $requestId2 = $result3.Data.requestId
    Write-Host "✓ SUCCESS: Request submitted" -ForegroundColor Green
    Write-Host "  Request ID: $requestId2" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result3.Duration, 2))ms" -ForegroundColor Gray
} else {
    Write-Host "✗ FAILED: $($result3.Error)" -ForegroundColor Red
    Write-Host "  Duration: $([math]::Round($result3.Duration, 2))ms" -ForegroundColor Gray
    exit 1
}

# Test 4: Wait for Matcher to Run
Write-Host "`n[TEST 4] Waiting for MatchMaker to process..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow
Write-Host "Waiting 3 seconds for matcher tick..." -ForegroundColor Gray

Start-Sleep -Seconds 3

# Test 5: Check Status Player 1 (Should be MATCHED)
Write-Host "`n[TEST 5] Check Match Status - Player 1 (Should be MATCHED or NotFound)" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$result5 = Invoke-MatchmakingApi -Method "GET" -Endpoint "/v1/request/$requestId1" -UserId $testUserId1

if ($result5.Success) {
    Write-Host "✓ SUCCESS: Status retrieved" -ForegroundColor Green
    Write-Host "  Status: $($result5.Data.status)" -ForegroundColor Gray
    Write-Host "  Request ID: $($result5.Data.requestId)" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result5.Duration, 2))ms" -ForegroundColor Gray
    
    if ($result5.Data.status -eq "MATCHED") {
        Write-Host "  ✓ Status is MATCHED as expected" -ForegroundColor Green
        Write-Host "  Session ID: $($result5.Data.sessionId)" -ForegroundColor Gray
        Write-Host "  Matched Users: $($result5.Data.matchedUserIds -join ', ')" -ForegroundColor Gray
        Write-Host "  Matched Requests: $($result5.Data.matchedRequestIds -join ', ')" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ Unexpected status: $($result5.Data.status)" -ForegroundColor Red
    }
} else {
    Write-Host "✓ Request not found (matched requests are removed from pool)" -ForegroundColor Yellow
    Write-Host "  Note: This is expected behavior - matched requests are removed" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result5.Duration, 2))ms" -ForegroundColor Gray
}

# Test 6: Check Status Player 2 (Should be MATCHED)
Write-Host "`n[TEST 6] Check Match Status - Player 2 (Should be MATCHED or NotFound)" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$result6 = Invoke-MatchmakingApi -Method "GET" -Endpoint "/v1/request/$requestId2" -UserId $testUserId2

if ($result6.Success) {
    Write-Host "✓ SUCCESS: Status retrieved" -ForegroundColor Green
    Write-Host "  Status: $($result6.Data.status)" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result6.Duration, 2))ms" -ForegroundColor Gray
    
    if ($result6.Data.status -eq "MATCHED") {
        Write-Host "  ✓ Status is MATCHED as expected" -ForegroundColor Green
        Write-Host "  Session ID: $($result6.Data.sessionId)" -ForegroundColor Gray
        Write-Host "  Matched Users: $($result6.Data.matchedUserIds -join ', ')" -ForegroundColor Gray
        
        # Verify both players have same session
        if ($result6.Data.sessionId -eq $result5.Data.sessionId) {
            Write-Host "  ✓ Both players have same session ID" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Session IDs don't match!" -ForegroundColor Red
        }
    } else {
        Write-Host "  ✗ Unexpected status: $($result6.Data.status)" -ForegroundColor Red
    }
} else {
    Write-Host "✓ Request not found (matched requests are removed from pool)" -ForegroundColor Yellow
    Write-Host "  Note: This is expected behavior - matched requests are removed" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result6.Duration, 2))ms" -ForegroundColor Gray
}

# Test 7: Error Scenario - Duplicate Request
Write-Host "`n[TEST 7] Error Scenario - Duplicate Request" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$testUserId3 = "test-user-3"
$requestBody3 = @{
    metadata = @{
        region = "eu-central"
    }
} | ConvertTo-Json

# Submit first request
$result7a = Invoke-MatchmakingApi -Method "POST" -Endpoint "/v1/request" -UserId $testUserId3 -Body $requestBody3

if ($result7a.Success) {
    $requestId3 = $result7a.Data.requestId
    Write-Host "✓ First request submitted: $requestId3 ($([math]::Round($result7a.Duration, 2))ms)" -ForegroundColor Gray
    
    # Try to submit duplicate
    $result7b = Invoke-MatchmakingApi -Method "POST" -Endpoint "/v1/request" -UserId $testUserId3 -Body $requestBody3
    
    if (-not $result7b.Success) {
        Write-Host "✓ SUCCESS: Duplicate request rejected as expected" -ForegroundColor Green
        Write-Host "  Error: $($result7b.Error)" -ForegroundColor Gray
        Write-Host "  Duration: $([math]::Round($result7b.Duration, 2))ms" -ForegroundColor Gray
    } else {
        Write-Host "✗ FAILED: Duplicate request was accepted (should be rejected)" -ForegroundColor Red
    }
    
    # Clean up - cancel the request
    $resultCleanup = Invoke-MatchmakingApi -Method "DELETE" -Endpoint "/v1/request/$requestId3" -UserId $testUserId3
    if ($resultCleanup.Success) {
        Write-Host "  ✓ Cleanup: Request cancelled ($([math]::Round($resultCleanup.Duration, 2))ms)" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ FAILED: Could not submit first request" -ForegroundColor Red
    Write-Host "  Duration: $([math]::Round($result7a.Duration, 2))ms" -ForegroundColor Gray
}

# Test 8: Cancel Request
Write-Host "`n[TEST 8] Cancel Match Request" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$testUserId4 = "test-user-4"
$requestBody4 = @{
    metadata = @{}
} | ConvertTo-Json

# Submit request
$result8a = Invoke-MatchmakingApi -Method "POST" -Endpoint "/v1/request" -UserId $testUserId4 -Body $requestBody4

if ($result8a.Success) {
    $requestId4 = $result8a.Data.requestId
    Write-Host "✓ Request submitted: $requestId4 ($([math]::Round($result8a.Duration, 2))ms)" -ForegroundColor Gray
    
    # Cancel it
    $result8b = Invoke-MatchmakingApi -Method "DELETE" -Endpoint "/v1/request/$requestId4" -UserId $testUserId4
    
    if ($result8b.Success) {
        Write-Host "✓ SUCCESS: Request cancelled" -ForegroundColor Green
        Write-Host "  Success: $($result8b.Data.success)" -ForegroundColor Gray
        Write-Host "  Duration: $([math]::Round($result8b.Duration, 2))ms" -ForegroundColor Gray
    } else {
        Write-Host "✗ FAILED: $($result8b.Error)" -ForegroundColor Red
        Write-Host "  Duration: $([math]::Round($result8b.Duration, 2))ms" -ForegroundColor Gray
    }
    
    # Verify status is CANCELLED
    $result8c = Invoke-MatchmakingApi -Method "GET" -Endpoint "/v1/request/$requestId4" -UserId $testUserId4
    
    if ($result8c.Success) {
        if ($result8c.Data.status -eq "CANCELLED") {
            Write-Host "  ✓ Status is CANCELLED as expected ($([math]::Round($result8c.Duration, 2))ms)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Unexpected status: $($result8c.Data.status)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "✗ FAILED: Could not submit request" -ForegroundColor Red
    Write-Host "  Duration: $([math]::Round($result8a.Duration, 2))ms" -ForegroundColor Gray
}

# Test 9: Request Not Found
Write-Host "`n[TEST 9] Error Scenario - Request Not Found" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

$fakeRequestId = "00000000-0000-0000-0000-000000000000"
$result9 = Invoke-MatchmakingApi -Method "GET" -Endpoint "/v1/request/$fakeRequestId" -UserId "test-user-5"

if (-not $result9.Success) {
    Write-Host "✓ SUCCESS: Non-existent request rejected as expected" -ForegroundColor Green
    Write-Host "  Error: $($result9.Error)" -ForegroundColor Gray
    Write-Host "  Duration: $([math]::Round($result9.Duration, 2))ms" -ForegroundColor Gray
} else {
    Write-Host "✗ FAILED: Non-existent request returned data (should fail)" -ForegroundColor Red
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Suite Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nAll core functionality tests completed." -ForegroundColor Green
Write-Host "Review the results above for any failures.`n" -ForegroundColor Gray
