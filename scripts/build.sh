#!/usr/bin/env bash
# =============================================================================
# Strategos — Build Script (Linux / macOS / WSL)
#
# Usage:
#   ./scripts/build.sh [options]
#
# Options:
#   -t, --target   Platform: windows | linux | macos | webgl | all  (default: linux)
#   -o, --output   Output directory relative to project root         (default: Artifacts)
#   --test         Run Unity EditMode tests before building
#   --dev          Build with development flags (profiler, debugging)
#   --clean        Delete Library/ cache before building
#   -h, --help     Show this help message
#
# Environment variables:
#   UNITY_EXECUTABLE   Full path to the Unity binary (overrides auto-detection)
#   UNITY_HUB_PATH     Root path of Unity Hub installs
#
# Examples:
#   ./scripts/build.sh
#   ./scripts/build.sh -t linux --test
#   ./scripts/build.sh -t all --clean
#   ./scripts/build.sh -t windows --dev -o Builds/dev
# =============================================================================

set -euo pipefail

# ─── Defaults ─────────────────────────────────────────────────────────────────
TARGET="linux"
OUTPUT_DIR="Artifacts"
RUN_TESTS=false
DEVELOPMENT=false
CLEAN=false

# ─── Argument parsing ─────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--target)   TARGET="$2";      shift 2 ;;
        -o|--output)   OUTPUT_DIR="$2";  shift 2 ;;
        --test)        RUN_TESTS=true;   shift ;;
        --dev)         DEVELOPMENT=true; shift ;;
        --clean)       CLEAN=true;       shift ;;
        -h|--help)
            head -35 "$0" | tail -28  # Print the usage comment block
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# ─── Paths ────────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
VERSION_FILE="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
ARTIFACTS_DIR="$PROJECT_ROOT/$OUTPUT_DIR"

# ─── Resolve Unity version ────────────────────────────────────────────────────
if [[ ! -f "$VERSION_FILE" ]]; then
    echo "ERROR: ProjectVersion.txt not found at $VERSION_FILE" >&2
    exit 1
fi

UNITY_VERSION=$(grep "^m_EditorVersion:" "$VERSION_FILE" | awk '{print $2}')

echo "── Strategos Build Script ────────────────────────────────────"
echo "   Project : $PROJECT_ROOT"
echo "   Unity   : $UNITY_VERSION"
echo "   Target  : $TARGET"
echo "   Output  : $ARTIFACTS_DIR"
echo "──────────────────────────────────────────────────────────────"

# ─── Locate Unity executable ──────────────────────────────────────────────────
find_unity() {
    local version="$1"

    # Explicit override (CI containers set this)
    if [[ -n "${UNITY_EXECUTABLE:-}" ]] && [[ -f "$UNITY_EXECUTABLE" ]]; then
        echo "$UNITY_EXECUTABLE"
        return 0
    fi

    # game-ci docker container path (most common in CI)
    local gameci_path="/opt/unity/editors/$version/Editor/Unity"
    if [[ -f "$gameci_path" ]]; then echo "$gameci_path"; return 0; fi

    # Linux Unity Hub path
    local hub_root="${UNITY_HUB_PATH:-$HOME/Unity/Hub/Editor}"
    local linux_path="$hub_root/$version/Editor/Unity"
    if [[ -f "$linux_path" ]]; then echo "$linux_path"; return 0; fi

    # macOS Unity Hub path
    local mac_path="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
    if [[ -f "$mac_path" ]]; then echo "$mac_path"; return 0; fi

    # WSL: Windows path mapped via /mnt/c
    local wsl_path="/mnt/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
    if [[ -f "$wsl_path" ]]; then echo "$wsl_path"; return 0; fi

    return 1
}

UNITY_EXE=$(find_unity "$UNITY_VERSION") || {
    echo "ERROR: Unity $UNITY_VERSION not found." >&2
    echo "Options:" >&2
    echo "  1. Install via Unity Hub" >&2
    echo "  2. Set UNITY_EXECUTABLE=/path/to/Unity" >&2
    exit 1
}

echo "   Unity   : $UNITY_EXE"
echo ""

# ─── Optional clean ───────────────────────────────────────────────────────────
if [[ "$CLEAN" == true ]]; then
    LIB_DIR="$PROJECT_ROOT/Library"
    if [[ -d "$LIB_DIR" ]]; then
        echo "Cleaning Library/ ..."
        rm -rf "$LIB_DIR"
        echo "Done."
    fi
fi

mkdir -p "$ARTIFACTS_DIR"

# ─── Helper: run Unity and check exit code ───────────────────────────────────
run_unity() {
    local log_file="$1"
    shift   # remaining args are Unity arguments

    echo "Running Unity:"
    echo "  $UNITY_EXE $*"
    echo "  Log: $log_file"
    echo ""

    "$UNITY_EXE" "$@" -logFile "$log_file" || {
        echo ""
        echo "ERROR: Unity exited with code $?. Full log: $log_file" >&2
        exit 1
    }
}

# ─── Run tests ────────────────────────────────────────────────────────────────
if [[ "$RUN_TESTS" == true ]]; then
    echo "=== Running EditMode Tests ==="
    TEST_RESULTS="$ARTIFACTS_DIR/test-results.xml"
    TEST_LOG="$ARTIFACTS_DIR/test-log.txt"

    run_unity "$TEST_LOG" \
        -batchmode -quit -nographics \
        -projectPath "$PROJECT_ROOT" \
        -runTests \
        -testPlatform EditMode \
        -testResults "$TEST_RESULTS"

    echo "Tests passed. Results: $TEST_RESULTS"
    echo ""
fi

# ─── Build function ───────────────────────────────────────────────────────────
build_target() {
    local platform_name="$1"
    local build_method="$2"

    echo "=== Building $platform_name ==="

    local build_output="$ARTIFACTS_DIR/$platform_name"
    local build_log="$ARTIFACTS_DIR/build-log-$platform_name.txt"

    local unity_args=(
        -batchmode -quit -nographics
        -projectPath    "$PROJECT_ROOT"
        -executeMethod  "$build_method"
        -customBuildPath "$build_output"
    )

    [[ "$DEVELOPMENT" == true ]] && unity_args+=(-development)

    run_unity "$build_log" "${unity_args[@]}"

    echo "Build complete: $build_output"
    echo ""
}

# ─── Dispatch ─────────────────────────────────────────────────────────────────
case "$TARGET" in
    windows) build_target "Windows" "Strategos.Editor.GameBuild.BuildWindows" ;;
    linux)   build_target "Linux"   "Strategos.Editor.GameBuild.BuildLinux"   ;;
    macos)   build_target "macOS"   "Strategos.Editor.GameBuild.BuildMacOS"   ;;
    webgl)   build_target "WebGL"   "Strategos.Editor.GameBuild.BuildWebGL"   ;;
    all)
        build_target "Windows" "Strategos.Editor.GameBuild.BuildWindows"
        build_target "Linux"   "Strategos.Editor.GameBuild.BuildLinux"
        build_target "macOS"   "Strategos.Editor.GameBuild.BuildMacOS"
        ;;
    *)
        echo "ERROR: Unknown target '$TARGET'. Use: windows | linux | macos | webgl | all" >&2
        exit 1
        ;;
esac

echo "── All done ──────────────────────────────────────────────────"
echo "   Artifacts: $ARTIFACTS_DIR"
echo "──────────────────────────────────────────────────────────────"
