#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd -P)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." &>/dev/null && pwd -P)"

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
Usage: deploy-local.sh [--dry-run|-n] [--verbose|-v] [--game-dir <path>]

Builds CodeMarket.ModAPI + first-party reference mods in Release mode and
deploys them into <game-dir>/BepInEx/plugins/.
EOF
}

log_info() {
  printf '%s[deploy]%s %s\n' "${C_GREEN}" "${C_RESET}" "$1"
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

if [[ ! -f "${TARGET_GAME_DIR}/Code Market_Data/Managed/JavaMarket.dll" ]]; then
  echo "ERROR: Code Market not found at '${TARGET_GAME_DIR}'." >&2
  exit 1
fi

if [[ ! -d "${TARGET_GAME_DIR}/BepInEx/core" ]]; then
  echo "ERROR: BepInEx 5 (x64 Mono) is not installed under '${TARGET_GAME_DIR}/BepInEx/core'." >&2
  exit 1
fi

RESTORE_ARGS=()
if [[ -d "${HOME}/.nuget/packages" ]]; then
  RESTORE_ARGS+=(--source "${HOME}/.nuget/packages")
fi

log_info "Building solution (Release) against '${TARGET_GAME_DIR}'..."
GAME_DIR="${TARGET_GAME_DIR}" dotnet build "${REPO_ROOT}/CodeMarket.ModAPI.sln" \
  -c Release -p:TreatWarningsAsErrors=true "${RESTORE_ARGS[@]}"

PLUGINS_DIR="${TARGET_GAME_DIR}/BepInEx/plugins"
API_DEST="${PLUGINS_DIR}/CodeMarket.ModAPI"
QOL_DEST="${PLUGINS_DIR}/CodeMarket.Mods.QoL"
PACK_DEST="${PLUGINS_DIR}/SampleDataPack"

CORE_DLL="${REPO_ROOT}/src/CodeMarket.ModAPI.Core/bin/Release/netstandard2.1/CodeMarket.ModAPI.Core.dll"
UNITY_DLL="${REPO_ROOT}/src/CodeMarket.ModAPI.Unity/bin/Release/netstandard2.1/CodeMarket.ModAPI.Unity.dll"
QOL_DLL="${REPO_ROOT}/src/Mods/CodeMarket.Mods.QoL/bin/Release/netstandard2.1/CodeMarket.Mods.QoL.dll"
PACK_JSON="${REPO_ROOT}/src/Mods/SampleDataPack/mod.json"

deploy_file() {
  local src="$1"
  local dst_dir="$2"
  if [[ "${DRY_RUN}" -eq 1 ]]; then
    log_info "[dry-run] install -Dm644 '${src}' -> '${dst_dir}/$(basename "${src}")'"
    return
  fi
  mkdir -p "${dst_dir}"
  cp -f "${src}" "${dst_dir}/"
  log_verbose "Copied '${src}' -> '${dst_dir}/'"
}

deploy_file "${CORE_DLL}" "${API_DEST}"
deploy_file "${UNITY_DLL}" "${API_DEST}"
deploy_file "${QOL_DLL}" "${QOL_DEST}"
deploy_file "${PACK_JSON}" "${PACK_DEST}"

log_info "Deployment complete -> ${PLUGINS_DIR}"
