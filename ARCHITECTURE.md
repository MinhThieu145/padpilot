# Architecture

## Overview

ChatVisual is a Windows desktop assistant built with C# and WPF.

The app currently combines three main areas:

1. **Desktop chat UI**
2. **Screenshot capture**
3. **Keyboard interception for a USB macro pad**

The most unusual part of the project is the input system.

The macro pad sends normal keyboard function keys like `F1` to `F9`, but the app needs to treat those keys differently depending on which physical device sent them:

- if the macro pad sends `F1`–`F9`, the app should intercept them and run custom logic
- if the normal keyboard sends `F1`–`F9`, the keys should behave normally

Because Windows does not provide both **global suppression** and **device identity** in the same keyboard API, the project currently combines two different input mechanisms.

---

## Main components

### `MainWindow`
Responsible for the visible desktop UI.

Current responsibilities:

- initializes the app window
- displays chat messages
- handles user message input
- captures screenshots
- creates the input hook system on startup

### `ClaudeClient`
Responsible for communication with the Anthropic API.

Current responsibilities:

- stores chat history
- sends text messages
- optionally includes a screenshot
- retries on overload errors

### `ChatMessage`
Simple model used by the UI to render messages in chat history.

### `RawInputHook`
Responsible for the macro pad / keyboard interception system.

Current responsibilities:

- installs a global low-level keyboard hook
- enumerates input devices
- finds the target macro pad by device identifier
- registers for raw keyboard input
- reads raw keyboard packets
- decides whether a key should be suppressed or replayed

---

## Why the input architecture is unusual

This project needs two different capabilities:

1. **globally block a key before other apps receive it**
2. **know which physical keyboard device generated the key**

No single keyboard API used here gives both.

### Low-level keyboard hook (`WH_KEYBOARD_LL`)
This can globally intercept and suppress keys.

Useful because:

- it can swallow keys before they continue through the normal input path

Limitation:

- it does **not** identify the physical keyboard device

That means the hook can see:
- virtual key code
- scan code
- flags
- timestamp

But it cannot answer:
- did this key come from the macro pad?
- or from the normal keyboard?

### Raw Input (`WM_INPUT`)
This can identify which physical device generated keyboard input.

Useful because:

- it provides a device handle (`hDevice`)
- it allows the app to distinguish between keyboards

Limitation:

- it is **read-only**
- by the time the app reads Raw Input, it is too late to stop the original key from reaching other apps

---

## Current input strategy

The project currently uses a **swallow first, decide later** strategy.

### Flow

1. A physical key is pressed
2. The low-level hook receives it first
3. If the key is `F1`–`F9` and not injected, the app swallows it immediately
4. Later, Raw Input receives the keyboard packet
5. The app checks which device generated the key
6. Then the app decides:

- **macro pad**
  - keep the key suppressed
  - run custom macro logic

- **normal keyboard**
  - replay the key using `SendInput`
  - this restores normal behavior for the user

---

## Why replay is needed

Because the low-level hook does not know the physical keyboard device, it cannot safely decide in real time whether an `F1` press should be blocked forever or allowed through.

So the current design does this:

- block first
- inspect the device later with Raw Input
- replay only if the key came from a normal keyboard

This is the core design tradeoff in the current system.

---

## Current replay loop issue and fix

When the app replays a normal keyboard key with `SendInput`, that replayed event can come back through the input pipeline again.

That can create an infinite loop:

1. normal keyboard key is swallowed
2. Raw Input decides it should be restored
3. app calls `SendInput`
4. replayed input appears again
5. app mistakenly tries to restore it again
6. loop repeats

### Current mitigation

The project currently ignores Raw Input events where:

- `RAWINPUTHEADER.hDevice == IntPtr.Zero`

This is being used as the signal that the event is injected rather than coming from a physical keyboard.

This behavior works for the current prototype, but it should still be treated as an implementation assumption that may need more hardening later.

---

## Current device detection approach

The macro pad is currently identified by checking its device path string from `GetRawInputDeviceInfo`.

The current matching logic looks for identifiers such as:

- `VID_1189`
- `MI_00`

This is a simple and practical prototype approach, but it is also hardcoded.

### Limitation
If the hardware changes, or if Windows exposes the device differently, this matching logic may need to be updated.

---

## Current startup flow

1. Main window loads
2. `RawInputHook` is created
3. the low-level keyboard hook is installed
4. the app enumerates Raw Input devices
5. the target macro pad is identified
6. keyboard Raw Input is registered to the app window
7. the app begins listening for:
   - low-level keyboard events
   - raw keyboard input messages

---

## Current shutdown flow

On app close:

- the low-level keyboard hook is explicitly removed with `UnhookWindowsHookEx`

This is necessary because the keyboard hook is a real Windows hook resource and should be cleaned up explicitly.

---

## Known architectural weakness

The current Raw Input target is tied to the WPF window.

This mostly works in the foreground, but background delivery has been inconsistent in testing.

Because of that, the next architectural step is:

### Planned improvement
Move Raw Input delivery to a dedicated hidden or message-only native window.

The goal is to make Raw Input less dependent on the lifecycle and focus behavior of the main WPF UI window.

This change is not finished yet.

---

## Current design summary

### What works well
- chat UI works
- screenshot flow works
- low-level hook works globally
- macro pad device can be identified
- replay loop has a working mitigation
- foreground macro pad handling works

### What is still fragile
- background Raw Input reliability
- hardcoded device matching
- current input logic is tightly coupled to Win32 interop code
- more cleanup and testable abstractions are still needed

---

## Future direction

Likely next steps:

1. finish the silent/message-only Raw Input window
2. separate pure decision logic from Win32 plumbing
3. make macro actions configurable
4. improve logging and debugging support
5. add automated tests for pure input-decision logic
6. keep manual testing for real hardware behavior

---

## Mental model

A simple way to think about the current system:

- **Low-level hook** = power to block
- **Raw Input** = power to identify
- **SendInput** = restore normal behavior when the blocked key came from the wrong device

That is the core architecture of the project right now.