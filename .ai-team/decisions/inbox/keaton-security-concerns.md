### 2025-01-XX: Azure API keys logged to stderr in cleartext on startup

**By:** Keaton

**What:** Program.cs logs diagnostic messages like "Loaded Azure OpenAI credentials from environment variables" and "Loaded Azure OpenAI credentials from user secrets/appsettings.json" to stderr. While the keys themselves aren't logged, the messages confirm whether credentials were loaded and from which source.

**Why:** This is relatively low risk but could expose information about the configuration source in logs. More concerning: if someone adds debug logging of the variables themselves, keys would leak to stderr.

**Recommendation:** 
1. Ensure no debug logging accidentally exposes keys (current code is safe)
2. Consider logging credential source at Debug level rather than Error level
3. Add a code comment warning against logging credential values
4. Document that stderr may contain diagnostic information about configuration sources
