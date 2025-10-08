#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  >&2 echo "Usage: ./api/scripts/ef-migrations.sh \"MigrationName\""
  exit 2
fi

migration_name="$1"
project_path="./api/api.csproj"

echo "Executing: dotnet ef migrations add ${migration_name} --project ${project_path} --startup-project ${project_path} --output-dir Data/Migrations"
dotnet ef migrations add "${migration_name}" --project "${project_path}" --startup-project "${project_path}" --output-dir "Data/Migrations"

echo "Executing: dotnet ef database update --project ${project_path} --startup-project ${project_path}"
dotnet ef database update --project "${project_path}" --startup-project "${project_path}"
