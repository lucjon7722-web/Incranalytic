# Incranalytic

A Windows desktop workbench for investigating the numbers, formulas, and progression systems in incremental games. The classic interface includes a file tree, searchable findings, explanations, source evidence, and a formula calculator with a level table.

## Version 0.1 development preview

Working features:
- Open APK/ZIP packages, extracted folders, JSON, CSV, and text/source files.
- Find JSON numeric literals and explicit formula strings; find numeric assignments in text and source code.
- Group likely currencies, production, upgrades, prestige, and offline systems. Each result identifies its source and distinguishes literal values from inferred roles.
- Evaluate arithmetic, powers, variables, parentheses, floor, ceil, sqrt, log, min, max, and pow. Compare levels 0–20.
- Export HTML, JSON, and CSV reports; copy original packages without modification; extract files without overwriting existing files.
- Use the included fictional Clockwork Orchard demo with 20 known findings.

This build does not yet decompile DEX/native code, decode Unity assets or Godot packs, rebuild APKs, or recover every mechanic from compiled games. Those require engine-specific integrations. Binary files remain visible in the file tree, and the Overview tab reports analysis coverage. No game code is executed.

## Run

Extract the portable Windows x64 package, then run `Incranalytic.exe`. Use **Demo** to try the known data set. Open **Formula Lab** and press **Calculate + level table**: the default level-10 tree cost should be approximately **60.6833660356**.

## Build and verify

Requires the .NET 10 SDK on Windows. There are no third-party package dependencies.

```powershell
dotnet build src/Incranalytic.App -c Release
dotnet run --project tests/Incranalytic.Smoke -- samples/incremental-demo.json
dotnet publish src/Incranalytic.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The test runner checks known findings, formula results, error cases, APK data inspection, unchanged original export, reports, extraction, and path traversal rejection.

## Terms and privacy

Educational purpose. Users are responsible for their actions and for having the rights or permissions required to inspect imported material. This development preview is provided as-is. Its findings may be incomplete or incorrect. No waiver applies where prohibited by law. Preview terms need legal review before public release. Full preview terms and coverage are available in **Help → Terms, privacy and coverage**.

Processing is local. This version has no telemetry, upload service, account system, or online AI provider. Imported originals are never modified. Archive limits: 20,000 files, 128 MB per file, and 1 GB expanded total; suspicious paths, symbolic links, and excessive compression ratios are rejected.

Project source licensing has not yet been selected. Third-party game files and assets are not included.
