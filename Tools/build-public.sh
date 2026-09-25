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
runner=()
if [[ "$(uname -s)" == Linux && -z "${DISPLAY:-}" ]]; then
  runner=(xvfb-run -a)
fi
case "$mode" in
  configure) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -quit -executeMethod ResonanceBuild.Configure -logFile validation/configure.log ;;
  android) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -quit -buildTarget Android -executeMethod ResonanceBuild.BuildAndroid -logFile validation/build-android.log ;;
  store) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -quit -buildTarget Android -executeMethod ResonanceBuild.BuildStore -logFile validation/build-store.log ;;
  windows) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -quit -buildTarget StandaloneWindows64 -executeMethod ResonanceBuild.BuildWindows -logFile validation/build-windows.log ;;
  validate) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -executeMethod ResonanceValidation.Run -logFile validation/runtime.log ;;
  render) "${runner[@]}" "$UNITY_EDITOR" "${common[@]}" -executeMethod ResonanceElementReference.Run -logFile validation/element-reference.log ;;
  *) echo "Usage: $0 test|configure|android|store|windows|validate|render" >&2; exit 2 ;;
esac

