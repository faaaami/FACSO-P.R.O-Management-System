#!/usr/bin/env sh
# Makes Tesseract OCR work on Linux (servers, Docker, CI).
#
# The TesseractOCR package loads its native libraries from "<app>/x64/" under
# Windows-style names. The Linux binaries shipped in Tesseract.Native need a
# newer OpenSSL (3.2+) than Debian 12 / Ubuntu 24.04 provide, so they cannot be
# loaded next to .NET. Instead we use the distribution's Tesseract and
# Leptonica and expose them under the names the package looks for.
#
# Requires: libtesseract5 and liblept5 (or libleptonica6) installed via apt.
# Usage: scripts/link-linux-ocr.sh <app-output-directory>
set -eu

APP_DIR="${1:?usage: link-linux-ocr.sh <app-output-directory>}"
LIB_DIR="/usr/lib/$(uname -m)-linux-gnu"

TESS="$(ls "$LIB_DIR"/libtesseract.so.5* 2>/dev/null | head -n 1 || true)"
LEPT="$(ls "$LIB_DIR"/liblept.so.5* "$LIB_DIR"/libleptonica.so.6* 2>/dev/null | head -n 1 || true)"

if [ -z "$TESS" ] || [ -z "$LEPT" ]; then
  echo "Tesseract/Leptonica not found in $LIB_DIR. Install libtesseract5 and liblept5." >&2
  exit 1
fi

mkdir -p "$APP_DIR/x64"
ln -sf "$LEPT" "$APP_DIR/x64/libleptonica-1.85.0.dll.so"
ln -sf "$TESS" "$APP_DIR/x64/libtesseract55.dll.so"

# The package's loader P/Invokes "libdl", which glibc 2.34+ only ships as libdl.so.2.
if [ ! -e "$LIB_DIR/libdl.so" ] && [ -e "$LIB_DIR/libdl.so.2" ]; then
  ln -sf "$LIB_DIR/libdl.so.2" "$LIB_DIR/libdl.so" 2>/dev/null || \
    echo "warning: could not create $LIB_DIR/libdl.so (run as root once)" >&2
fi

echo "OCR libraries linked into $APP_DIR/x64"
