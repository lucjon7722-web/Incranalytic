# Microsoft Store status and preparation history

## Availability update — November 4, 2026

Kitarie, the creator, has confirmed that **Incranalytic is officially available
on the Microsoft Store**. The earlier preparation snapshot below predates
publication and does not describe the current listing state.

[Get Incranalytic from the Microsoft Store](https://apps.microsoft.com/detail/9nkdqhdljf78).
For support, contact **Kitarie** at [Lpj2010@outlook.com](mailto:Lpj2010@outlook.com).

This update records availability; it does not retroactively supply missing test
logs or certification records. Use Partner Center for the live submission state.

## Historical preparation snapshot — November 3, 2026

Updated November 3, 2026. The owner supplied the reserved Incranalytic app's
Partner Center package identity. The assigned publisher display name is Kitarie.
Local identity configuration is excluded from Git; submission builds use the
exact Partner Center fields rather than the preflight placeholder.

Completed locally:

- A self-contained x64 MSIX build for the Store edition.
- Microsoft's MakeAppx manifest/schema packaging validation.
- Exact comparison of all three supplied identity values with the built manifest.
- 22 Store edition UI/integration checks, including Store-specific terms and
  update guidance. These were also run against the payload extracted from MSIX.
- 20 package checks covering the desktop entry point, capability, executable,
  runtime, notices, export permission, examples, logo dimensions, and absence of
  the GitHub updater, Inno setup/uninstaller, and debug symbols.
- The ordinary GitHub edition still builds without warnings and passes its
  existing 21 application checks.
- Listing copy, review notes, privacy-policy draft, and icon assets.
- Runtime licenses/notices are now explicitly copied from the exact framework
  packages used in publishing, for both Store and future Inno setup builds.

Outstanding at the time of this snapshot:

- Confirm developer-account verification and any Partner Center requirements.
- Actual MSIX installed activation, persistence, file access, coexistence,
  update/removal testing in a controlled environment, and WACK.
- Finish authentic listing screenshots (capture was interrupted by the owner).
- Review/publish the privacy policy, confirm support contact and listing fields,
  and complete content/age-rating and availability questionnaires.
- Owner review of the concrete submission, then Microsoft certification.

No Store package has been uploaded or submitted by this work. No Azure resource,
paid signing subscription, or Windows certificate trust change was made. The
MSIX is unsigned for Store submission; that is not an ordinary sideload installer.
Local checks do not establish Store approval or guarantee certification.
