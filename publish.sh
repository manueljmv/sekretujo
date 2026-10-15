#!/usr/bin/env bash
set -euo pipefail

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist/linux-x64

dotnet publish src/Sekretujo.App/Sekretujo.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist/win-x64
