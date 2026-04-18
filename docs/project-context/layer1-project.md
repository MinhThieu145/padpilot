# ChatVisual - Layer 1 Project Summary

## Name and One-Sentence Pitch

**ChatVisual** - An always-on-top Windows AI overlay that lets a developer ask questions, attach screenshots, and trigger common actions from F-key macros without leaving the current application.

---

## What It Does

ChatVisual is a borderless WPF desktop pane designed to sit above the user's current work. The user can type a question, optionally attach a full-display screenshot, and receive an AI response rendered in the chat UI. Conversation history is kept across exchanges until the user clears it or changes response mode.

The app has three fixed response modes: `Quick`, `Thinking`, and `DeepThinking`. The modes trade speed against reasoning depth and are selected by cycling rather than arbitrary external assignment.

ChatVisual also installs a global low-level keyboard hook while the app is running. The current simplified macro model swallows physical `F1-F10` key events globally and routes those keys to hardcoded app actions such as moving the overlay, taking a screenshot, sending a message, cycling mode, scrolling chat, and clearing the session.

---

## Who It's For and Why

This is a personal productivity and learning project built by and for a single developer on Windows. The main goal is to keep AI assistance close at hand while working, without switching to a browser or a separate chat app.

The macro-key behavior exists because the user wants fast keyboard-driven control over the overlay. It is intentionally scoped as local app plumbing, not as a general-purpose macro engine.

---

## Platform and Tech Stack

- **OS:** Windows only, using Win32 APIs where needed.
- **Language/Runtime:** C# on .NET Framework 4.7.2.
- **UI Framework:** WPF, centered around a single overlay window.
- **AI Backends:** Anthropic/Claude and OpenAI behind provider-specific client wrappers.
- **AI Routing:** Normal responses go through `ModeOrchestrator`, not directly from UI code to provider clients.
- **Markdown Rendering:** MdXaml.
- **Code Display:** AvalonEdit.
- **Settings Direction:** API keys and mode settings are planned to persist locally under `%LocalAppData%\ChatVisual\settings.json`, with API keys protected by Windows DPAPI when persistence work is implemented.

---

## Core Features

- **Multi-turn AI chat** - Maintains shared provider-neutral conversation history across exchanges.
- **Screenshot attachment** - Captures the display and passes screenshot bytes through the orchestrator and provider clients for the next AI request.
- **Response modes** - Uses fixed `Quick`, `Thinking`, and `DeepThinking` modes. `Quick` uses OpenAI; `Thinking` and `DeepThinking` try Claude first and fall back to OpenAI on Claude failure.
- **Mode/session behavior** - Switching response mode clears shared conversation history; history can also be cleared manually.
- **Macro action handling** - Swallows global physical `F1-F10` events while the app is running and dispatches registered app actions.
- **Overlay window behavior** - The main window is topmost, hidden from the taskbar, transparent/click-through at the root grid, and hidden from screen capture via `SetWindowDisplayAffinity`.

---

## Key Vocabulary / Concepts

- **Overlay** - The app's window posture: always-on-top, taskbar-hidden, visually lightweight, and designed to stay near the user's active work.
- **Response Mode** - One of the fixed AI orchestration modes: `Quick`, `Thinking`, or `DeepThinking`.
- **Session** - The active shared conversation history. It is cleared manually or automatically when response mode changes.
- **Provider Client** - A wrapper such as `ClaudeClient` or `OpenAIWrapper` that translates shared app messages into provider-specific SDK calls.
- **Mode Orchestrator** - The routing boundary that owns active mode, shared history, mode changes, and provider fallback behavior.
- **Key Swallowing** - Blocking physical `F1-F10` key events from reaching other applications while the app is running, so ChatVisual can use them as macro actions.

---

## What This Project Is NOT

- **Not commercial software** - No installer, auto-update, telemetry, monitoring, or production hardening unless explicitly requested.
- **Not cross-platform** - Windows-only by design; do not introduce cross-platform abstractions without a direct request.
- **Not multi-user** - Single local user; no accounts, sync, collaboration, or networked user state.
- **Not a general macro engine** - Current macro behavior is hardcoded app control, not user-configurable scripting or arbitrary automation.
- **Not an enterprise architecture exercise** - Keep the code direct and sympathetic to the existing WPF/.NET Framework project.
- **Not currently an MVVM application** - Existing UI state is bound through WPF resources and `ObservableCollection<T>` without introducing view-model classes.
