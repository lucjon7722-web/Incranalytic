# Incranalytic

A Windows studio for people to create, visualize, and test incremental systems, then export scripts or code for their game or application. Users define the values, rules, formulas, and relationships.

## Purpose and authorship

Incranalytic supports human creativity and reasoning. Its purpose is not to use AI to generate incremental game systems. People author their mechanics and make the design decisions; the tools calculate, simulate, visualize, and help them test those decisions.

The core workflow does not require an AI model, an AI account, or sending a project to an AI service. This repository has used AI-assisted software development. That process is disclosed separately from the functionality offered to users.

## Intended experience

- Start with a fresh, untouched system or import an existing compatible system. Supported import formats remain to be defined.
- Edit one system through connected structured fields, typed formulas, and nodes.
- Define named values, currencies, generators, costs, upgrades, timers, milestones, and resets.
- Write formulas with an index of supported words, functions, variables, units, and examples that explains what users can type.
- See graphs, tables, charts, and dependency diagrams connected to the same authored system.
- Change a parameter and inspect its effects across levels, time, or selected scenarios.
- Test expectations and inspect the calculation steps and assumptions behind each result.
- Save and share editable systems with their rules intact.
- Export authored systems as scripts or code for a target game engine. Target languages and integration contracts remain to be selected; export should preserve tested behavior.
- Work in an original classic desktop interface inspired by tools such as Ghidra.

These are product goals, not claims that the current application already implements them.

## Version 0.2 — base studio

Implemented in the development build:

- Blank Values sheet with user-defined, case-sensitive symbols and meanings.
- Structured initial values and editable gain formulas; optional example system.
- User-selected intervals and per-second or per-tick updates.
- Run, Pause, Step, Reset; simultaneous updates from the previous state.
- Scientific arithmetic with 1–1,000 selected significant digits and arbitrary-size integer exponents, subject to memory and input limits. Operations truncate toward zero; this is not infinite storage or infinite precision.
- Simulation table, positive-value logarithmic graph, formula dependency diagram and connection editor.
- Searchable index and guide, project save/import, simulation CSV export.
- C# export for Unity with the same arithmetic and fixed-step simulation rules.
- Offline Windows x64 installer, English language page, terms, destination/shortcut choices, Windows Installed Apps registration, and an uninstaller with optional private-runtime cleanup.

The graph retains 2,001 samples and omits nonpositive or out-of-range plotted values. Powers require integer exponents. Conditional/event nodes, arbitrary script import, Desmos, and user-created plugins remain future work. A project import means the `.incr` format. The broad intended experience above includes future goals.

Read [the studio guide](docs/studio-guide.md). The earlier [inspection preview](docs/preview-0.1.md) is historical; the application now starts in the studio.

## Build and verification

Requires the .NET 10 SDK on the build computer. Users receive the runtime bundled privately with the app.

```powershell
dotnet build src/Incranalytic.App -c Release
dotnet run --project tests/Incranalytic.StudioTests
./scripts/Build-Installer.ps1 -Compiler 'path/to/ISCC.exe'
./scripts/Test-Installer.ps1
```

The studio checks cover scientific arithmetic, selected precision, invalid input, deterministic updates, project round trips, and compilation/parity of generated C#9 using a Unity API stub. Installed-app checks exercise the Values binding, simulation controls, rendering and embedded export runtime. Installer checks cover registration, install, dependency retention, full removal and preservation of user projects on the current Windows host. A clean Windows VM and real Unity editor integration have not been tested.

Inno Setup is a build tool, not an end-user dependency. This development installer uses the noncommercial compiler; obtain the appropriate Inno Setup license before commercial distribution. Installer and application are unsigned development builds.

## Future ideas — brainstorming

- Built-in Desmos Graphing Calculator integration.
- User-created plugins.
- Broader engine integrations and richer simulation workflows.

These ideas are recorded for later investigation. Integration design, licensing, offline behavior, plugin permissions, and distribution details have not been selected. There is no committed implementation or release date for them.

## Privacy and ownership

Local operation and transparent calculations are design goals. Users should be able to inspect and retain the rules they authored. The existing preview processes files locally and has no telemetry or online AI provider.

Source licensing has not yet been selected. Preview terms are included in the installer and the Help menu. Legal review and release signing remain public-release tasks.
