using Xunit;

namespace VoiceMCP.Tests
{
    /// <summary>
    /// Tests for conditional Teams service registration in Program.cs.
    /// 
    /// The registration logic (being built by Fenster) works as follows:
    /// - When TEAMS_WEBHOOK_URL env var is set → TeamsWebhookService and TeamsTools are registered
    /// - When TEAMS_WEBHOOK_URL env var is NOT set → Teams services are NOT registered
    /// 
    /// These are documented as integration verification points because Program.cs uses
    /// top-level statements with Host builder, making it difficult to unit test the
    /// registration logic in isolation without refactoring.
    /// 
    /// MANUAL VERIFICATION STEPS:
    /// 1. Run `dotnet run` without TEAMS_WEBHOOK_URL → verify no Teams-related errors
    /// 2. Run `TEAMS_WEBHOOK_URL=https://test dotnet run` → verify Teams tools appear in MCP tool list
    /// 3. Check stderr output for "Teams notification enabled" or similar log message
    /// 
    /// FUTURE: If Fenster extracts registration into a testable method (e.g., 
    /// ServiceCollectionExtensions.AddTeamsServices), we can write proper unit tests here.
    /// </summary>
    public class ConditionalRegistrationTests
    {
        [Fact]
        public void TeamsWebhookService_ImplementsInterface()
        {
            // Verify the contract is satisfied at the type level
            Assert.True(typeof(Services.ITeamsNotificationService)
                .IsAssignableFrom(typeof(Services.TeamsWebhookService)));
        }

        [Fact]
        public void TeamsWebhookService_Constructor_AcceptsRequiredParameters()
        {
            // Verify the constructor signature matches what DI will provide
            var constructor = typeof(Services.TeamsWebhookService).GetConstructor(
                new[] { typeof(HttpClient), typeof(string), typeof(string) });

            Assert.NotNull(constructor);
        }

        [Fact]
        public void TeamsTools_Constructor_AcceptsITeamsNotificationService()
        {
            var constructor = typeof(Tools.TeamsTools).GetConstructor(
                new[] { typeof(Services.ITeamsNotificationService) });

            Assert.NotNull(constructor);
        }

        [Fact]
        public void TeamsTools_DoesNotHave_McpServerToolTypeAttribute()
        {
            // TeamsTools should NOT have [McpServerToolType] because it's conditionally registered.
            // Fenster registers it explicitly only when TEAMS_WEBHOOK_URL is set.
            var attribute = Attribute.GetCustomAttribute(
                typeof(Tools.TeamsTools),
                typeof(ModelContextProtocol.Server.McpServerToolTypeAttribute));

            Assert.Null(attribute);
        }
    }
}
