#!/usr/bin/env bash
set -euo pipefail

# Kept outside the checkout so setup never changes application files or lockfiles.
export DOTNET_ROOT=/workspace/.tools/dotnet
export DOTNET_CLI_HOME=/workspace/.cache/dotnet-cli
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export PATH="$DOTNET_ROOT:$PATH"

sdk_version=10.0.401
sdk_sha512=51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b

if [[ ! -x "$DOTNET_ROOT/dotnet" ]] || ! "$DOTNET_ROOT/dotnet" --list-sdks | rg --fixed-strings --quiet "$sdk_version ["; then
    archive=$(mktemp /tmp/faceit-dotnet-sdk.XXXXXX.tar.gz)
    trap 'rm -f "$archive"' EXIT
    curl --fail --silent --show-error --location --retry 2 \
        "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" \
        --output "$archive"
    printf '%s  %s\n' "$sdk_sha512" "$archive" | sha512sum --check --status
    mkdir -p "$DOTNET_ROOT"
    tar -xzf "$archive" -C "$DOTNET_ROOT"
fi

checkout_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$checkout_dir"
dotnet restore FaceitSwitch.slnx --locked-mode
dotnet build FaceitSwitch.slnx --configuration Release --no-restore
dotnet test tests/FaceitSwitch.Tests/FaceitSwitch.Tests.csproj --configuration Release --no-build --no-restore \
    --logger 'trx;LogFileName=core-tests.trx' --results-directory artifacts/test-results
dotnet publish src/FaceitSwitch/FaceitSwitch.csproj --configuration Release --no-restore \
    --output artifacts/win-x64
