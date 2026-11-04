# Individual publisher verification and signing

The project has selected the free Microsoft Store route. Use
[the Store submission guide](../store/submission-guide.md) for the active plan.
This document describes the optional paid Azure route and does not authorize
creating paid resources.

Prepared for Incranalytic, 2026-11-03. This is setup guidance, not a statement
that a publisher has been verified or any existing release has been signed.

Kitarie remains the public creator identity. Microsoft's individual verification
uses the person's legal identity, which is visible in certificate details.
No business registration is required for the individual route.

## 1. Check the Azure account before buying anything

Open [Azure portal](https://portal.azure.com/), sign in yourself, and search for
Subscriptions. Check whether any subscription exists and whether it is enabled.
If its offer type is unclear, check its Overview or Properties for the offer
or plan. A Microsoft account alone does not establish a paid Azure subscription.

Artifact Signing currently requires a paid Azure subscription. Free, trial,
and sponsored subscriptions are not supported. Review Microsoft's current offer
and billing terms before upgrading or creating a paid subscription.

Only report whether the account has no subscription, a free/trial subscription,
or an enabled paid subscription. Do not put passwords, payment information,
identity documents, verification links, or account identifiers in the repository.

## 2. Review cost and certificate identity

Use the official pricing page and the Azure portal's price preview. Creating
an Artifact Signing account starts billing; do not create one merely to explore
the screens. This guide does not promise a fixed price.

For individual validation, the associated Azure billing account must be of type
Individual. Its legal name and address must match the identity/address evidence
Microsoft requires. Use the certificate subject preview to inspect the public
details before submitting. The documented individual flow exposes name and
city/state/country in the certificate, and excludes email/street address by
default. Do not select optional address disclosure without reviewing it.

## 3. Set up the service after approving the cost

Follow the official quickstart to register Microsoft.CodeSigning and create an
Artifact Signing account. Choose the Basic tier if its displayed price, quota,
and features meet your needs. An Azure resource name can reference Kitarie;
that does not replace the verified legal certificate name.

Assign your account the Artifact Signing Identity Verifier role using the
official access-control instructions. In Identity validations, select
Individual and Public. Check the billing identity and certificate preview.

Complete sign-in, email/phone checks, government-ID verification, and any
Authenticator/Verified ID steps yourself on the linked Microsoft/provider
pages. Submit documents only through that official process. Wait until the
identity-validation status is Completed.

## 4. Create the production certificate profile

Create a Public Trust certificate profile linked to the completed individual
validation. Public Trust Test and Private Trust do not substitute for production
public signing. Assign the signing identity the Artifact Signing Certificate
Profile Signer role. The identity-verification role alone does not grant signing.

Record the signing account's endpoint/region, account name, and profile name
locally. They identify resources; no private signing key needs to be emailed,
uploaded to GitHub, or pasted into chat. Authentication is configured separately.

## 5. Prepare the signing machine and Incranalytic build

After validation, install Microsoft's official Artifact Signing Client Tools
and the authentication tooling selected for the signing machine. Check the
download's Microsoft Authenticode signature before installation. Microsoft's
tools have their own prerequisites, including signing tools/runtime components.
These are developer build tools, not new studio-user dependencies.

The production release sequence must be:

1. Build studio and Version Manager for the intended release commit.
2. Sign and timestamp Incranalytic executables and owned DLLs before packaging.
   Preserve Microsoft's existing signatures on third-party runtime components.
3. Configure Inno Setup's signing support for the generated uninstaller and
   final setup. Signing only the outer setup does not sign its embedded payload
   or its generated uninstaller.
4. Verify signatures, timestamps, certificate identity, and installed payloads.
5. Test installation, studio behavior, Version Manager, and uninstallation.
6. Generate checksums after signing, then publish a new version with verified
   uploads. Keep the published unsigned 0.5.0 assets intact as historical files.

This guide does not yet enable signing in Build-Installer.ps1. Integration and
an actual signing test require the approved account/profile and local tools.
A successful identity check does not itself modify or sign existing downloads.
Windows download reputation is separate from certificate identity verification.

## Official references

- [Artifact Signing quickstart and individual verification](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- [Supported subscriptions and certificate naming](https://learn.microsoft.com/en-us/azure/artifact-signing/faq)
- [Pricing and when billing starts](https://azure.microsoft.com/en-us/pricing/details/artifact-signing/)
- [Signing integrations and tool requirements](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-signing-integrations)
- [Inno Setup SignTool setting](https://jrsoftware.org/ishelp/topic_setup_signtool.htm)
- [Inno Setup signed uninstaller setting](https://jrsoftware.org/ishelp/topic_setup_signeduninstaller.htm)
