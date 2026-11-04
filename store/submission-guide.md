# Microsoft Store submission — individual developer

The Store route replaces Azure signing setup for this distribution. No paid
Azure resource or purchased public certificate is needed for MSIX Store
submission. Microsoft verifies the developer account, reviews the app, and
signs accepted Store packages. Existing GitHub EXE releases remain separate.

## Account and app identity — completed by the owner

1. Start at https://storedeveloper.microsoft.com/ and choose Individual developer
   through the current free registration flow. Use your Microsoft account.
2. Complete Microsoft's identity verification yourself. Do not send identity
   documents, passwords, or verification codes through this project or chat.
3. Reserve Incranalytic if available. The owner has now supplied the reserved
   app's identity and assigned publisher display name, Kitarie. Account-verification
   status has not been independently checked by this preparation.
4. Open Product management → Product identity for the reserved app. Copy the
   exact Package/Identity/Name, Package/Identity/Publisher, and
   Package/Properties/PublisherDisplayName into a local copy of
   identity.example.json named identity.local.json. This file is ignored by Git.
   Do not invent these values or assume the publisher display name is Kitarie;
   use the values Microsoft approves and assigns.

## Build tools

Use Microsoft's Windows SDK MakeAppx tool. The preparation work downloaded
Microsoft.Windows.SDK.BuildTools 10.0.28000.2957 from the official NuGet feed and
validated its MakeAppx Microsoft Authenticode signatures before execution.
The tool is kept outside the source repository. No tools were added to the
end-user dependencies beyond the privately bundled .NET runtime.

```powershell
# Local format/integration checks without a Store account:
./scripts/Build-Store.ps1 -MakeAppx 'path/to/x64/makeappx.exe' -Preflight

# Final identity-associated package, after reservation and verification:
./scripts/Build-Store.ps1 -MakeAppx 'path/to/x64/makeappx.exe' -IdentityFile './store/identity.local.json'

# Inspect the package and execute its extracted payload:
./scripts/Test-StorePackage.ps1 -MakeAppx 'path/to/x64/makeappx.exe' -Package 'path/to/package.msix'
```

Preflight packages have a placeholder identity and are explicitly named
PREFLIGHT-NOT-FOR-SUBMISSION. They must not be uploaded as the real product.
Submission builds reject the supplied example/placeholder identity. The package
is unsigned, as allowed for MSIX Store submissions. This does not allow ordinary
local double-click installation; the accepted Store release will be signed by
Microsoft. No self-signed certificate has been installed in a Windows trust store.

## Store edition behavior

- Self-contained x64 WinForms studio with minimum OS manifest 10.0.19041.0.
- No GitHub Version Manager, token storage, Inno installer or uninstaller.
- Help explains Store-managed updates. Install/uninstall is handled by Windows.
- Store-specific terms and privacy policy accompany the app.
- Projects, formulas, code, and local recovery remain user-authored and local.
- The only manifest capability is runFullTrust, needed by the desktop WinForms
  app and normal user-selected file import/save/export dialogs. It does not
  request administrator elevation, a service, or arbitrary downloaded code.
- Reserved studio source terms and personal/commercial export permission remain.

## Listing material and owner decisions

listing-draft.md contains copy, review notes, and suggested metadata. Draft icon
assets are generated from original grid/node geometry. This is a draft visual,
not a trademark-availability determination. Confirm the public developer display
name and support contact in Partner Center.

privacy.md is the Store privacy-policy draft and ships inside the package. The
submission also needs a publicly reachable policy URL owned/controlled by the
publisher; publishing a policy web page is a separate remaining step. A private
support email has not been selected. Do not use identity or billing details as
public support contacts without the owner's choice.

Use genuine current-app screenshots. Desktop screenshots must meet Microsoft's
current size/format requirements. Do not use the preflight identity, unpublished
features, fake results, or a certification claim in promotional screenshots.
Answer the age-rating and content questionnaires according to the actual app.

## Required checks before submitting

1. Replace placeholder identity and build the final package with no compiler warnings.
2. Validate/unpack with MakeAppx and rerun the extracted studio checks.
3. Test actual packaged install, activation, app-data behavior, persistence,
   external project save/import/export, uninstall, and coexistence with the
   GitHub edition in a controlled test environment.
4. Run Windows App Certification Kit (WACK). This preparation has not installed
   WACK or changed developer mode/certificate trust settings. Package schema and
   extracted-payload checks are not WACK or installed-package tests.
5. Check logos/screenshots, host the privacy policy, and complete listing fields,
   availability, age/content ratings, pricing (free), support and restricted
   capability explanation. Have the owner review the concrete submission.
6. Submit through Partner Center. Only Microsoft's review can establish Store
   acceptance; local checks do not guarantee approval. Do not publish automatically
   before the owner has reviewed the actual listing and final package.

## Sources checked November 3, 2026

- https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements
- https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-certification-process
