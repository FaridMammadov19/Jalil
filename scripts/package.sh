#!/bin/zsh

set -euo pipefail

script_directory="${0:A:h}"
project_directory="${script_directory:h}"
runtime_identifier="${1:-}"

if [[ "$runtime_identifier" != "osx-arm64" && "$runtime_identifier" != "osx-x64" ]]; then
  echo "Usage: ./scripts/package.sh osx-arm64|osx-x64" >&2
  exit 2
fi

if [[ -x "$project_directory/.dotnet/dotnet" ]]; then
  dotnet_command="$project_directory/.dotnet/dotnet"
else
  dotnet_command="dotnet"
fi

artifact_directory="$project_directory/artifacts"
package_directory="$artifact_directory/Jalil-$runtime_identifier"
archive_path="$artifact_directory/Jalil-$runtime_identifier.zip"

rm -rf "$package_directory"
rm -f "$archive_path"
mkdir -p "$package_directory"

"$dotnet_command" publish "$project_directory/src/Jalil.App/Jalil.App.csproj" \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --self-contained true \
  --output "$package_directory" \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None

cp "$project_directory/distribution/Start Jalil.command" "$package_directory/Start Jalil.command"
cp "$project_directory/distribution/QUICK_START.txt" "$package_directory/QUICK_START.txt"
chmod +x "$package_directory/Jalil" "$package_directory/Start Jalil.command"

ditto -c -k --keepParent "$package_directory" "$archive_path"
echo "Created $archive_path"
