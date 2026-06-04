#! /bin/bash

# Searches for string concatenation which are likely
# to be internationalization error because compared to string with format
# parameter, order of words and numbers is not parameterized and can be wrong
# for Asian langages etc.
# The output of the script is filtered so it gives few enough results, to
# manullay review all of them. But may be not covering some cases.
# Work pass on report by that script was already done.

find ./Source/1.6 -name "*.cs" | xargs grep -nHP '\" \+|\"\+|\+\"|\+ \"' | grep -Ev "SoS.|TranslatorFormattedStringExtensions|.Colorize\(|Log.|Things/|UI/|ThingDef.Named|HarmonyPatch\(|BaseGen.symbolStack.Push" > ConcatStrinReport.txt

