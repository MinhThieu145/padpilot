# Known Issues

This document tracks the current limitations, fragile areas, and known bugs in ChatVisual.

It is meant to help with:
- debugging
- future refactoring
- avoiding repeated confusion
- keeping track of unfinished work

---

## 1. Background Raw Input is inconsistent

### Status
Known issue. Not fully solved yet.

### Description
The app currently registers keyboard Raw Input to the main WPF window.

Foreground behavior works, but background delivery has been inconsistent.

Observed behavior:
- Raw Input works reliably when the app is focused
- background delivery sometimes works briefly
- background delivery later becomes unreliable
- the low-level keyboard hook continues to work even when Raw Input does not

### Why this matters
The current architecture depends on Raw Input to identify which physical keyboard generated a key.

If Raw Input is missing or inconsistent in the background, the app cannot reliably decide whether to:
- suppress the macro pad key
- or replay the normal keyboard key

### Current direction
The next planned fix is to move Raw Input delivery to a dedicated silent/message-only native window instead of using the main WPF window.

---

## 2. Silent/message-only window implementation is not finished

### Status
In progress / experimental

### Description
A dedicated silent window is the planned solution for more reliable background Raw Input handling.

The migration is not complete yet.

### Current problem
A native window created with `CreateWindowEx` cannot be treated like a WPF `HwndSource` window.

Earlier attempts mixed:
- native Win32 window creation
- WPF `HwndSource.FromHwnd(...)`
- `AddHook(...)`

That approach is incorrect for the final design.

### Planned direction
The silent window implementation should use:
- a custom registered native window class
- a real native `WndProc`
- Raw Input registration targeted to that message-only window

---

## 3. Macro pad matching is hardcoded

### Status
Known limitation

### Description
The target macro pad is currently identified by checking the device path string for hardcoded values such as:
- `VID_1189`
- `MI_00`

### Why this matters
This works for the current hardware prototype, but it is not flexible.

If:
- the device is replaced
- Windows exposes it differently
- another similar device is used

then matching may fail.

### Future improvement
Move device matching to:
- a config file
- a user-selectable device setup flow
- or a more robust device identification system

---

## 4. Input logic is tightly coupled to Win32 interop code

### Status
Known design weakness

### Description
Most of the keyboard decision logic currently lives inside:
- low-level hook code
- Raw Input parsing code
- Win32 event handlers

That makes the code harder to:
- unit test
- reason about
- refactor safely

### Why this matters
A lot of the behavior depends on small decisions such as:
- is this a target key?
- is this injected?
- is this the macro pad?
- should this event be replayed?

Those decisions would be easier to test if they were extracted into pure helper methods.

### Future improvement
Refactor the code so that:
- Win32 interop gathers raw data
- separate logic methods make decisions
- those decision methods can later be unit tested

---

## 5. Current replay detection relies on assumptions

### Status
Working prototype, but still fragile

### Description
The current replay-loop mitigation uses the assumption that injected raw keyboard input appears with:

- `RAWINPUTHEADER.hDevice == IntPtr.Zero`

This is currently being used to ignore replayed events and prevent an infinite reinjection loop.

### Why this matters
This works in current testing, but it is still an implementation assumption rather than a fully hardened replay-tracking system.

### Possible future improvement
Future replay filtering may also use:
- `dwExtraInfo`
- explicit replay tracking
- replay event queues
- stronger event correlation logic

---

## 6. No formal automated test coverage yet

### Status
Not implemented yet

### Description
The project currently relies mostly on:
- manual testing
- console logging
- focused debugging during development

### Why this is acceptable for now
Most of the current complexity is in:
- Windows hooks
- Raw Input
- hardware behavior
- focus/background behavior

Those are hard to fully automate at this stage.

### Future improvement
Once more pure decision logic exists, add tests for:
- target key detection
- key-up/key-down detection
- device matching logic
- replay decision logic
- injected-event filtering

Manual tests will still be needed for real hardware behavior.

---

## 7. Logging is verbose and debug-oriented

### Status
Known limitation

### Description
The current app logs heavily to the console for debugging.

This is useful during development, but it is noisy and not structured.

### Why this matters
As the app grows, debugging will become harder if logs are not:
- grouped
- consistent
- optionally disabled

### Future improvement
Add:
- a debug flag
- structured log categories
- optional reduced logging for normal development

---

## 8. App cleanup is only partially implemented

### Status
Partially addressed

### Description
The low-level keyboard hook is now explicitly unhooked on shutdown.

That part is good.

However, future native resources such as:
- silent/message-only windows
- extra Win32 handles
- future input resources

will also need explicit cleanup.

### Future improvement
Expand the shutdown path to clean up all native resources in one place.

---

## 9. The project is Windows-specific

### Status
Expected limitation

### Description
This project depends on:
- WPF
- Win32 hooks
- Raw Input
- Windows message handling

It is not intended to be cross-platform in its current form.

### Why this matters
Most of the input architecture is tightly tied to Windows APIs.

---

## 10. Claude API key must not be committed

### Status
Important operational rule

### Description
The Anthropic API key should be loaded from an environment variable and never committed to source control.

### Current expected setup
The app expects:

- `ANTHROPIC_API_KEY`

to be set in the environment.

### Why this matters
Hardcoding secrets in the repository is a security risk.

---

## 11. Current screenshot behavior captures the full primary display

### Status
Known behavior

### Description
The screenshot feature currently captures the entire primary screen and stores it as base64 before sending it to the assistant.

### Why this matters
This may capture sensitive information unintentionally.

### Future improvement
Potential future enhancements:
- region capture
- active-window capture
- screenshot preview before send
- explicit screenshot confirmation

---

## 12. Current state summary

### Working now
- app launches
- chat UI works
- message sending works
- screenshot capture works
- low-level keyboard hook installs
- macro pad device can be identified
- foreground input behavior mostly works
- shutdown cleanup for the low-level hook works

### Still fragile
- background Raw Input reliability
- silent/message-only window migration
- hardcoded device matching
- long-term replay filtering robustness
- testability of input decision logic