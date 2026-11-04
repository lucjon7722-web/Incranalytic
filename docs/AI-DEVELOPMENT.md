# How Incranalytic is made

Incranalytic has been made with substantial AI assistance from **GPT-6.1SOL**
through Codex. This includes assistance writing implementation code, interface
changes, documentation, tests, and packaging scripts. It would be misleading to
present this as a traditionally hand-coded product from an experienced team.

The creator is working alone, is inexperienced in software development, and has
no project funding or development team. AI assistance makes it possible to turn
the creator's ideas into a working studio, learn through iteration, and perform
checks that would otherwise be difficult to carry out alone. It does not remove
the creator's responsibility for the project or guarantee the software is correct.

Human direction determines the purpose, behavior, priorities, and release
decisions. The public source and documented checks make the result inspectable.
AI-written tests can miss issues, and passing checks are evidence with limits,
not independent certification or a security audit. Release notes distinguish
automated checks from real engine and clean-machine testing.

## Development assistance is separate from the product

The studio does **not** use AI to generate incremental systems. It has no model
service integration, prompt-based system generator, AI account requirement, or
project upload to an AI provider. People write their own values, formulas,
IFCLOUD rules, and connections. Incranalytic executes those rules and displays
the results so human creativity and reasoning can be seen and tested.

The base studio works offline. The optional Version Manager contacts GitHub
when fetching release information or downloads. Public downloads need no token.

## Current verification limits

The project has automated numeric, language, workspace, distribution, recovery,
and application checks. Exported C# is compiled and compared with studio behavior
using a small Unity API stub. This is not the same as testing in Unity itself.
Installer checks run on the development Windows computer with an isolated test
installation; they do not represent a clean-machine or independent audit.

This is a free public development release. The studio source is viewable under
reserved terms, rather than an open-source license. Exports and required runtime
files have separate personal and commercial use permissions.
