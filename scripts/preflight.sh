#!/usr/bin/env bash
# Access: Level 2
# Last Updated: 2026-11-25
# Runs every check a pull request must pass: restore, formatting, tests, publish. Stops at the first failure.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f backend/minioasis.slnx ]; then
  if [ -n "$(find . -name '*.csproj' -not -path './.git/*' -print -quit)" ]; then
    echo "Projects exist, but backend/minioasis.slnx is missing." >&2
    exit 1
  fi
  echo "No .NET code yet: nothing to check."
  exit 0
fi

cd backend
dotnet restore minioasis.slnx
dotnet format minioasis.slnx --verify-no-changes --no-restore
dotnet test minioasis.slnx --configuration Release --no-restore --nologo
dotnet publish Minioasis.Api/Minioasis.Api.csproj --configuration Release --no-restore
