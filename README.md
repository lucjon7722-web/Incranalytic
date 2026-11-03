# Incranalytic

Incranalytic is a planned Windows desktop tool for investigating incremental game systems. Its central job is to make progression understandable: show the numbers a game uses, trace how they are calculated, and explain how currencies, generators, upgrades, multipliers, timers, and resets affect each other.

## Project status

Planning and repository setup. No application or release is available yet.

## Product goals

- Import supported game packages and data sources, starting with Android APKs and expanding to extracted game files, configuration, save data, and developer-provided project exports.
- Detect engines and inspect available code, assets, scenes, and data. Initial targets include native Android, Unity, Godot, Cocos Creator, and web-based games.
- Identify incremental systems such as production rates, costs, scaling curves, bonuses, prestige, offline earnings, and achievements.
- Show raw values, formulas, units, dependencies, and example calculations in a searchable workspace.
- Explain each finding in plain language with a link to the source evidence and a confidence level. Distinguish recovered facts from inferences.
- Let users compare game versions or scenarios and export reports, tables, and extracted files.
- Preserve imported originals and process untrusted packages locally with resource limits.

## Planned Windows experience

A classic, dense desktop interface inspired by analysis tools such as Ghidra: project tree, dockable panels, code and data views, formula explorer, cross references, notes, and an evidence-backed findings panel. Incranalytic will use its own branding and visual assets.

## Scope and limits

APK decompilation is one input path, not the product's purpose. Recovered code may be incomplete, obfuscated, or unavailable, especially in native or encrypted builds. Incranalytic will report coverage and uncertainty instead of claiming to reconstruct original source code or every game mechanic.

The application is intended for education and analysis of software users are permitted to inspect. A future release will include Terms of Service, privacy information, third-party notices, installation instructions, and clear export behavior.

## Repository

This repository will hold the application source, documentation, tests, and release packaging. The implementation plan and licensing will be added as development begins.
