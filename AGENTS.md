# AGENTS.md

Guidance for AI coding agents working on this project.

## Before starting any task

Read these files first for project context:

- `docs/project-context/layer1-project.md` — what this project is (product, users, scope)
- `docs/project-context/layer2-state.md` — current state of the code (existing classes, architecture decisions, conventions)

These are the minimum context needed to produce proposals that fit this project.

## Planning decisions are binding

Locked planning decisions live in `docs/planning/decisions.md`. Do not propose work that contradicts decisions in this file.

If a decision in that file seems wrong given new information, raise it with me explicitly — do not silently work around it or ignore it.

## Code conventions

Code conventions are listed in the "Code conventions in use" section of `layer2-state.md`. Follow them when writing new code.

## Scope discipline

This is a personal Windows-only project. Do not propose:
- Cross-platform abstractions
- Multi-user or networked features
- Enterprise-scale architecture patterns
- Production-grade infrastructure (telemetry, monitoring, auto-update, installers)

unless explicitly asked.