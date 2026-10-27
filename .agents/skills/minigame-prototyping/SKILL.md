---
name: minigame-prototyping
description: Plan or implement a small, playable minigame prototype for MULTIVERSE PARTY 3D.
---

# Minigame prototyping

Use for turning a minigame concept into a bounded, playable Unity slice.

- Keep the shared player identity, input conventions, round states, and results flow consistent across modes.
- Start with one map and the smallest complete loop: enter, play, resolve a winner, show results, and restart or return.
- Prefer the GDD's recommended first online mode, Código Mortal, when no other mode is requested. Treat its rules as design input, not proof of implementation.
- Separate mode rules from shared session, timer, score, and UI systems. Add a mode-specific system only when a concrete rule needs it.
- Define completion in observable terms: a player can finish a full round, the outcome is correct, and another round can start.
- Do not expand to additional maps, cosmetics, or variants before the core loop is playable.
