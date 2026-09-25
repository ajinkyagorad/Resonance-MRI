#!/usr/bin/env bash
# Headless runtime validation and review renders (one Unity editor for this project; graphics on, xvfb).
set -uo pipefail
cd /home/triton/projects/ResonanceQuest
unity=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
if pgrep -f "Unity.*-projectPath /home/triton/projects/ResonanceQuest" >/dev/null; then echo "A Unity editor already runs for this project"; exit 2; fi
timeout 5400 xvfb-run -a "$unity" -batchmode -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceValidation.Run -logFile "$PWD/validation/runtime.log"
code=$?
python3 Tools/review_sheets.py >/dev/null 2>&1 || true
echo "VALIDATION_EXIT $code"
exit $code
