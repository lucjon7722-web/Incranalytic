# Install and manage versions

Download the versioned Setup EXE offline, or keep **Incranalytic-Installer.exe**,
the reusable Windows x64 Version Manager. Public releases need no account or token.

Click Refresh, choose a version, read its notes, and click Install. Save and close
the studio first. The manager checks size and SHA-256 before launching setup.
Verified cached setup can be reused. One version is installed at a time. Older
versions may not open newer projects; keep backups. A protocol/security update
can require a new manager, but ordinary studio releases do not.

Setup includes the manager under Help and in the Start menu, English selection,
terms, destination and shortcuts. The studio and private .NET runtime install
together. The studio works offline. Unity is only needed separately to integrate
exports into a Unity game.

## Optional private access

Private access starts off. Enable it only when needed, then enter a fine-grained
GitHub token scoped to this repository with Contents: Read. Browser cookies are
not reused. Saved credentials load only when private access is explicitly
enabled. Remembering uses Windows Credential Manager for this Windows account;
Forget deletes saved access.

Tokens go only to api.github.com, never to cross-host redirects, projects, or
packages. Anonymous refresh does not delete saved access. Older private-prototype
managers display token fields directly; leaving them empty works for public releases.

## Download verification

Only nondraft releases with matching versioned Setup EXE/checksum assets appear.
Previews are optional. HTTPS downloads use approved GitHub hosts, expected names,
size, SHA-256, and GitHub asset digest where supplied. Corrupt, incomplete, and
cancelled downloads are not launched. Integrity checking is separate from
publisher signing; current builds are unsigned.

## Uninstall

Installed Apps and the Start menu offer uninstall with these choices:

- Remove bundled .NET dependencies (checked).
- Also remove preferences, cache, and saved GitHub access (unchecked).
- Also delete unsaved recovery copies (separate and unchecked).

Saved projects are preserved. Shared runtimes are never removed. Keeping
dependencies can leave files in the install directory. Delete the standalone
manager separately if unwanted. Older uninstallers may not know about recovery
or saved credentials.
