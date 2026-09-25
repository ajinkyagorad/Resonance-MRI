#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${RESONANCE_META_APP_ID:?Set the numeric App ID from your new Meta Horizon app}"
unity="${UNITY_EDITOR:-/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity}"
mkdir -p validation
xvfb-run -a "$unity" -batchmode -quit -job-worker-count 2 -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceBuild.BuildStore -logFile validation/build-horizon.log
python3 Tools/sign-release.py --store
