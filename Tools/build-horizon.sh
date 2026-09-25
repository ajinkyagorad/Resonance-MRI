#!/usr/bin/env bash
set -euo pipefail
cd /home/triton/projects/ResonanceQuest
: "${RESONANCE_META_APP_ID:?Set the numeric App ID from your new Meta Horizon app}"
xvfb-run -a /home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity -batchmode -quit -projectPath /home/triton/projects/ResonanceQuest -buildTarget Android -executeMethod ResonanceBuild.BuildStore -logFile validation/build-horizon.log
python3 Tools/sign-release.py --store
