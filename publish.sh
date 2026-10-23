#!/usr/bin/env bash
set -euo pipefail

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ~/Programs/sekretujo

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o ~/Programs/win-x64/sekretujo
