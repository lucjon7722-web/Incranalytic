# IFCLOUD code reference — Incranalytic

**IFCLOUD 0.1** is the custom numeric rule language in **Incranalytic Studio,
created by Kitarie**. This is its official language reference.

New to IFCLOUD code? Start with the [first-module tutorial and examples](ifcloud-code.md).
See [Incranalytic downloads and features](../README.md) or
[the creator and project overview](about-incranalytic.md).

IFCLOUD is Incranalytic's custom numeric rule language for workspace modules. It has C#-style braces, semicolons, assignments and conditions. It is interpreted as bounded numeric statements and does not compile or execute arbitrary C#, import libraries, or access operating-system services. Human authors supply all code.

```text
input amount;
input multiplier;
output result;

let candidate = amount * multiplier;
if (candidate >= 100) {
    result = 100;
} else {
    result = candidate;
}
```

Declare all ports at the start. Inputs are read-only. Outputs must be assigned on every branch that executes. `let name = expression;` declares a module-local variable; assignment can update locals or outputs. Local variables do not persist across ticks. An executed read of an unassigned variable is an error. Names are case-sensitive ASCII identifiers; letters have no built-in game meaning.

Arithmetic: `+ - * / ^`, parentheses, `abs(x)`, `min(a,b)`, `max(a,b)`. Power uses integer exponents from -100000 to 100000; `^` is power, not C# XOR. Constants accept decimal and scientific notation. Conditions support `> < >= <= == !=`, short-circuit `&& ||`, and `!`. Zero is false and nonzero is true; `true` and `false` are 1 and 0. Braces and semicolons are required. `//` comments continue to end of line.

## Workspace blocks

- **Input:** constant number, or a reference to a symbol on Values. Referenced Values use the previous-state snapshot.
- **Module:** custom IFCLOUD code; declared ports appear on the block.
- **Previous step:** outputs stored memory; its `next` input becomes memory after a successful tick. Initial memory is authored in the inspector.
- **Output:** records the connected value in the table and graph.

Drag right-side output ports to left-side input ports. One wire feeds an input; an output can feed several inputs. Move blocks by dragging their body. Middle/right drag pans; wheel zooms. Double-click a module to code it. Apply property edits with the inspector button. Undo/redo covers block changes, moves, wiring, templates and imported systems.

## Conditional loop example

Insert the example loop from the Workspace toolbar. Its Previous-step block starts at zero. The module increments it until it reaches ten, then keeps it at ten. Completed ticks output 1, 2, ..., 10, 10. The initial table row is explicitly labeled **preview**: it evaluates the first tick without committing memory. Reset discards memory and restarts from the authored initial conditions.

Modules execute once per tick. Interval does not automatically scale a module's output. If a rule needs `gain * interval`, wire an authored interval constant into that module. Values gain formulas still follow the selected per-second/per-tick mode. Workspace calculations and Values updates read the same previous-state snapshot and commit atomically. A failed tick changes neither Values nor Previous-step memory. Same-tick cycles require an explicit Previous-step boundary. Workspace Outputs are observable signals and do not overwrite Values.

## Why this number?

Select a block after Run/Step. The inspector shows the last successful execution's wire inputs, Values snapshot, condition result, assignments, outputs and pending memory. These are execution records, not AI explanations. Errors stop simulation; failed branches do not silently produce guessed values.

## Reuse and drafts

Save a selected module as a project template, then place independent copies from Saved modules. `.ifcloud` files share module source across projects. Blocks with the same Group label are framed together. `.ifgraph` exports reuse a selected group or the whole workspace; internal edges are preserved and inserted block IDs are regenerated. External group connections and referenced Values symbols must be supplied in the receiving project.

Projects can save incomplete wiring and draft code. Execution and Unity export require a valid connected system. Saving uses project format 2; version-1 projects remain readable. Keep a backup if you need to reopen a project in 0.2, which predates workspace support.

## Limits and export

500 Values, 500 blocks, 2000 wires and 100 saved module templates. A module has at most 16 inputs, 16 outputs, 32000 characters, 4096 tokens and 32 nested blocks. Loops, recursion, strings, arrays, classes, external imports and arbitrary functions are not part of IFCLOUD 0.1. Numeric precision is 1–1000 significant digits, truncated toward zero. History retains at most 2001 samples and approximately 100000 signal cells.

Unity export includes the same scientific, IFCLOUD and workspace runtimes. Copy every exported C# file into Assets. `GetWorkspaceValue(blockId, port)` exposes results; the export README lists Output block IDs. Exported C#9 code is compiled and compared with the studio in a Unity API stub; actual Unity project integration still needs testing.
