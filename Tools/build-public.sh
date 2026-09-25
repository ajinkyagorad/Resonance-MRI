#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mode="${1:-test}"
mkdir -p validation
if [[ "$mode" == test ]]; then
  "${DOTNET:-dotnet}" run --project Tools/simcore/simcore.csproj -c Release -- test
  exit
fi
: "${UNITY_EDITOR:?Set UNITY_EDITOR to your Unity 6000.5.5f1 executable}"
common=(-batchmode -job-worker-count 2 -projectPath "$PWD")
case "$mode" in
  configure) "$UNITY_EDITOR" "${common[@]}" -nographics -quit -executeMethod ResonanceBuild.Configure -logFile validation/configure.log ;;
  android) "$UNITY_EDITOR" "${common[@]}" -nographics -quit -buildTarget Android -executeMethod ResonanceBuild.BuildAndroid -logFile validation/build-android.log ;;
  windows) "$UNITY_EDITOR" "${common[@]}" -nographics -quit -buildTarget Win64 -executeMethod ResonanceBuild.BuildWindows -logFile validation/build-windows.log ;;
  validate) xvfb-run -a "$UNITY_EDITOR" "${common[@]}" -executeMethod ResonanceValidation.Run -logFile validation/runtime.log ;;
  render) xvfb-run -a "$UNITY_EDITOR" "${common[@]}" -executeMethod ResonanceElementReference.Run -logFile validation/element-reference.log ;;
  *) echo "Usage: $0 test|configure|android|windows|validate|render" >&2; exit 2 ;;
esac

