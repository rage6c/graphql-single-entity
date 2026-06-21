#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
run_id="$(date -u +%Y%m%d%H%M%S)"
results_dir="$root_dir/TestResults/Coverage/$run_id"
report_dir="$root_dir/TestResults/CoverageReport/$run_id"

cd "$root_dir"
dotnet tool restore
dotnet test tests/GraphqlDataService.Sample.Tests/GraphqlDataService.Sample.Tests.csproj \
  --collect:"Code Coverage" \
  --results-directory "$results_dir"

coverage_file="$(find "$results_dir" -name '*.coverage' -type f -print -quit)"
if [[ -z "$coverage_file" ]]; then
  echo "No .coverage file was produced." >&2
  exit 1
fi

dotnet tool run dotnet-coverage merge "$coverage_file" \
  -o "$results_dir/coverage.cobertura.xml" \
  -f cobertura \
  --nologo

dotnet tool run reportgenerator \
  "-reports:$results_dir/coverage.cobertura.xml" \
  "-targetdir:$report_dir" \
  "-reporttypes:TextSummary;Cobertura" \
  "-assemblyfilters:+GraphqlDataService.Sample;+GraphqlDataService.Generator;-GraphqlDataService.Sample.Tests" \
  -verbosity:Warning

cat "$report_dir/Summary.txt"

line_coverage="$(awk '/^  Line coverage:/ { gsub("%", "", $3); print $3; exit }' "$report_dir/Summary.txt")"
if ! awk -v coverage="$line_coverage" 'BEGIN { exit !(coverage >= 90) }'; then
  echo "Production line coverage ${line_coverage}% is below the required 90%." >&2
  exit 1
fi

echo "Production line coverage ${line_coverage}% meets the required 90%."
