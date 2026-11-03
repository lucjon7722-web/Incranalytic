# Install and manage versions

Keep **Incranalytic-Installer.exe**. This reusable, self-contained Windows x64 Version Manager discovers published GitHub versions and downloads the setup you choose. It also ships with studio 0.4 under **Help → Install / manage versions** and the **Incranalytic Version Manager** Start menu shortcut.

Click Refresh, choose a version, read its notes and click Install. The manager verifies its download's size and SHA-256 before launching the normal setup wizard. Save and close the studio first. The manager reuses a verified cached installer if you need to retry. You may select an older version; older software may not understand newer project formats, so retain your project backups. Keep the standalone manager when switching to older releases that predate the built-in menu.

The manager does not need to be redownloaded for every studio release. Future releases must retain the versioned `Incranalytic-<version>-Setup.exe` / `.sha256` asset names. A protocol or security update to the manager itself can still require a new manager build. It installs one selected studio version at a time, rather than parallel installations.

## Private repository access

The repository is currently private. Browser cookies are not read by the manager. Enter a GitHub fine-grained personal access token scoped to the Incranalytic repository with **Contents: Read**. Public repositories can be accessed anonymously. The manager uses GET requests only and sends the token only to api.github.com, never to cross-host download redirects.

Remembering is optional and initially off. **Remember in Windows Credential Manager** saves access for the current Windows account on this computer. **Forget saved token** deletes it. Tokens are never bundled with installers, included in URLs, logged, or written to project files. The studio remains usable offline without any token.

Uninstalling 0.4 offers removal of preferences, cached installers and the saved token. If you downgrade and uninstall using an older uninstaller, use the manager's Forget button first; earlier uninstallers do not know about the new credential. The standalone manager can be deleted separately when no longer needed.

## Download safety and verification

Only nondraft releases with matching installer and checksum assets are offered. Preview releases are optional. Downloads are restricted to approved GitHub HTTPS hosts, checked against the exact expected filename, size, release SHA-256 and GitHub asset digest where supplied. Incomplete/corrupt/cancelled downloads are not executed. This verifies release integrity, not Authenticode publisher signing; these development packages remain unsigned.

The manager includes its own runtime so it survives changes to a studio's dependencies. Setup also bundles the private runtime for the studio. The installer may be larger because this keeps the reusable manager independent of the installed studio version.

Tests cover release filtering, semantic ordering, cached downloads, integrity failures, redirects, token isolation, cancellation, private live GitHub downloads, Windows credential storage using a disposable test credential, and installer cleanup. Actual Windows clean-VM coverage remains a release task.
