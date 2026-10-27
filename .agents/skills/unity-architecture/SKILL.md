---
name: unity-architecture
description: Structure or modify Unity scenes, assets, scripts, packages, and shared gameplay systems in this project.
---

# Unity project architecture

Use for Unity implementation and structural changes in MULTIVERSE PARTY 3D.

- Preserve Unity asset `.meta` files and stable GUID references when moving or editing assets.
- Keep shared loop systems (round phases, timer, teams, scoring, HUD contracts, respawn) separate from minigame rules and presentation.
- Prefer small components and explicit interfaces over a single global manager. Keep network state out of view-only UI.
- Keep reusable tuning in serialized assets when designers need to adjust it; avoid hard-coding per-mode balance values into shared systems.
- Check the installed Unity and package versions before relying on APIs. Inspect `Packages/manifest.json` and `packages-lock.json` before proposing package-dependent code.
- Avoid claiming a feature works from compilation alone; describe what was actually exercised in Unity.
