# Privacy Policy — Resonance MRI

Effective 25 September 2026.

Resonance MRI is an educational simulation maintained through the [public project](https://github.com/ajinkyagorad/Resonance-MRI). It uses a bundled anatomical model and simulated MRI signals.

## Information used by the application

Head, controller and optional hand-tracking poses are used locally to render and interact with exhibits. Passthrough is supplied by the Meta runtime. The first-party application does not read or save passthrough camera images, record microphone audio, scan a user's body or upload an anatomical scan.

The application writes a local diagnostic CSV containing timestamps, app version, graphics-device description and aggregate frame timings. It remains in the app's local data directory; first-party code does not transmit this file. Removing the app's stored data removes local diagnostics.

The current inspection build has no first-party advertising, analytics upload, account registration or network-based lesson service. Its code opens GitHub only when the user selects the source link. Visiting GitHub is governed by GitHub's own privacy policy.

## Meta services and Store builds

The planned Horizon Store build uses the Meta Platform SDK to initialize platform services and check whether the signed-in account is entitled to the app. Meta processes platform/account information under its policies. First-party app code receives initialization and entitlement results and does not request or save the account's profile or user ID. Platform/OS services may process their own diagnostics; the statements above concern the application's own code.

The public preview and future Store-configured APK must be audited separately when Store integration changes. This policy will be updated if data handling changes.

## Questions

Use the project's [support issues](https://github.com/ajinkyagorad/Resonance-MRI/issues) to contact the maintainers. Issues are public: keep personal or medical information out of them. For platform-account privacy requests, use Meta's account privacy controls.

