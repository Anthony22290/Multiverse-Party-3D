---
name: multiplayer-testing
description: Verify multiplayer behavior, authority, synchronization, and failure cases for this Unity game.
---

# Multiplayer verification

Use when validating networked gameplay or diagnosing multiplayer defects.

- Exercise at least one host and one client in separate game instances. Add players when a rule depends on team size or voting.
- Verify the full round loop and compare each player's view of movement, phase, score, and outcome.
- Check authority boundaries by sending invalid or stale requests, including invalid targets, wrong codes, out-of-range actions, dead players, and active protection where relevant.
- Include practical connection failures: latency, disconnect, host exit, and reconnect when the feature claims to support them.
- Report environment, steps, expected result, actual result, and whether the behavior was reproduced. Separate editor checks from build checks.
- Do not say a multiplayer feature is verified if it was only inspected statically or run with a single instance.
