# Product direction: incremental system creation

Status: planning baseline, November 3, 2026. Examples are proposals for discussion, not implemented syntax.

## Core commitment

People create the systems. Incranalytic makes their rules and consequences visible and testable. AI-generated mechanics, formulas, and progression systems are not the product's purpose. The core authoring and simulation workflow should operate without an AI service.

The former APK/game-inspection workflow is no longer the primary product direction. Existing inspection code is an early prototype.

## Proposed creation loop

1. Declare meaningful values, starting amounts, and units.
2. Define production, purchasing, upgrades, conditions, or resets.
3. Inspect formulas, tables, graphs, and dependency diagrams.
4. Run a scenario with explicit starting conditions, duration, and player actions.
5. Compare results to expectations and revise the system.

For example, a creator could define a tree costing 15 apples, cost growth of 1.15 per purchase, and production of 1 apple per tree per second. A cost graph shows the next purchase cost at each owned count. A time simulation additionally needs a starting balance and a purchase strategy. Those assumptions should be visible rather than silently supplied.

## Language index

The index should document the exact syntax the editor accepts, with searchable categories and insertable examples. The editor and index should share one vocabulary.

| Concept | Purpose | Example |
| --- | --- | --- |
| Value | A named number and optional unit | Starting apples: 10 |
| Rate | Gain or loss over time | 1 apple per second per tree |
| Formula | Relationship between inputs | baseCost * growth ^ owned |
| Condition | Requirement for an action | apples >= nextCost |
| Action | Change to state | Buy a tree and subtract its cost |
| Event | Triggered behavior | Reach a milestone |
| Reset | Declared state changes | Reset apples and trees; retain prestige |

Invalid names, missing inputs, circular dependencies, and conflicting units need actionable explanations. Large numbers and rounding rules need explicit treatment.

## Visualization principles

- Derive every view from the same authored model.
- Label axes and units; distinguish owned count, level, and time.
- Distinguish formula plots from stateful simulations.
- Make intermediate calculations and dependencies inspectable.
- Show scenario inputs, purchase strategies, update rules, and rounding assumptions.
- Compare saved scenarios without overwriting the base design.

## Future ideas to investigate later

**Desmos:** clarify whether this is a graphing workspace, synchronized formula view, or both. Investigate embedding rights, connectivity, and offline behavior before selecting an implementation.

**User-created plugins:** define the extension interface, permissions, and behavior disclosure. Preserve the human-authored purpose and disclose external services a plugin uses.

**Complete installer:** supply all required runtimes and prerequisites, subject to redistribution rights. Verify installation on a clean supported Windows environment, plus upgrade and uninstall behavior. Users should not need to discover dependencies themselves.

## Open design decisions

- Is the first release a design and balancing workbench, or should it run and export a playable game?
- Should creation begin with formulas, forms, a node diagram, or connected views of one model?
- Should simulation support manual actions, an authored purchase strategy, or both?

These decisions remain open for discussion. The future ideas are recorded as brainstorming and are not yet implementation tasks.
