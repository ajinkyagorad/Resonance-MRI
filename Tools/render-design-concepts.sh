#!/usr/bin/env bash
# Design 0.9 concept frames (scratch branch design/0.9-concepts): builds the static mock-up scene and renders the four
# frames over the bright room photograph into validation/design-0.9 (one Unity editor for this project; xvfb, graphics on).
# CONCEPT_ONLY=ab renders a subset.
set -uo pipefail
cd /home/triton/projects/ResonanceQuest
unity=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
if pgrep -f "Unity.*-projectPath /home/triton/projects/ResonanceQuest" >/dev/null; then echo "A Unity editor already runs for this project"; exit 2; fi
timeout 3600 xvfb-run -a -s "-screen 0 1280x1024x24" "$unity" -batchmode -projectPath "$PWD" -buildTarget Android -executeMethod DesignConcepts.Run -logFile "$PWD/validation/design-0.9.log"
code=$?
echo "DESIGN_EXIT $code"
exit $code
