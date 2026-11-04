# Incranalytic — Store listing draft

Historical pre-publication draft. On November 4, 2026, creator **Kitarie**
confirmed that **Incranalytic is available on the Microsoft Store**. The
checklist and reviewer notes below are retained as drafting history, not as a
record of the current live listing or outstanding certification work.
See the [availability update](preparation-status.md) and
[project overview](../docs/about-incranalytic.md).

## Short description

Design, visualize, and test your own incremental systems with connected blocks,
formulas, and conditional code.

## Description

Incranalytic is a classic desktop studio for people who want to build and
understand incremental systems. Create game progression, production loops,
growing resources, and other numerical systems using your own ideas and rules.

Start with an untouched workspace or import a compatible Incranalytic project.
Define symbols with your own meanings. Connect Input, Module, Previous-step,
and Output blocks, then write formulas and conditional IFCLOUD code. Run,
pause, step, and reset your simulation. Inspect a table, graph, dependencies,
and the actual calculations behind a block's output.

The studio does not use AI to generate your systems. You supply the creativity,
formulas, and code. It calculates and displays their behavior. Development of
the software itself has used substantial GPT-6.1SOL assistance: its creator
works alone, is inexperienced, and has no funding or development team. The
repository documents that process and its verification limits.

Export human-authored systems as C# with supporting runtimes for Unity.
Exported systems and required runtime files permit personal and commercial use,
modification, and distribution under the included export permission. Test the
integration in your target Unity project before relying on it.

## Features

- Connected grid workspace with custom conditional IFCLOUD modules.
- User-defined Values, formulas, scientific notation, and selected precision.
- Run/Pause/Step/Reset simulation, table, graph, and dependency views.
- Why-this-number calculation traces, explicit feedback memory, reusable groups.
- Help manuals, optional example loop, walkthrough, and local recovery.
- Editable project import/save, CSV results, and Unity C# export.
- Offline studio; no AI-service account, telemetry, or project upload.

## Requirements and limitations

Windows desktop, x64 package, English interface. Mouse/keyboard recommended.
No Unity installation is required to run the studio. Unity integration requires
the user's separate Unity environment. The private .NET runtime is included.

This is development software. Scientific arithmetic has finite precision
(1–1,000 significant digits) and truncates toward zero. There is no literal
infinity. Powers require integer exponents. Graphs show positive log10 values;
the table retains nonpositive results. Project/runtime limits are in the manual.
Imports support Incranalytic formats, not arbitrary C#/APK decompilation.
Desmos integration and third-party plugins are future ideas, not current features.

## Suggested metadata — verify in Partner Center

- Category: Developer tools, if available and appropriate.
- Price: Free.
- Language: English (United States).
- Keywords: incremental; simulation; formulas; progression; visual programming; Unity; IFCLOUD.
- Website/support: https://github.com/lucjon7722-web/Incranalytic
- Support issues: https://github.com/lucjon7722-web/Incranalytic/issues
- Privacy URL: pending publication of the approved Store policy.
- Age/content rating: owner must complete the actual questionnaire.
- Screenshots: pending capture and review of the current Store edition.

## Notes for certification reviewers

This is a WinForms desktop application packaged as MSIX with runFullTrust. It
needs ordinary desktop operation and user-selected filesystem access to read
projects, save authored work, and export code/CSV. It does not request admin
elevation or install a service. IFCLOUD is a bounded interpreter for authored
numeric conditions and assignments; it does not execute host C#, OS commands,
or downloaded modules. No GitHub updater is included in the Store package.

No login, purchases, test account, or online backend is required. On first launch,
choose Load example loop in the walkthrough. Click Step repeatedly: the counter
increments to ten and holds. Select the module to see real branch traces. Use
Simulation Table and Graph in the sidebar. Reset unlocks editing. Help opens
the manuals and walkthrough. Save .incr projects in a normal user folder;
Unity export creates C# files in a new folder at a user-selected destination.

Local development checks include arithmetic, IFCLOUD, workspace, recovery,
distribution, and exported C# parity using a Unity API stub. Actual Unity editor
integration, installed MSIX behavior, clean-machine checks, and WACK remain
pending until separately tested. Update these notes with the final results
before submission.
