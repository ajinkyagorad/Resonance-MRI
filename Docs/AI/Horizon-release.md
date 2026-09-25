# Horizon release preparation — new app

The new app has no numeric Meta App ID yet. A release-signed preview and a reproducible Store build procedure are supplied. The preview is not the final Store binary: it deliberately permits sideloading and does not enforce Horizon ownership. BuildStore refuses a missing or placeholder App ID and adds the RESONANCE_STORE entitlement gate. There is no claim that a draft has been uploaded or approved.

## Prepared

- Native ARM64 IL2CPP, min SDK 32 / target SDK 34, Vulkan single-pass stereo, contextual MR/VR boundary policy.
- Store-only initialization/entitlement gate using Meta Platform SDK 205.0.0. It starts at launch, blocks the experience until authorization, and fails closed on failure or timeout. No account credentials are compiled into the app.
- Dedicated persistent release certificate, held in /home/triton/.config/resonance-quest with owner-only permissions. The signing tool supplies its password via a private file; nothing secret is published or committed.
- Same debug signing identity retained for the private Quest update APK. The release preview has a different certificate and cannot replace that debug installation in place. The app has no saved progress to migrate. After an eventual Store release, keep the release key for updates and increment versionCode.
- Asset/attribution record, unencumbered CC-BY anatomy archive, scanner model and receiver references, store text and privacy draft.

## Finish when the new app exists

1. Create the immersive Quest app in your Meta developer organisation and obtain its numeric App ID; keep Android package com.nebulytic.resonance.
2. On the server set RESONANCE_META_APP_ID to that real ID, then run bash Tools/build-horizon.sh. The generated App ID resource is ignored by Git. The final signed output is Builds/Resonance-MRI-Horizon-0.8.0.apk (versionCode 9).
3. Verify the entitlement success, denial and offline paths using actual release-channel accounts on Quest. Run the published VRC checklist, including sustained frame rate, text comfort, input/focus recovery, passthrough without a pre-drawn boundary and restoration before opaque VR.
4. Supply the publisher support contact, public privacy-policy URL, age/content declarations, pricing and production Store media. Select supported Quest 3 / 3S devices. Upload to a release channel first, then submit for review through the dashboard or Meta upload CLI with your own account credentials.
5. Back up the release keystore through the publisher's secure backup process. Never put it in the download directory, project zip or source repository.

Store submission remains pending the App ID, live entitlement/device evidence, publisher fields and Meta review. Do not use the preview APK as the final submission.

Official references: https://developers.meta.com/horizon/resources/publish-submit/ ; https://developers.meta.com/horizon/resources/publish-overview-appID/ ; https://developers.meta.com/horizon/resources/publish-mobile-manifest/ ; https://developers.meta.com/horizon/resources/vrc-quest-packaging-2/
