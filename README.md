# Incranalytic

A planned Windows studio for people to create, visualize, and test incremental game systems, then export scripts or code for their game. Users define the values, rules, formulas, and relationships. Graphs, tables, charts, and diagrams make the consequences of those choices visible.

## Purpose and authorship

Incranalytic supports human creativity and reasoning. Its purpose is not to use AI to generate incremental game systems. People author their mechanics and make the design decisions; the tools calculate, simulate, visualize, and help them test those decisions.

The planned core workflow does not require an AI model, an AI account, or sending a project to an AI service. This repository has used AI-assisted software development. That process is disclosed separately from the functionality offered to users.

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

## Current status

The project is changing direction from game inspection to system creation. The existing **0.1 development preview** was built around inspection and remains an early technical foundation. It includes a formula evaluator, a level table, file inspection, and report exports. It does not yet provide the visual system authoring workspace described above.

See [the current preview and build instructions](docs/preview-0.1.md) for what actually works today, and [the product direction](docs/product-direction.md) for the new planning baseline. This update changes documentation only.

## Future ideas — brainstorming

- Built-in Desmos Graphing Calculator integration.
- User-created plugins.
- A complete Windows installation package that supplies the application and all required dependencies, with no manual prerequisite setup.

These ideas are recorded for later investigation. Integration design, licensing, offline behavior, plugin permissions, and distribution details have not been selected. There is no committed implementation or release date for them.

## Privacy and ownership

Local operation and transparent calculations are design goals. Users should be able to inspect and retain the rules they authored. The existing preview processes files locally and has no telemetry or online AI provider.

Source licensing has not yet been selected. Terms and packaging will be revised for the creation-focused product before public release.
