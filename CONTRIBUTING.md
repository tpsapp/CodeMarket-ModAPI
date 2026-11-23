# Contributing to `CodeMarket.ModAPI`

Thank you for contributing to `CodeMarket.ModAPI`!

## Engineering Standards
* **Standard Library Primacy:** `CodeMarket.ModAPI.Core` targets `netstandard2.1` with **zero external NuGet packages**. Do not introduce third-party dependencies for utilities achievable cleanly with the BCL.
* **Domain vs. Presentation Separation:** Keep all pure registries, parsers, validators, and persistence logic inside `CodeMarket.ModAPI.Core` with zero `UnityEngine`, `BepInEx`, `HarmonyLib`, or `JavaMarket` references.
* **No Hardcoded Paths:** Always resolve the *Code Market* installation path via `GAME_DIR` / `$(GameDir)` or `BepInEx.Paths`.
* **Zero Warnings:** All changes must compile with `-c Release -p:TreatWarningsAsErrors=true` and pass the full headless Core test suite.

## Local Verification Before Opening a PR
```bash
export GAME_DIR="/path/to/steamapps/common/Code Market"
dotnet build CodeMarket.ModAPI.sln -c Release -p:TreatWarningsAsErrors=true
dotnet run --project tests/CodeMarket.ModAPI.Core.Tests -c Release --no-build
shellcheck -x scripts/*.sh
```

## Commit Style
Use [Conventional Commits](https://www.conventionalcommits.org/):
* `feat(core): ...`
* `fix(unity): ...`
* `docs: ...`
* `chore(scripts): ...`
