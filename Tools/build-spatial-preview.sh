#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
UNITY=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
mkdir -p validation
if [ "${1:-}" != "--native-only" ]; then
DOTNET_ROLL_FORWARD=Major /home/triton/.dotnet/dotnet run --project Tools/simcore/simcore.csproj -c Release -- test > validation/simulation-spatial.log 2>&1
python3 Tools/generate-cued-narration.py am_michael --check
python3 Tools/generate-cued-narration.py bm_george --check
fi
timeout 3600 xvfb-run -a "$UNITY" -batchmode -job-worker-count 2 -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceValidation.Run -logFile "$PWD/validation/runtime.log"
timeout 5400 "$UNITY" -batchmode -nographics -quit -job-worker-count 2 -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceBuild.BuildAndroid -logFile "$PWD/validation/build-android.log"
python3 Tools/verify_apk.py
timeout 3600 "$UNITY" -batchmode -nographics -quit -job-worker-count 2 -projectPath "$PWD" -buildTarget Win64 -executeMethod ResonanceBuild.BuildWindows -logFile "$PWD/validation/build-windows.log"
