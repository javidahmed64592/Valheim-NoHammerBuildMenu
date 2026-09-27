#!/usr/bin/env bash
# Builds the mod, deploys all mod files to your r2modman profile, and produces
# a Thunderstore-ready zip. Run locally and in CI — deploy is skipped
# automatically when DEPLOY_DIR does not exist (e.g. on the CI runner).
set -euo pipefail
cd "$(dirname "$0")"

NAMESPACE="Aestelwen"
NAME="NoHammerBuildMenu"
VERSION=$(python3 -c "import json; print(json.load(open('manifest.json'))['version_number'])")

# r2modman plugins folder — override with: DEPLOY_DIR=/your/path bash package.sh
DEPLOY_DIR="${DEPLOY_DIR:-/path/to/mod manager/Valheim/profiles/profile/BepInEx/plugins}"

dotnet build -c Release

rm -rf dist
mkdir -p "dist/$NAMESPACE-$NAME"
cp "bin/Release/$NAME.dll" manifest.json README.md icon.png "dist/$NAMESPACE-$NAME/"

if [[ -d "$DEPLOY_DIR" ]]; then
    mkdir -p "$DEPLOY_DIR/$NAMESPACE-$NAME"
    cp -r "dist/$NAMESPACE-$NAME/." "$DEPLOY_DIR/$NAMESPACE-$NAME/"
    echo "Deployed → $DEPLOY_DIR/$NAMESPACE-$NAME/"
fi

(cd "dist/$NAMESPACE-$NAME" && zip -r "../$NAMESPACE-$NAME-$VERSION.zip" .)
echo "Ready: dist/$NAMESPACE-$NAME-$VERSION.zip"

# Export version for the GitHub Actions artifact upload step
if [[ -n "${GITHUB_ENV:-}" ]]; then
    echo "VERSION=$VERSION" >> "$GITHUB_ENV"
fi
