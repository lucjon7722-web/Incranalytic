# Incranalytic Studio 0.2

Start with the blank Values sheet. Add any case-sensitive symbol, a display name, an initial number and a gain formula. Letters have no predefined meanings. Initial numbers accept scientific notation, such as `1e1000000`.

Run advances simulation time. Pause stops advancement. Step applies one interval. Reset clears results and unlocks editing; the next run starts from the authored initial numbers. Saving preserves the model, not a live simulation.

Per-second mode applies `next = current + gain * interval`. Per-tick mode applies `next = current + gain`. All formulas read the previous tick's state and all changes apply simultaneously. This is fixed-step Euler simulation, not an exact continuous solver. Interval can affect results.

The table stores the latest 2,001 samples. The graph displays positive numbers on a log10 vertical axis with a sample-index horizontal axis. Zero, negative, and log10 magnitudes outside ±1,000,000 are omitted from the plot and remain available in the table. Dependencies shows formula links; Connect symbols edits the target gain formula.

Scientific arithmetic supports 1–1,000 significant digits, chosen per project. Each operation truncates toward zero. Exponents are arbitrary-size integers, subject to memory/input limits. This supports numbers far beyond floating-point range, but does not promise infinite storage or exact infinite precision. Formula powers currently accept integer exponents between -100,000 and 100,000. Supported functions: abs, min, max.

Import opens `.incr` projects. Export creates C# source for Unity, the shared arithmetic runtime, the editable project and integration instructions. No Unity installation is needed to export. Generated scripts are compiled and compared to the simulator with a Unity API stub; integration inside an actual Unity game still needs testing.

This base studio does not include Desmos, third-party plugins, arbitrary script import, conditional formulas, specialized prestige/event nodes, or a complete game exporter. Those remain future capabilities.

The installer includes private Microsoft .NET 10 and Windows Forms dependencies. It works offline and installs for the current Windows user. Uninstall offers a checked option to remove the private dependencies, and an unchecked option to remove known preferences and cache. It preserves user-created projects and never uninstalls shared machine runtimes. If you retain dependencies, the runtime files and their manifest remain in the installation folder. Remove them later only when no longer needed. Installer and application are unsigned development builds.

The product has no AI system generation, account, telemetry, or network calls. Software development has used AI assistance. Preview terms require legal review before a commercial release.
