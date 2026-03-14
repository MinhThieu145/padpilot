# ChatVisual

ChatVisual is a C# WPF desktop assistant that combines a chat interface, screenshot-based context, and USB macro pad input for custom desktop actions and automation.

## Status

This project is currently an experimental personal project.

### Working now
- WPF desktop chat interface
- Claude-powered assistant responses
- Full-screen screenshot capture and send-to-assistant flow
- Global low-level keyboard hook prototype
- Raw Input device detection for a specific USB macro pad
- Macro pad identification by device handle

### In progress
- Reliable background Raw Input handling
- Dedicated silent/message-only window for Raw Input
- Cleaner macro action system
- Better configuration and secret management

## Features

- Chat with a desktop assistant from a WPF UI
- Send optional screenshot context along with your message
- Intercept keyboard input globally using `WH_KEYBOARD_LL`
- Use Raw Input to distinguish input from a specific external macro pad
- Prototype support for swallowing macro pad F1–F9 while letting normal keyboard input continue normally

## Why this project exists

Some USB macro pads have locked firmware and only send normal function keys like `F1` through `F9`.

That creates a problem:
- Windows sees the same virtual keys from both the macro pad and the normal keyboard
- a low-level keyboard hook can block keys globally, but cannot identify the physical device
- Raw Input can identify the physical device, but cannot block the key before other apps receive it

This project combines both approaches:
1. swallow candidate keys globally in a low-level hook
2. inspect Raw Input to identify which physical device generated the key
3. keep macro pad keys suppressed
4. replay normal keyboard keys back into the OS when needed

## Architecture overview

### Main window
`MainWindow.xaml.cs`
- initializes the chat UI
- stores chat messages in an `ObservableCollection`
- sends messages to Claude
- captures screenshots
- initializes the Raw Input / low-level hook system

### Claude client
`ClaudeClient.cs`
- manages the Anthropic client
- stores conversation history
- sends user messages and optional screenshots
- retries on overload errors

### Chat message model
`ChatMessage.cs`
- simple model used to bind chat messages to the UI

### Input interception
`RawInputHook.cs`
- installs the global low-level keyboard hook
- enumerates input devices
- finds the target macro pad by device name
- registers keyboard Raw Input
- inspects keyboard packets from Raw Input
- distinguishes macro pad input from normal keyboard input
- re-injects normal keyboard input when necessary using `SendInput`

## Current input flow

1. A physical key is pressed
2. `WH_KEYBOARD_LL` intercepts the event globally
3. Candidate keys (`F1`–`F9`) are swallowed immediately
4. Raw Input receives the keyboard packet
5. The app checks the device handle:
   - if it came from the target macro pad, keep it suppressed and run custom logic
   - if it came from another keyboard, replay it with `SendInput`

## Tech stack

- C#
- WPF
- Win32 interop
- Anthropic SDK

## Setup

### Requirements
- Windows
- Visual Studio
- .NET desktop runtime / SDK compatible with this project
- A supported Anthropic API key

### Important
Do **not** hardcode API keys in source code.

Use an environment variable instead:

```powershell
setx ANTHROPIC_API_KEY "your_key_here"