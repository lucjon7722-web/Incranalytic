# Incranalytic Studio 0.5

Start in **Workspace**, a pannable/zoomable grid for building conditional systems. Add blocks from the toolbar, move them by dragging, and connect right-side output ports to left-side input ports. Middle/right drag pans; the wheel zooms. Fit brings the scene into view.

**Input** supplies a constant or a user-defined Values symbol. **Module** contains custom IFCLOUD code with declared ports. **Previous step** stores a number between ticks. **Output** records its connected value in the simulation table and graph. Double-click a module to code it. Use the inspector's Apply button for title, group, constant and symbol edits. Undo/redo covers workspace edits and wiring.

Use **Insert example loop** to try a counter that increments to ten and then holds. The first table row is a labeled preview; it does not commit state. Completed ticks output 1, 2, ..., 10. Previous-step blocks make feedback timing explicit. Same-tick cycles require a Previous-step boundary.

Read **Help → IFCLOUD Manual** inside the app, or the installed `IFCLOUD.md`. The language supports numeric input/output declarations, local variables, assignments, if/else, comparisons, Boolean conditions, arithmetic and min/max/abs. It does not execute host C#, external libraries or operating-system commands. Creators write the rules; the software has no AI system generation.

Select a block during or after simulation for **Why this number?**: its actual wire inputs, condition results, assignments and outputs. Values snapshot and pending memory are shown where applicable. The record is from the last successful tick. A failure commits no Values changes or feedback memory.

Use shared Group labels to organize blocks. Save a selected module as a reusable project template; new instances copy its code. Export/import `.ifcloud` for source modules. Export/import `.ifgraph` for reusable groups or whole systems, preserving internal wiring with fresh block IDs. Referenced Values and external group connections must be provided in the receiving project.

The blank **Values** sheet remains the project's dictionary of arbitrary symbols and meanings. Enter an initial number and arithmetic gain formula. Letters have no predefined purpose. Per-second mode applies `next = current + gain * interval`; per-tick mode applies `next = current + gain`. Values gain formulas and workspace Input references use the previous-state snapshot. Workspace modules execute once per tick without automatic interval scaling: wire an authored interval input when needed.

Run advances simulation time; Pause stops advancing; Step applies one interval. Reset clears history and memory and unlocks editing. Saving preserves the authored model, not a running simulation. Project save accepts unfinished code and wiring, replaces the file atomically and keeps its previous version as `.bak`. Run and Unity export require a valid executable system. Version-1 projects remain readable; updated files use format 2.

The table/graph include Values and workspace Output signals. History retains at most 2,001 samples and approximately 100,000 signal cells; older samples are dropped. Graphs display positive numbers on a log10 vertical axis and sample-index horizontal axis. Zero, negative and log10 magnitudes outside ±1,000,000 remain in the table but are omitted from the graph.

Scientific arithmetic supports 1–1,000 selected significant digits. Each operation truncates toward zero. Exponents are arbitrary-size integers subject to memory and input limits; this does not imply infinite storage or exact infinite precision. Powers accept integer exponents between -100000 and 100000. Project limits: 500 Values, 500 blocks, 2000 wires and 100 saved module templates.

Unity export creates C# source, the shared numeric/interpreter/workspace runtimes as needed, an editable project and integration instructions. Copy every exported `.cs` file into Assets. No Unity installation is needed to export. C#9 output is compiled and compared with the studio through a Unity API stub; actual Unity project integration still needs testing.

The offline installer includes private Microsoft .NET 10 / Windows Forms dependencies, an English-only language page, terms and shortcut/destination options. Uninstall can remove or retain private runtime files; projects are preserved and shared machine runtimes are never removed. A separate unchecked option removes known preferences and cache. Application and installer are unsigned development builds.

Desmos, third-party plugins, arbitrary C# imports and a complete game exporter remain future work. Source licensing and preview terms need review before commercial distribution. Studio projects stay local, with no telemetry or online AI provider. The optional Version Manager accesses GitHub for release listings and downloads; private access requires a user-provided token. Development has used AI assistance.

Use Help → Install / manage versions or the Start menu Version Manager to choose releases. You can also keep the standalone Incranalytic-Installer.exe. See version-manager.md for private repository access, credential controls, and downgrade compatibility.

## Navigation and getting started

Use the left sidebar for the five work views. Help opens separate searchable System and IFCLOUD manuals and the Getting started walkthrough. The first launch shows the walkthrough; loading its example is optional. The blank-workspace card offers input, module and example actions. IFCLOUD editor checkboxes toggle line numbers and keyword colors.

## View options and recovery

View toggles grid, snapping, live block values, inspector, tooltips, automatic recovery and simulation settings. Simulation settings start hidden. Preferences persist for the current Windows account. Local recovery starts enabled and checks for changes every 30 seconds, including open code drafts. Snapshots stay in LocalAppData/Incranalytic/recovery, separate from saved projects. After interruption the studio offers available copies; File → Recover unsaved work reopens the chooser. Saving or explicitly discarding clears this window’s recovery. Recovery is a periodic checkpoint, so use Save or Save draft for immediate preservation. Other active studio windows retain their own recovery. Uninstall preserves unsaved recovery unless its separate removal option is checked.

## Export rights

Exports and required runtime files can be used, modified and distributed in personal or commercial systems under EXPORT-LICENSE.txt, which accompanies each export. Studio source uses separate reserved terms.
