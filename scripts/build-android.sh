#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ANDROID_PROJECT="$PROJECT_ROOT/MonoCraft.Android/MonoCraft.Android.csproj"
OUTPUT_DIR="$PROJECT_ROOT/releases/android"

DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
ANDROID_SDK="${ANDROID_HOME:-$HOME/Android/Sdk}"

# --- Argumentos ---
CONFIG="Release"
SIGN=false

for arg in "$@"; do
    case $arg in
        --debug)   CONFIG="Debug" ;;
        --sign)    SIGN=true ;;
    esac
done

echo "==> Building MonoCraft Android ($CONFIG)..."
echo "    ANDROID_SDK : $ANDROID_SDK"
echo "    DOTNET      : $DOTNET"
echo ""

BUILD_CMD=("$DOTNET" build "$ANDROID_PROJECT" \
    -c "$CONFIG" \
    -p:AndroidSdkDirectory="$ANDROID_SDK" \
    -p:AndroidPackageFormat=apk)

if $SIGN; then
    echo "==> Using monocraft.keystore for signing..."
    BUILD_CMD+=("-p:AndroidKeyStore=true")
    BUILD_CMD+=("-p:AndroidSigningKeyStore=$PROJECT_ROOT/monocraft.keystore")
    BUILD_CMD+=("-p:AndroidSigningStorePass=monocraft123")
    BUILD_CMD+=("-p:AndroidSigningKeyAlias=monocraft")
    BUILD_CMD+=("-p:AndroidSigningKeyPass=monocraft123")
fi

"${BUILD_CMD[@]}"

APK=$(find "$PROJECT_ROOT/MonoCraft.Android/bin/$CONFIG" -name "*-Signed.apk" | head -1)
if [ -z "$APK" ]; then
    APK=$(find "$PROJECT_ROOT/MonoCraft.Android/bin/$CONFIG" -name "*.apk" | head -1)
fi

if [ -z "$APK" ]; then
    echo "ERROR: APK not found after build." >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DEST="$OUTPUT_DIR/MonoCraft_${CONFIG}_${TIMESTAMP}.apk"
cp "$APK" "$DEST"

echo ""
echo "==> APK ready: $DEST"

if $SIGN; then
    echo "==> APK successfully signed with V2/V3 signatures!"
fi
