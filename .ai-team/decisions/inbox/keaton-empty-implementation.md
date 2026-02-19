### 2025-01-XX: WindowsVoiceService.cs is an empty class — dead code

**By:** Keaton

**What:** WindowsVoiceService.cs contains only an empty internal class with no implementation. It's never registered in DI or referenced anywhere.

**Why:** This should either be implemented (if there's a plan for native Windows Speech API fallback) or deleted. Keeping dead code in the repo creates confusion and maintenance overhead. If it's a placeholder for future work, document that intent or move it to a separate branch.

**Recommendation:** Delete WindowsVoiceService.cs unless there's an active plan to implement it.
