namespace Incranalytic.App;

public static class IfcloudGuide
{
    public const string Text = """
IFCLOUD 0.1 — BUILT-IN LANGUAGE INDEX

PURPOSE
You write the rules. IFCLOUD evaluates numeric input/output modules in the workspace.
It is a small, bounded language with C#-style statements. It is not C# and cannot
execute C# libraries, access files or networks, or call operating-system commands.

QUICK START
Add Input → Module → Output. Drag a green output port on a block's right edge
to a blue input port on another block's left edge. Double-click a module to code it.
Add a Previous-step block to remember values across ticks and build feedback loops.

MODULE STRUCTURE
Declare ports first, then write statements. Names are case-sensitive.

input amount;
input multiplier;
output result;

let candidate = amount * multiplier;
if (candidate >= 100) {
    result = 100;
} else {
    result = candidate;
}

INPUT
input name;
A read-only number supplied by a connected wire. Every input requires a wire.
To use a Values-sheet symbol, add an Input block and enter that symbol in its inspector.
Blank Values-symbol field uses the block's constant instead.

OUTPUT
output name;
An output port the module must assign on the branch actually executed.
An unassigned output stops the tick; no state or memory changes are committed.
Use Output blocks to record any module port in the simulation table and graph.

LET / ASSIGNMENT
let intermediate = amount + 5;
result = intermediate * 2;
Inputs cannot be assigned. Outputs and declared local variables can be reassigned.
Locals belong to this module execution and do not persist between ticks.
Locals declared inside a branch must be assigned on any path that later uses them.

IF / ELSE / ELSE IF
if (amount > 10) {
    result = amount * 2;
} else if (amount == 10) {
    result = 20;
} else {
    result = amount;
}
Braces and semicolons are required. Only the selected branch executes.
The inspector records which branch ran and the numbers each assignment produced.

CONDITIONS
Comparisons: >  <  >=  <=  ==  !=
Boolean operators: && (and), || (or), ! (not)
true is 1; false is 0. Zero is false; every nonzero number is true.
Boolean operators short-circuit:
if (amount != 0 && 100 / amount > 2) { result = 1; } else { result = 0; }
The division is not evaluated when amount is zero.

ARITHMETIC
+ - * / ^ ( )
abs(x), min(a,b), max(a,b)
^ means power, not C# XOR. Powers require integer exponents from -100000 to 100000.
Numeric constants can use scientific notation, such as 1e1000, or decimals such as .5.
Operator order, from lowest to highest:
||, &&, == !=, < <= > >=, + -, * /, unary signs/!, powers.
Use parentheses to make intent explicit.

COMMENTS
// A comment continues to the end of the line.

PREVIOUS-STEP MEMORY / FEEDBACK
Previous-step emits its stored number. Its next input is remembered only after
the entire tick succeeds. Reset restores its authored initial memory.
Every Previous-step block updates simultaneously. A same-tick cycle with no
Previous-step boundary is rejected with a diagnostic.

EXAMPLE LOOP
Previous-step (initial 0) → Module.previous
Module.total → Previous-step.next and Output.value

input previous;
output total;
if (previous < 10) {
    total = previous + 1;
} else {
    total = previous;
}

The first completed tick outputs 1, then 2, ... until 10, then stays 10.
The initial row is labeled preview: it shows the first tick's calculation without
committing memory. A module executes once per tick. Use a named interval constant
as an Input if a module's rule needs gain × interval; tick length is never implicit.

WORKSPACE AND VALUES TIMING
Values gain formulas and workspace Input references read the same previous-state
snapshot. Values changes and Previous-step memory commit only if the full tick succeeds.
Workspace modules are pure calculations. Workspace Output blocks are recorded
signals and do not overwrite a Values symbol. Wire a Previous-step block for state.

REUSABLE MODULES AND GROUPS
Modules → Save selected as reusable module adds a template to this project.
Placing a template copies its code; editing one instance does not change others.
Export/import .ifcloud files to reuse code across projects.
Give blocks the same Group label to organize a system visually. Export that group
as .ifgraph to reuse its blocks and internal connections. External connections
and referenced Values symbols must be supplied in the receiving project.

EDITING AND DRAFTS
Use Apply block changes after editing properties. Apply valid code validates syntax
and removes connections to ports that were deleted. Undo restores those changes.
Save draft preserves unfinished code. Project Save works with unfinished wiring
or code; Run and Unity export require executable, connected systems.
Workspace Undo/Redo covers block edits, connections, movement and imports.
Reset the simulation to unlock editing. Camera movement remains available while paused/running.

WHY THIS NUMBER?
Select a block after Run/Step to inspect its last successful calculation.
The inspector shows wire inputs, Values snapshots, conditions, assignments,
outputs and what memory will become on the next tick. It uses the execution trace;
it does not ask an AI model to explain the result.

LIMITS
500 Values, 500 blocks, 2000 wires and 100 saved module templates per project.
Each module: 16 inputs, 16 outputs, 32000 characters, 4096 tokens, 32 nested blocks.
No while/for loops, recursion, strings, arrays, classes, arbitrary functions or external imports.
Scientific precision: 1–1000 significant digits; truncation toward zero.
All finite-storage and arithmetic limits from the general index still apply.
Recorded history retains at most 2001 samples and approximately 100000 signal cells.
Graph shows positive values on a logarithmic axis; the table includes other values.

UNITY EXPORT
Workspace projects export ScientificNumber.cs, Ifcloud.cs, WorkspaceRuntime.cs and
IncranalyticSystem.cs. Copy all exported C# files into Assets. The same interpreter,
memory boundaries and numeric runtime are used by the studio and the generated code.
Use GetWorkspaceValue(blockId, port) for outputs; output block IDs are listed in the
export README. Validate the integration in your Unity project.
""";
}
