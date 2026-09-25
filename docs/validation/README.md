# Baseline evidence

Original 0.9.3 server reports: numerical, runtime, APK, Android and Windows builds. They are preserved from the release, not rerun by source publication. Physical Quest checks remain outstanding.

The separate rendered-element export completed with exit 0. Its Linux Editor log also reported Meta OVRPlugin initialization and Unity SearchDatabase environment errors; the export does not establish headset behavior.


## 0.9.4 playback update

[Full runtime report](0.9.4/runtime-report.json): 95 passed, zero application errors.
[Playback report](0.9.4/playback-report.json): 33 passed.
[Fast placement report](0.9.4/playback-layout-report.json): all tested bounds, overlap and orientation checks passed.

Actual updated renders: [controls](../renders/0.9.4/playback-controls.png), [single-spin context and phase](../renders/0.9.4/single-spin-and-phase.png), [source link](../renders/0.9.4/source-link.png).

The simulation core is unchanged from the 66 passing fresh-public-clone numerical tests. [APK checks](0.9.4/apk-report.json) passed all 60 assertions, including every accelerated voice clip and Vulkan XR boot entries. [Android](0.9.4/build-android.json) and [Windows](0.9.4/build-windows.json) builds succeeded. [Release APK identity](0.9.4/release-apk-identity.json) records the signed candidate; its application payload matches the validated preview. The Linux Editor environment warnings are recorded separately. Physical Quest and Horizon channel validation remain outstanding.
