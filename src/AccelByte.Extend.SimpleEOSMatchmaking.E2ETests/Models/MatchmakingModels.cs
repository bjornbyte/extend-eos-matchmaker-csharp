using System.Text.Json.Serialization;

namespace AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Models;

public class SubmitMatchRequest
{
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SubmitMatchResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;
}

public class GetMatchStatusResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("matchedUserIds")]
    public List<string> MatchedUserIds { get; set; } = new();

    [JsonPropertyName("matchedRequestIds")]
    public List<string> MatchedRequestIds { get; set; } = new();
}

public class CancelMatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
