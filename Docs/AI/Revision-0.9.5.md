# 0.9.5 — Android install location

Version code **18**. This packaging update sets `android:installLocation="auto"` in the final APK (binary value `0`), as required by [Meta's release-manifest specification](https://developers.meta.com/horizon/resources/publish-mobile-manifest/).

The previous 0.9.4 APK encoded value 2 (preferExternal). Configure now sets Unity's AndroidPreferredInstallLocation.Auto, and the Gradle manifest postprocessor explicitly writes auto. The final-artifact verifier rejects both a missing attribute and any other value.

The rebuilt APK passes all 61 artifact checks. The release-signed copy was separately inspected for install location, version/name, payload equivalence and persistent signing identity.

This update leaves runtime code and lesson content unchanged. Runtime/playback evidence and Store media remain the actual 0.9.4 results. The current Windows download remains 0.9.4. Store App ID, account steps and physical Quest validation remain outstanding.
