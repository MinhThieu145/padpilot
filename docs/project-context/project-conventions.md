# Project Conventions

Tribal knowledge about this project — toolchain quirks, local setup details, and corrections that agents have gotten wrong before.

Agents reading this file should follow anything here over their default assumptions. Entries are appended at the end of working sessions when the agent or toolchain got something wrong, or when local conventions were discovered that future sessions should follow.

---

## [2026-04-19] Additions

### Toolchain

- Visual Studio auto-updates the classic `.csproj` when files are added via Solution Explorer. Classic `.csproj` files don't auto-glob on their own, but in practice new files show up in the project file because the IDE writes them in. Trust the Solution Explorer view — if the file is listed there, it builds. Do not tell the user they need to manually add `<Compile Include="...">` entries.

### Naming

- Provider-level terminology uses the actual API/account name, not the product name. Use `AnthropicApiKey` (not `ClaudeApiKey`) and reference the `ANTHROPIC_API_KEY` environment variable. The API account is with Anthropic; Claude is one model family under that account. This matches existing conventions in the code.