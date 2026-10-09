#!/usr/bin/env bash
set -e

echo "Formatando a solução..."
dotnet format MonoCraft.sln --no-restore

echo "Formatando o projeto..."
dotnet format MonoCraft.csproj --no-restore
