### 2025-01-XX: TeamsWebhookService creates new HttpClient per instance

**By:** Keaton

**What:** Program.cs line 35 instantiates TeamsWebhookService with `new HttpClient()`, passing it to the constructor. This HttpClient is stored as an instance field and used for all webhook posts.

**Why:** Creating HttpClient instances directly can lead to socket exhaustion under load. While VoiceMCP likely has low request volume (human interaction pace), this is still an anti-pattern in .NET.

**Recommendation:** Use IHttpClientFactory pattern:
1. Register HttpClient in DI: `builder.Services.AddHttpClient<ITeamsNotificationService, TeamsWebhookService>()`
2. Inject HttpClient into TeamsWebhookService constructor
3. Remove manual HttpClient instantiation from Program.cs

This is low priority for an MCP server (not high-throughput), but it's best practice and makes testing easier.
