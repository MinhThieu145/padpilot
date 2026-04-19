# Layer 2 Project State

Last updated: 2026-04-19

## Existing classes and their purpose

### ChatVisual

- `AIRequestConfig`: Holds provider request settings for model, system prompt, temperature, and maximum output tokens.
- `Base64ToImageConverter`: Converts base64 screenshot strings into WPF `BitmapImage` instances for XAML binding.
- `ChatMessageRole`: Defines the provider-neutral conversation roles supported by shared AI history.
- `ClaudeClient`: Translates shared conversation history and screenshots into Anthropic messages, applies request config, and retries overload failures.
- `ConversationMessage`: Provides the UI-bound chat row shape used by `MainWindow` for displayed role/content pairs.
- `MainWindow`: Owns the WPF window lifecycle, UI-bound message collections, screenshot capture, macro action registration, and calls into `ModeOrchestrator`.
- `ModeOrchestrator`: Owns the active response mode, shared chat history, provider routing, mode-change clearing, and manual history clearing.
- `OpenAIWrapper`: Translates shared conversation history and screenshots into OpenAI chat messages, selects the OpenAI model, applies request options, and retries failures.
- `RawInputHook`: Installs a global low-level keyboard hook, swallows physical F1-F10 events, and dispatches registered macro actions.
- `ResponseMode`: Defines the fixed orchestration modes `Quick`, `Thinking`, and `DeepThinking`.
- `SharedMessage`: Provides the provider-neutral message format used by the orchestrator and AI clients.

## Architecture decisions already made

- The app is constrained to .NET Framework 4.7.2 and WPF.
- AI calls are routed through `ModeOrchestrator`; UI code should not directly choose provider clients for normal responses.
- Provider-specific clients translate from `SharedMessage` into SDK-specific message types instead of exposing provider models across the app.
- Request tuning is separated into `AIRequestConfig` presets instead of scattering model, prompt, temperature, and token settings through call sites.
- Modes are a fixed `ResponseMode` enum and are changed by cycling, not by arbitrary external assignment.
- Shared conversation history is cleared when the response mode changes, and it can also be cleared manually.
- `Quick` uses OpenAI; `Thinking` and `DeepThinking` try Claude first and fall back to OpenAI on Claude failure.
- Screenshot payloads are passed to the orchestrator and clients as `byte[]`; base64 is used only for current UI thumbnail binding.
- Macro interception currently uses hook-only `WH_KEYBOARD_LL` behavior; all physical F1-F10 keys are swallowed globally while the app is running.
- The main overlay is configured as topmost, hidden from the taskbar, transparent, mouse non-interactive at the root grid, and hidden from capture via `SetWindowDisplayAffinity`.

## Code conventions in use

- Keep one significant class or enum per source file, excluding generated `Properties` files.
- Use PascalCase for public and internal type names, methods, properties, and enum members; use leading-underscore camelCase for private fields.
- Keep provider SDK translation inside provider client classes, with `ModeOrchestrator` as the routing boundary.
- Async AI request methods return `Task<string>` and keep provider-specific retry behavior inside provider clients.
- Bind WPF UI state with `ObservableCollection<T>` and XAML resources rather than introducing view-model classes.

## Locked planning decisions

- Settings v1 scope is limited to AI mode settings, global guidelines, and API key storage shape.
- Macro keybindings, Focus Mode window behavior, screenshot settings, and session behavior are out of scope for the settings milestone.
- Modes stay fixed at `Quick`, `Thinking`, and `DeepThinking`; user-created modes are not planned for the current settings milestone or soon after.
- Each mode supports a primary and fallback provider config, with fallback sharing the primary system prompt while provider, model, and tuning may differ.
- Global guidelines are stored once at the top level and combined with each mode's system prompt at request time.
- API keys will move from environment variables into settings protected with Windows DPAPI, with DPAPI implementation deferred to persistence work.
- Settings will persist as local JSON at `%LocalAppData%\ChatVisual\settings.json`.
- The app remains Windows-only; cross-platform support is out of scope.
- The project is personal, not commercial, so multi-user scenarios are out of scope.
- The simplified hook-only macro model is the current approach; Raw Input device-specific handling is superseded unless the simplified model stops working.
