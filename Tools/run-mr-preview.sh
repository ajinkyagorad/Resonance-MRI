#!/usr/bin/env bash
# Mixed-reality preview renders (eye buffer composited over grey/room backdrops as the headset does) and the CPU cost
# probe. Usage: Tools/run-mr-preview.sh <output dir>. One Unity editor for this project; graphics on (xvfb).
set -uo pipefail
cd /home/triton/projects/ResonanceQuest
out=${1:-validation/renders/mr}
unity=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
if pgrep -f "Unity.*-projectPath /home/triton/projects/ResonanceQuest" >/dev/null; then echo "A Unity editor already runs for this project"; exit 2; fi
mkdir -p "$out"
RESONANCE_MR_OUT="$out" timeout 3600 xvfb-run -a "$unity" -batchmode -projectPath "$PWD" -buildTarget Android -executeMethod ResonanceValidation.RunMR -logFile "$PWD/validation/mr-preview.log"
code=$?
echo "MR_EXIT $code"
exit $code
