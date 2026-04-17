# Planning Decisions

Locked decisions made in planning sessions that should be respected by future work. Decisions are added here when they're made, not when they're implemented. If a decision is later reversed, remove it from this file — do not leave stale decisions.

## Settings architecture (from Task 1 planning)

- Settings v1 scope is: AI mode settings, global guidelines, and API key storage shape. Everything else (macro keybindings, Focus Mode window behavior, screenshot settings, session behavior) is out of scope for the settings milestone.
- Modes stay fixed at Quick, Thinking, DeepThinking. No user-created modes in this milestone or soon after.
- Each mode supports a primary + fallback provider config. Fallback shares the same system prompt as primary; only provider/model/tuning differ.
- Global guidelines are stored once at the top level and combined with each mode's system prompt at request time.
- API keys will be stored in settings (moving off environment variables) with Windows DPAPI protection. DPAPI work itself is deferred to the persistence task.
- Settings will persist as a local JSON file under `%LocalAppData%\ChatVisual\settings.json`. JSON load/save is a later task, not part of the data model task.

## Project-level decisions

- The app remains Windows-only. Cross-platform is not a goal.
- This is a personal project, not commercial. Multi-user scenarios are out of scope.
- The simplified hook-only macro model is the current approach. Raw Input device-specific handling is superseded unless the simplified model stops working.