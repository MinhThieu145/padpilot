# Architecture

## Overview

ChatVisual is a Windows desktop assistant built with C# and WPF.

Right now the app has four main parts:

1. Chat UI
2. Screenshot capture
3. Global macro hotkeys using a low-level keyboard hook
4. AI response orchestration through `ModeOrchestrator`

The current version uses the simplified input design:

- all physical `F1` through `F10` keys are intercepted globally
- those keys are handled by the app as macro keys
- the keys are swallowed so other apps do not receive them

This means the app does **not** currently distinguish between:

- the external macro pad
- the normal keyboard

That older Raw Input based direction is postponed for now to keep the app simpler and easier to debug.

## How To Continue Development

When continuing this project:

1. read this file first
2. run `scripts/refresh-ai-note.ps1`
3. read `docs/ai-local/CURRENT_SESSION.md`
4. inspect the current git diff if you need deeper context

## Main Components

### `MainWindow`

The main WPF window.

Responsibilities:

- displays chat history
- shows screenshot thumbnails
- shows the current response mode
- captures full-screen screenshots
- registers macro actions
- forwards prompts and screenshots to `ModeOrchestrator`

Current window behavior:

- topmost overlay
- hidden from the taskbar
- not interactable with the mouse through the main grid
- message input is currently collapsed

### `ModeOrchestrator`

The central coordinator for AI responses.

Responsibilities:

- keeps the current response mode
- stores shared conversation history in `_sharedChatHistory`
- routes requests to the correct AI client
- clears shared history when the mode changes
- provides a manual `ClearHistory()` path for session reset

Current response modes:

- `Quick`
- `Thinking`
- `DeepThinking`

Current behavior:

- `Quick` uses OpenAI
- `Thinking` tries Claude first, then falls back to OpenAI
- `DeepThinking` currently follows the same provider flow as `Thinking`

### `OpenAIWrapper`

Handles OpenAI requests.

Responsibilities:

- converts shared history into OpenAI chat messages
- includes screenshots as image parts
- applies the active request config
- retries failed requests

### `ClaudeClient`

Handles Anthropic requests.

Responsibilities:

- converts shared history into Claude message content
- includes screenshots as image content
- applies the active request config
- retries overload failures
- throws on final failure so the orchestrator can decide fallback behavior

### `RawInputHook`

Despite the name, this class currently acts as the keyboard hook manager.

Responsibilities:

- installs the global low-level keyboard hook
- ignores injected keyboard events
- intercepts physical `F1` through `F10`
- routes macro keys into registered actions
- removes the hook on shutdown

> Note: the class name is still `RawInputHook` from the older design even though the current implementation uses only the low-level keyboard hook path.

## Current Input Architecture

The app currently uses **only** `WH_KEYBOARD_LL` (`SetWindowsHookEx`) for macro key handling.

### What Happens

1. A key is pressed anywhere in Windows
2. The low-level keyboard hook receives the event
3. If the key is:
   - physical
   - not injected
   - one of `F1` through `F10`

   then the app:
   - treats it as a macro key
   - runs the registered macro action
   - swallows the key so it does not continue to other apps

4. All other keys continue normally

### Current Macro Map

- `F1` move window up
- `F2` move window down
- `F3` move window left
- `F4` move window right
- `F5` take screenshot
- `F6` send message to the orchestrator
- `F7` cycle response mode
- `F8` scroll chat up
- `F9` scroll chat down
- `F10` clear the current session

## Current Conversation Flow

1. The user builds context with text, screenshots, or both
2. `MainWindow` converts screenshot thumbnails into byte arrays
3. `ModeOrchestrator` appends the user message to shared history
4. The active response mode chooses the provider path
5. The selected AI client converts the shared history into provider-specific messages
6. The assistant response is returned to `MainWindow`
7. `ModeOrchestrator` stores the assistant response back into shared history
8. The UI appends the assistant message and clears the pending screenshots

## Why The Input Design Was Simplified

An earlier version tried to combine:

- `WH_KEYBOARD_LL` for global suppression
- Raw Input for device identification

The goal was:

- macro pad `F1` through `F9` should be intercepted
- normal keyboard `F1` through `F9` should keep working normally

That version turned out to be much more complex because:

- the low-level hook can block keys, but cannot identify the keyboard device
- Raw Input can identify the device, but cannot block the original key in time
- replaying keys with `SendInput` introduced extra complexity
- background Raw Input behavior was unreliable in the current app setup

For the current stage of the project, the simpler design was chosen instead:

- faster development
- less fragile behavior
- fewer moving parts
- easier debugging

## Current Limitations

- normal keyboard `F1` through `F10` no longer behave normally while the app is running
- the app does not distinguish between keyboards
- `RawInputHook` is now a misleading class name
- the overlay is non-interactable but not true click-through
- `README.md` still needs a later refresh to match the current design

Issue tracking should live in GitHub Issues, not in a separate long-lived issues document in the repo.

## Future Direction

The current priorities are:

- keep the architecture docs aligned with the live code
- improve AI handoff between development sessions
- clean up naming around the hook system
- extract more pure decision logic from Win32-heavy code
- revisit device-specific input handling later if the simpler model stops being good enough
