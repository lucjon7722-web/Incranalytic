# IFCLOUD code — getting started with Incranalytic

IFCLOUD is the custom numeric rule language used by **Incranalytic Studio,
created by Kitarie**. You write IFCLOUD code inside workspace Module blocks
to describe calculations, conditions, and output values for incremental systems.

This guide is part of the [official Incranalytic repository](../README.md).
For the complete syntax and limits, read the [IFCLOUD language reference](IFCLOUD.md).

## What can you build with IFCLOUD?

Use modules to calculate resource production, cap a value, switch between
upgrade conditions, or update a counter. Connect modules to Input,
Previous-step memory, and Output blocks to form a system you can simulate
and inspect. IFCLOUD uses C#-style braces and semicolons, but it is a separate,
bounded numeric language. It does not execute arbitrary C# or operating-system
commands.

## Your first IFCLOUD code example

This module multiplies an amount and caps the result at 100:

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

1. Open Incranalytic and choose Workspace.
2. Add two Input blocks, a Module block, and an Output block.
3. Double-click the Module and enter the code above. Apply the edit so its
   declared ports appear.
4. Set the Input constants to 30 and 2. Connect them to `amount` and
   `multiplier`, then connect the module's `result` to the Output block.
5. Step the simulation. The output is 60. Select the Module to inspect its
   inputs, branch decision, and assignment in **Why this number?**
6. Reset before editing. Change the first Input to 80 and Step again.
   The output is 100 because the cap applies.

The cap is an upper bound; this example does not clamp negative results to zero.

## How the code works

| Code | Meaning |
| --- | --- |
| `input amount;` | Declares a read-only input port. |
| `output result;` | Declares an output that the module must assign. |
| `let candidate = amount * multiplier;` | Calculates a temporary local value. |
| `if (candidate >= 100)` | Selects a branch using a numeric comparison. |
| `result = 100;` | Assigns the value sent to the output port. |

Declare ports at the start. Names are case-sensitive. Assign outputs on every
executed branch. Local variables last for one execution; they are not persistent
memory.

## A counter with feedback

Choose the built-in example loop from the Workspace toolbar, or open
[the conditional counter project](../samples/conditional-counter.incr).
It uses a Previous-step block initialized to zero and increments to ten before
holding there. Completed ticks produce 1, 2, 3, and so on through 10, then 10 again.

A Module runs once per tick. Feed values through a **Previous-step** block when
you need memory; a direct same-tick cycle is not valid. The initial table row is
a preview and does not commit memory. Reset restores the authored initial state.

## Save and reuse your work

Save the project as `.incr`. Reuse module source with `.ifcloud` files, or
groups and systems with `.ifgraph` files. Read the [workspace and reuse reference](IFCLOUD.md)
before moving a module that depends on other inputs or Values symbols.

Unity export supplies C# with the scientific-number, IFCLOUD, and workspace
runtimes. Test exported code in your own Unity project; the recorded automated
checks use a Unity API stub, not the Unity editor.

## Where to get Incranalytic

[Get Incranalytic from the Microsoft Store](https://apps.microsoft.com/detail/9nkdqhdljf78), published by **Kitarie**.
The separate Windows GitHub edition is available from
[official releases](https://github.com/lucjon7722-web/Incranalytic/releases/latest).

- [Incranalytic features and installation](../README.md)
- [IFCLOUD syntax, supported operators, and limits](IFCLOUD.md)
- [About Incranalytic and its creator, Kitarie](about-incranalytic.md)
- [Report an issue](https://github.com/lucjon7722-web/Incranalytic/issues)

For questions about Incranalytic or IFCLOUD, contact **Kitarie** at
[Lpj2010@outlook.com](mailto:Lpj2010@outlook.com).
