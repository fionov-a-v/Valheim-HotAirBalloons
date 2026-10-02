#!/bin/sh
# Собирает мод и кладёт архив для Thunderstore/r2modman (или чтобы передать друзьям) в dist/.
set -e
cd "$(dirname "$0")"
[ -x "$HOME/.dotnet/dotnet" ] && export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
dotnet build -c Release
rm -rf dist/pkg && mkdir -p dist/pkg/plugins
cp bin/Release/HotAirBalloons.dll dist/pkg/plugins/
cp package/manifest.json package/icon.png README.md LICENSE.md dist/pkg/
(cd dist/pkg && rm -f ../HotAirBalloons-1.3.1.zip && python3 -m zipfile -c ../HotAirBalloons-1.3.1.zip manifest.json icon.png README.md LICENSE.md plugins)
rm -rf dist/pkg
echo "dist/HotAirBalloons-1.3.1.zip"
