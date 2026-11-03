#!/bin/sh
# Extract the official CapooPals assets from the `assets` branch into the working tree.
# The asset paths are in .gitignore on main, so `git status` stays clean afterwards.
# Usage: sh tools/fetch_assets.sh [remote]   (default remote: origin)
set -e
cd "$(git rev-parse --show-toplevel)"
remote="${1:-origin}"
git fetch "$remote" assets
# autocrlf off: extract the files byte for byte as stored (LF), like a normal checkout here
git -c core.autocrlf=false archive FETCH_HEAD assets | tar -x
echo "Assets extracted from $remote/assets. Open the project once in the Godot editor to import them."
