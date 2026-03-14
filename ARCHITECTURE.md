# Architecture

## Overview

ChatVisual is a Windows desktop assistant built with C# and WPF.

Right now the app has three main parts:

1. **Chat UI**
2. **Screenshot capture**
3. **Global macro hotkeys using a low-level keyboard hook**

The current version uses a simplified input design:

- all physical `F1` through `F9` keys are intercepted globally
- those keys are handled by the app as macro keys
- the keys are swallowed so other apps do not receive them

This means the app no longer tries to distinguish between:
- the external macro pad
- the normal keyboard

That was part of an earlier design, but it was intentionally removed for v1 to keep the project simpler and more reliable.

---

## Main components

### `MainWindow`
The main WPF window.

Responsibilities:
- displays the chat UI
- stores and renders chat messages
- sends user messages to the assistant
- captures screenshots
- creates the keyboard hook system on startup

### `ClaudeClient`
Handles communication with the Anthropic API.

Responsibilities:
- stores message history
- sends text messages
- optionally includes screenshot data
- retries on overload errors

### `ChatMessage`
Simple model for chat items shown in the UI.

### `RawInputHook`
Despite the name, this class now acts as the app’s keyboard hook manager.

Responsibilities:
- installs the global low-level keyboard hook
- intercepts physical `F1`–`F9`
- ignores injected input when appropriate
- routes macro key presses into app-defined macro behavior
- removes the hook on shutdown

> Note: the class name is still `RawInputHook` from the earlier design, even though Raw Input is no longer used in the current version.

---

## Current input architecture

## Current design
The app currently uses **only** `WH_KEYBOARD_LL` (`SetWindowsHookEx`) for macro key handling.

### What happens
1. A key is pressed anywhere in Windows
2. The low-level keyboard hook receives the event
3. If the key is:
   - physical
   - not injected
   - one of `F1` through `F9`
   
   then the app:
   - treats it as a macro key
   - runs app-defined macro behavior
   - swallows the key so it does not continue to other apps

4. All other keys continue normally

### Result
This makes `F1`–`F9` global app macro keys.

That includes:
- the external macro pad
- the normal keyboard

The current design does **not** distinguish which physical keyboard sent the key.

---

## Why the design was simplified

An earlier version tried to combine:

- `WH_KEYBOARD_LL` for global suppression
- Raw Input for device identification

The goal was:

- macro pad `F1`–`F9` should be intercepted
- normal keyboard `F1`–`F9` should behave normally

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

---

## Tradeoffs of the current design

### Advantages
- much simpler input system
- no Raw Input dependency
- no replay logic
- no background Raw Input problems
- easier to reason about and maintain

### Limitations
- normal keyboard `F1`–`F9` no longer behave normally
- the app does not distinguish between keyboards
- all physical `F1`–`F9` are treated as app macro keys

This tradeoff is currently acceptable for v1.

---

## Current startup flow

1. Main window loads
2. `RawInputHook` is created
3. the global low-level keyboard hook is installed
4. the app is ready to intercept `F1`–`F9`

---

## Current shutdown flow

When the app closes:

1. the main window closes
2. the keyboard hook is explicitly removed using `UnhookWindowsHookEx`

This cleanup is required because the keyboard hook is a real Windows hook resource.

---

## Current macro behavior

The hook currently routes intercepted `F1`–`F9` keys into a macro handler method.

That handler is the place where app-specific macro actions should live.

This is the main extension point for future macro features.

---

## Future direction

The original long-term idea is still valid:

- intercept `F1`–`F9` from the macro pad only
- allow normal keyboard `F1`–`F9` to continue behaving normally

That would require reintroducing a more advanced architecture, likely involving:
- device-specific input handling
- a more reliable message-only or native input sink
- a cleaner separation between input plumbing and decision logic

That work is intentionally postponed for now.

The current version prioritizes simplicity over device-specific behavior.

---

## Summary

The current architecture is:

- **WPF UI for the desktop assistant**
- **Claude API integration for chat**
- **global low-level keyboard hook for macro keys**

The current version is intentionally simplified:
- all physical `F1`–`F9` are app macro keys
- Raw Input is not used
- device-specific macro handling is future work
