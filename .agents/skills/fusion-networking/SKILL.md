---
name: fusion-networking
description: Implement or review multiplayer gameplay with Photon Fusion 2 in this Unity project.
---

# Photon Fusion 2 networking

Use for networking architecture, sessions, replicated gameplay, authority, or Fusion code in this project.

- Check the installed Fusion package and its exact version before using an API. Consult current official Photon Fusion documentation when behavior or signatures are uncertain; never invent APIs.
- The design targets Host Mode. Keep game-critical decisions authoritative: roles, codes, eliminations, damage, scoring, spawning, and round transitions.
- Treat client input as a request. Validate target, range, visibility, alive state, protection, and cooldown on the authority before applying results.
- Replicate only state clients need. Do not expose private player data to its owner or other clients when recipient-specific visibility is required.
- Keep simulation, rendering, and UI responsibilities separate. Use Fusion's simulation callbacks for network state and presentation callbacks for visuals, according to the installed version's documented model.
- Check scene loading, object lifetime, reconnect behavior, and host-loss limits before promising continuity. Do not promise host migration unless implemented and verified.
- Keep multiplayer logic testable without coupling it to one minigame's HUD.
