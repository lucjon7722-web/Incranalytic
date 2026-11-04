# Incranalytic Studio — IFCLOUD code and incremental systems

**Incranalytic is a Windows desktop studio created by Kitarie** for designing,
simulating, and understanding incremental systems. **IFCLOUD is its custom
numeric rule language**: write conditional code, connect workspace blocks, and
inspect how your rules change values over time.

Build game progression, resource production, and feedback loops using your own
formulas and IFCLOUD code. Test them with simulation tables, graphs, and
calculation traces, then export C# with supporting runtimes for Unity.

This is the official Incranalytic GitHub repository and home of the IFCLOUD
language documentation.

- [Get Incranalytic from the Microsoft Store](https://apps.microsoft.com/detail/9nkdqhdljf78)
- [Learn IFCLOUD code: first module and examples](docs/ifcloud-code.md)
- [IFCLOUD language reference](docs/IFCLOUD.md)
- [About Incranalytic and Kitarie](docs/about-incranalytic.md)
- [Download GitHub releases](https://github.com/lucjon7722-web/Incranalytic/releases/latest)

**Development transparency:** This software has been made with substantial
assistance from **GPT-6.1SOL** through Codex. Its creator is working alone, is
inexperienced, and has no project funding or development team. AI assistance
makes building and testing the studio feasible. It does not make results
automatically correct. Read [how it is made](docs/AI-DEVELOPMENT.md).

**The studio does not use AI to generate systems.** People author every rule,
formula, connection, and design decision. The software executes and visualizes
those choices. There is no AI service, AI account requirement, or project upload.

## Get Incranalytic

[**Get Incranalytic from the Microsoft Store**](https://apps.microsoft.com/detail/9nkdqhdljf78) — published by **Kitarie**.

### GitHub edition — 0.5.0

Windows 10/11 x64. Download from [official releases](https://github.com/lucjon7722-web/Incranalytic/releases/latest):

- **Incranalytic-0.5.0-Setup.exe** installs this version offline with its private .NET runtime.
- **Incranalytic-Installer.exe** is the reusable Version Manager. Keep it to choose future or older versions. Public releases need no GitHub token.

Setup includes English language selection, terms, destination and shortcut
choices, Installed Apps registration, and an uninstaller. No Unity installation
is needed to use the studio. These GitHub installer builds are unsigned. Save and close the studio
before replacing it. See [installation and version management](docs/version-manager.md).

## What 0.5.0 includes

- A left sidebar for Workspace, Values, Simulation Table, Graph, and Dependencies.
- Separate searchable **System Manual** and **IFCLOUD Manual** windows under Help.
- A first-run walkthrough with an optional example loop; reopen it through Help.
- View toggles for grid, snapping, live block values, inspector, tooltips, recovery, and simulation settings. Advanced settings start hidden.
- A dismissible blank-workspace quickstart and IFCLOUD editor line numbers/keyword colors.
- Local recovery enabled by default, checked every 30 seconds for changed projects and open code drafts. Recovery never overwrites saved projects. Available copies are offered after interruption or through File.
- Save/Save as, unsaved-change prompts, atomic project replacement, and previous-file .bak backups.

## Authoring and simulation

Start blank or import a compatible .incr project. Values are user-defined,
case-sensitive symbols: A can mean coins, production, or anything else.
Structured initial values and gain formulas are optional alongside the workspace.

Place Input, Module, Previous-step memory, and Output blocks. Write IFCLOUD
rules with C#-style declarations, arithmetic, conditions, and assignments.
IFCLOUD is a bounded custom language, not arbitrary C# execution. Group blocks,
save reusable module templates (.ifcloud), and import/export groups or systems
(.ifgraph). **Why this number?** shows actual inputs, conditions, branch decisions,
assignments, and outputs.

Run, Pause, Step, and Reset share the table and graph. Choose a fixed interval
and per-second or per-tick Values gains. Values read previous state and update
together; workspace feedback uses explicit Previous-step memory. Projects save
the authored model, not a live simulation session.

Scientific arithmetic supports 1–1,000 significant digits and arbitrary-size
integer exponents, subject to memory and input limits. Operations truncate
toward zero; there is no literal infinity or infinite precision. Powers require
integer exponents. The graph uses positive log10 values and sample index; it
omits nonpositive/out-of-range points. The table retains values and elapsed time.
History is limited to 2,001 samples and approximately 100,000 signal cells.
Projects support up to 500 Values and 500 blocks.

Unity C# export includes the same arithmetic/interpreter/workspace runtimes.
Exports and required runtime files may be used, modified, and distributed in
personal and commercial games or applications under [export permission](EXPORT-LICENSE.txt).
Test integration in your Unity project. Import does not decompile APKs or
arbitrary compiled scripts. Specialized event nodes, Desmos, user-created
plugins, and other engine targets remain future ideas.

Read [the studio guide](docs/studio-guide.md), [IFCLOUD reference](docs/IFCLOUD.md),
and [0.5 release notes](docs/releases/0.5.0.md).

## Privacy, source, and ownership

Projects, calculations, preferences, and recovery stay local. The studio has no
telemetry or online AI provider. Only the optional Version Manager contacts
GitHub for release metadata/downloads. Private access starts off; remembering a
token is optional through Windows Credential Manager.

Your authored systems remain yours, subject to imported material's rights.
Studio source is **reserved source, not open source**. Viewing and local personal
builds are permitted; studio redistribution requires separate permission. Read
[studio terms](LICENSE), [export permission](EXPORT-LICENSE.txt), and
[third-party notices](THIRD-PARTY-NOTICES.md).

## Contact and support

Contact **Kitarie**, the creator of Incranalytic, at [Lpj2010@outlook.com](mailto:Lpj2010@outlook.com).
You can also [report bugs or ask questions on GitHub](https://github.com/lucjon7722-web/Incranalytic/issues).

## Build and checks

Incranalytic is available on the Microsoft Store under publisher **Kitarie**.
Store packaging is documented in the [Store submission guide](store/submission-guide.md).
The [preparation record](store/preparation-status.md) preserves the earlier
build/check history; it is not a live certification dashboard. The GitHub
installers are a separate distribution channel.

Developers need the .NET 10 SDK and Inno Setup 6 to build Windows setup.
End users receive private runtimes; no separate SDK or engine is needed.

~~~powershell
dotnet build src/Incranalytic.App -c Release
dotnet build src/Incranalytic.Installer -c Release
dotnet run --project tests/Incranalytic.StudioTests -c Release
dotnet run --project tests/Incranalytic.WorkspaceTests -c Release
dotnet run --project tests/Incranalytic.DistributionTests -c Release
./scripts/Build-Installer.ps1 -Compiler 'path/to/ISCC.exe' -IncludeTestInstaller
./scripts/Test-Installer.ps1
~~~

Checks cover arithmetic, conditional execution, failed-tick atomicity, 500-block
graphs, reusable systems, project saves, recovery, credentials/download integrity,
installed UI flows, and install/uninstall preservation. Exported C# is compiled
and compared with studio results using a Unity API stub. These checks are not an
independent audit. Actual Unity editor integration and a clean Windows VM have
not been tested. The unsigned installer uses Inno Setup; see its
[licensing information](https://jrsoftware.org/isorder.php). Older releases and
the [original inspection preview](docs/preview-0.1.md) are historical.
