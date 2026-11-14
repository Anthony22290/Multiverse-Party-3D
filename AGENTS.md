# MULTIVERSE PARTY 3D

Unity 6000.5.6f1 project. Preserve existing scenes, assets and uncommitted work. Read relevant skills under `.agents/skills/` before changing game systems.

The Unity CLI is installed at `C:/Users/antho/AppData/Local/Unity/bin/unity.exe`. The open Editor has `com.unity.pipeline`, so check `unity status --format json` and use `unity command` to inspect or change the live Editor. Add `--caller plugin --skill unity-cli` only to `unity command` invocations, not to `unity status`. After entering Play, run `unity status --until-ready --format json` before sending the next command. Check compilation and Play behavior when changing gameplay. Do not create a build unless requested.

The active project directory is `D:/RV/MULTIVERSE PARTY 3D`. The pirate arena and menu scenes live under `Assets/001_Scenes/`; game scripts live under `Assets/003_Scripts/`.

REA is installed for reverse-engineering reference applications. Gemini rejects REA's full MCP tool schemas, so the `rea_*` tools are disabled in this project's OpenCode config. For a specific reference target, use REA's CLI from bash instead: `C:/Users/antho/.local/rea-runtime/node-v24.22.1-win-x64/node.exe C:/Users/antho/.local/rea-package/node_modules/rea-agents/scripts/rea.mjs --help`. Do not inspect or copy another game's assets without a concrete target and purpose.
