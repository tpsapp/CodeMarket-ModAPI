#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd -P)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." &>/dev/null && pwd -P)"

VERSION="1.0.1"
DRY_RUN=0
VERBOSE=0
TARGET_GAME_DIR="${GAME_DIR:-${HOME}/.steam/steam/steamapps/common/Code Market}"

if [[ -z "${NO_COLOR:-}" && -t 1 ]]; then
  C_GREEN=$'\033[0;32m'
  C_YELLOW=$'\033[0;33m'
  C_RESET=$'\033[0m'
else
  C_GREEN=""
  C_YELLOW=""
  C_RESET=""
fi

usage() {
  cat <<'EOF'
Usage: package-release.sh [--dry-run|-n] [--verbose|-v] [--game-dir <path>]

Builds Release artifacts, runs Core tests, and creates cross-platform
GitHub Release and Thunderstore ZIP bundles in dist/.
EOF
}

log_info() {
  printf '%s[package]%s %s\n' "${C_GREEN}" "${C_RESET}" "$1"
}

log_verbose() {
  if [[ "${VERBOSE}" -eq 1 ]]; then
    printf '%s[verbose]%s %s\n' "${C_YELLOW}" "${C_RESET}" "$1"
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -n|--dry-run) DRY_RUN=1; shift ;;
    -v|--verbose) VERBOSE=1; shift ;;
    --game-dir)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: --game-dir requires a path argument." >&2
        exit 1
      fi
      TARGET_GAME_DIR="$2"
      shift 2
      ;;
    -h|--help) usage; exit 0 ;;
    *)
      echo "ERROR: Unknown option '$1'." >&2
      usage >&2
      exit 1
      ;;
  esac
done

if ! command -v zip >/dev/null 2>&1; then
  echo "ERROR: 'zip' utility is required to build release archives." >&2
  exit 1
fi

RESTORE_ARGS=()
if [[ -d "${HOME}/.nuget/packages" ]]; then
  RESTORE_ARGS+=(--source "${HOME}/.nuget/packages")
fi

log_info "Building Release solution and running Core unit tests..."
GAME_DIR="${TARGET_GAME_DIR}" dotnet build "${REPO_ROOT}/CodeMarket.ModAPI.sln" \
  -c Release -p:TreatWarningsAsErrors=true "${RESTORE_ARGS[@]}"
dotnet run --project "${REPO_ROOT}/tests/CodeMarket.ModAPI.Core.Tests" -c Release --no-build

CORE_DLL="${REPO_ROOT}/src/CodeMarket.ModAPI.Core/bin/Release/netstandard2.1/CodeMarket.ModAPI.Core.dll"
UNITY_DLL="${REPO_ROOT}/src/CodeMarket.ModAPI.Unity/bin/Release/netstandard2.1/CodeMarket.ModAPI.Unity.dll"
QOL_DLL="${REPO_ROOT}/src/Mods/CodeMarket.Mods.QoL/bin/Release/netstandard2.1/CodeMarket.Mods.QoL.dll"
PACK_JSON="${REPO_ROOT}/src/Mods/SampleDataPack/mod.json"
TS_MANIFEST="${REPO_ROOT}/packaging/thunderstore/manifest.json"
README_MD="${REPO_ROOT}/README.md"
LICENSE_FILE="${REPO_ROOT}/LICENSE"

DIST_DIR="${REPO_ROOT}/dist"
GH_ZIP="${DIST_DIR}/CodeMarket.ModAPI-v${VERSION}-github.zip"
TS_ZIP="${DIST_DIR}/CodeMarket.ModAPI-v${VERSION}-thunderstore.zip"

if [[ "${DRY_RUN}" -eq 1 ]]; then
  log_info "[dry-run] Would generate '${GH_ZIP}' and '${TS_ZIP}'."
  exit 0
fi

STAGE_DIR="$(mktemp -d)"
trap 'rm -rf "${STAGE_DIR}"' EXIT

mkdir -p "${DIST_DIR}"
rm -f "${GH_ZIP}" "${TS_ZIP}"

# 1. GitHub Release ZIP (extract directly into Code Market root after installing BepInEx 5)
GH_STAGE="${STAGE_DIR}/github"
mkdir -p "${GH_STAGE}/BepInEx/plugins/CodeMarket.ModAPI" \
         "${GH_STAGE}/BepInEx/plugins/CodeMarket.Mods.QoL" \
         "${GH_STAGE}/BepInEx/plugins/SampleDataPack"
cp "${CORE_DLL}" "${UNITY_DLL}" "${GH_STAGE}/BepInEx/plugins/CodeMarket.ModAPI/"
cp "${QOL_DLL}" "${GH_STAGE}/BepInEx/plugins/CodeMarket.Mods.QoL/"
cp "${PACK_JSON}" "${GH_STAGE}/BepInEx/plugins/SampleDataPack/"
cp "${README_MD}" "${LICENSE_FILE}" "${GH_STAGE}/"

(cd "${GH_STAGE}" && zip -qr "${GH_ZIP}" .)
log_verbose "Created ${GH_ZIP}"

# 2. Thunderstore ZIP (r2modman / Gale compatible layout)
TS_STAGE="${STAGE_DIR}/thunderstore"
mkdir -p "${TS_STAGE}/plugins/CodeMarket.ModAPI" \
         "${TS_STAGE}/plugins/CodeMarket.Mods.QoL" \
         "${TS_STAGE}/plugins/SampleDataPack"
cp "${TS_MANIFEST}" "${README_MD}" "${LICENSE_FILE}" "${TS_STAGE}/"
cp "${CORE_DLL}" "${UNITY_DLL}" "${TS_STAGE}/plugins/CodeMarket.ModAPI/"
cp "${QOL_DLL}" "${TS_STAGE}/plugins/CodeMarket.Mods.QoL/"
cp "${PACK_JSON}" "${TS_STAGE}/plugins/SampleDataPack/"

(cd "${TS_STAGE}" && zip -qr "${TS_ZIP}" .)
log_verbose "Created ${TS_ZIP}"

log_info "Release archives created:"
ls -lh "${GH_ZIP}" "${TS_ZIP}"
