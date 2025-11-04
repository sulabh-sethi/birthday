#!/usr/bin/env bash
set -euo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
base64 --decode "$dir/people.xlsx.b64" > "$dir/people.xlsx"
echo "Restored sample workbook to $dir/people.xlsx"
