#!/usr/bin/env bash
# Full server pipeline for a release candidate: simulation-core tests, narration lint, Unity validation (renders and
# gates), Android and Windows builds, APK verification. One Unity editor for this project at a time; graphics on (xvfb).
set -euo pipefail
cd /home/triton/projects/ResonanceQuest
unity=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
if pgrep -f "Unity.*-projectPath /home/triton/projects/ResonanceQuest" >/dev/null; then echo "A Unity editor already runs for this project"; exit 2; fi
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_ROLL_FORWARD=Major
(cd Tools/simcore && dotnet run -c Release -- test)
for voice in am_michael bm_george; do python3 Tools/generate-cued-narration.py "$voice" --check; done
Tools/run-validation.sh
xvfb-run -a "$unity" -batchmode -quit -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceBuild.BuildAndroid -logFile "$PWD/validation/build-android.log"
xvfb-run -a "$unity" -batchmode -quit -projectPath "$PWD" -buildTarget Win64 -executeMethod ResonanceBuild.BuildWindows -logFile "$PWD/validation/build-windows.log"
python3 Tools/verify_apk.py
