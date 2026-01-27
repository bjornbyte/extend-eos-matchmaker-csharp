# Implementation Examples

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md) | **Examples**

---

> **⚠️ Note:** These code examples were generated with AI assistance and have not been tested or verified to work. They are provided as architectural guidance and starting points. You should test and adapt them for your specific environment and requirements.

---

This guide provides complete, working code examples for common customizations of the matchmaking service.

## Table of Contents

- [Webhook Player Notifier](#webhook-player-notifier)
- [Key-Value Store-Based MatchPool](#key-value-store-based-matchpool)
- [Database-Backed CompletedRequestStore](#database-backed-completedrequeststore)
- [Skill-Based MatchMaker](#skill-based-matchmaker)

---

## Webhook Player Notifier

**Use Case:** Notify your game backend when matches are created so it can push notifications to players.

**Extension Point:** `IPlayerNotifier`

### Implementation

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    public class WebhookPlayerNotifier : IPlayerNotifier
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebhookPlayerNotifier> _logger;
        private readonly string _webhookUrl;

        public WebhookPlayerNotifier(
            HttpClient httpClient,
            ILogger<WebhookPlayerNotifier> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webhookUrl = configuration["PlayerNotifier:WebhookUrl"] 
                ?? throw new InvalidOperationException("PlayerNotifier:WebhookUrl not configured");
        }
