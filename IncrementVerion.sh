#!/bin/bash

file="./Source/1.7/ShipInteriorMod2.cs"

# Extract current version string
current=$(grep -oP 'public const string SOS2version = "\KGithubV[0-9]+\.[0-9]+\.[0-9]+' "$file")
if [ -z "$current" ]; then
    echo "Version line not found."
    exit 1
fi

# Split into parts
read -r title major minor patch <<< "$(echo "$current" | sed 's/GithubV\([0-9]\+\)\.\([0-9]\+\)\.\([0-9]\+\)/GithubV \1 \2 \3/')"

# Increment patch
patch=$((patch + 1))
new_version="${title}${major}.${minor}.${patch}"

# Update file
sed -i "s/public const string SOS2version = \".*\";/public const string SOS2version = \"${new_version}\";\r/" "$file"

unix2dos "$file"

echo "Version SOS2version to $new_version"
