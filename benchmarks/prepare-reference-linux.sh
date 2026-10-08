#!/usr/bin/env bash
# Explicit, untimed installation for the comparative reference VM. No profile
# changes, PATH-selected Python, third-party Python packages or benchmark runs.
set -euo pipefail

bench_root="${HOME}/lython-bench-toolchains"
install_dependencies=false
while (($#)); do
    case "$1" in
        --root) bench_root="${2:?--root needs an absolute directory}"; shift 2 ;;
        --install-build-dependencies) install_dependencies=true; shift ;;
        *) printf 'Unknown option: %s\n' "$1" >&2; exit 2 ;;
    esac
done
[[ "$bench_root" == /* && "$bench_root" != / ]] || {
    printf 'Choose an absolute toolchain directory other than /.\n' >&2; exit 2;
}
[[ "$(uname -s)" == Linux && "$(uname -m)" == x86_64 ]] || {
    printf 'This pinned profile requires Linux x86_64.\n' >&2; exit 2;
}

python_version=3.13.16
python_sha256=f4b1bfb3c79b5bb11b8d228a12504163b4c0dab4d679828d8f5f26b6cb6ab35d
sdk_version=10.0.401
runtime_version=10.0.12
sdk_sha512=51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b
python_prefix="$bench_root/cpython/$python_version"
sdk_prefix="$bench_root/dotnet/$sdk_version"
mkdir -p "$bench_root/downloads" "$bench_root/builds" "$bench_root/logs"
status_file="$bench_root/preparation-status.json"
started_utc="$(date -u +%FT%TZ)"
on_exit() {
    local result=$?
    local status=failed
    ((result == 0)) && status=complete
    printf '{"status":"%s","exitCode":%s,"startedUtc":"%s","completedUtc":"%s"}\n' \
        "$status" "$result" "$started_utc" "$(date -u +%FT%TZ)" > "$status_file.partial"
    mv "$status_file.partial" "$status_file"
}
trap on_exit EXIT
printf '{"status":"running","pid":%s,"startedUtc":"%s"}\n' "$$" "$started_utc" > "$status_file"

if $install_dependencies; then
    sudo -n apt-get update > "$bench_root/logs/apt-update.log" 2>&1
    sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        build-essential pkg-config curl ca-certificates xz-utils libssl-dev \
        zlib1g-dev libbz2-dev libreadline-dev libsqlite3-dev libffi-dev liblzma-dev \
        libncursesw5-dev libgdbm-dev libgdbm-compat-dev libexpat1-dev uuid-dev \
        > "$bench_root/logs/apt-install.log" 2>&1
fi

download_checked() {
    local url=$1 target=$2 digest=$3 algorithm=$4
    if [[ ! -f "$target" ]]; then
        curl --fail --location --silent --show-error --retry 2 --connect-timeout 15 \
            --max-time 600 "$url" --output "$target.partial"
        printf '%s  %s\n' "$digest" "$target.partial" | "$algorithm" --check --status
        mv "$target.partial" "$target"
    fi
    printf '%s  %s\n' "$digest" "$target" | "$algorithm" --check --status
}

sdk_archive="$bench_root/downloads/dotnet-sdk-$sdk_version-linux-x64.tar.gz"
download_checked "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" \
    "$sdk_archive" "$sdk_sha512" sha512sum
mkdir -p "$sdk_prefix"
if [[ ! -x "$sdk_prefix/dotnet" ]]; then
    tar -xzf "$sdk_archive" -C "$sdk_prefix"
fi
export DOTNET_ROOT="$sdk_prefix"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
[[ "$(cd "$sdk_prefix" && ./dotnet --version)" == "$sdk_version" ]]
"$sdk_prefix/dotnet" --list-runtimes | grep -Fx "Microsoft.NETCore.App $runtime_version [$sdk_prefix/shared/Microsoft.NETCore.App]"

python_archive="$bench_root/downloads/Python-$python_version.tar.xz"
download_checked "https://www.python.org/ftp/python/$python_version/Python-$python_version.tar.xz" \
    "$python_archive" "$python_sha256" sha256sum
if [[ ! -x "$python_prefix/bin/python3.13" ]]; then
    tar -xJf "$python_archive" -C "$bench_root/builds"
    python_build="$bench_root/builds/cpython-$python_version-pgo-lto"
    mkdir -p "$python_build"
    (
        cd "$python_build"
        "$bench_root/builds/Python-$python_version/configure" --prefix="$python_prefix" \
            --enable-optimizations --with-lto --enable-experimental-jit=no --with-ensurepip=no \
            > "$bench_root/logs/cpython-configure.log" 2>&1
        make -j "$(nproc)" > "$bench_root/logs/cpython-build.log" 2>&1
        make altinstall > "$bench_root/logs/cpython-install.log" 2>&1
    )
fi

"$python_prefix/bin/python3.13" -I -S - "$bench_root" "$python_sha256" "$sdk_sha512" <<'PY'
import gc
import hashlib
import json
import pathlib
import platform
import pyexpat
import ssl
import subprocess
import sys
import sysconfig
import unicodedata
import zlib

root = pathlib.Path(sys.argv[1])
dotnet = root / "dotnet/10.0.401/dotnet"
assert platform.python_implementation() == "CPython"
assert sys.version_info[:3] == (3, 13, 16)
assert not sysconfig.get_config_var("Py_DEBUG")
assert not sysconfig.get_config_var("Py_GIL_DISABLED")
assert sys.flags.isolated == 1 and sys.flags.no_site == 1
assert gc.isenabled()
config = sysconfig.get_config_var("CONFIG_ARGS")
assert "--enable-optimizations" in config and "--with-lto" in config
assert "--enable-experimental-jit=no" in config

def digest(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

receipt = {
    "schemaVersion": 1,
    "profile": "linux-x64-cpython-3.13.16-pgo-lto-dotnet-10.0.401",
    "python": {
        "version": sys.version,
        "executable": sys.executable,
        "sha256": digest(sys.executable),
        "sourceSha256": sys.argv[2],
        "configArgs": config,
        "compiler": platform.python_compiler(),
        "cflags": sysconfig.get_config_var("CFLAGS"),
        "pgoFlags": sysconfig.get_config_var("PGO_PROF_USE_FLAG"),
        "ltoFlags": sysconfig.get_config_var("LTOFLAGS"),
        "debug": bool(sysconfig.get_config_var("Py_DEBUG")),
        "freeThreaded": bool(sysconfig.get_config_var("Py_GIL_DISABLED")),
        "gilEnabled": sys._is_gil_enabled(),
        "jit": "disabled at build time",
        "gcEnabled": gc.isenabled(),
        "flags": repr(sys.flags),
        "paths": sys.path,
        "zlibCompileVersion": zlib.ZLIB_VERSION,
        "zlibRuntimeVersion": zlib.ZLIB_RUNTIME_VERSION,
        "expatVersion": pyexpat.EXPAT_VERSION,
        "opensslVersion": ssl.OPENSSL_VERSION,
        "unicodeVersion": unicodedata.unidata_version,
        "libc": platform.libc_ver(),
    },
    "dotnet": {
        "sdk": "10.0.401",
        "runtime": "10.0.12",
        "executable": str(dotnet),
        "sha256": digest(dotnet),
        "archiveSha512": sys.argv[3],
        "info": subprocess.check_output([str(dotnet), "--info"], text=True),
    },
    "buildPackages": subprocess.check_output(
        ["dpkg-query", "-W", "-f=${Package}\t${Version}\n"], text=True),
    "platform": platform.platform(),
    "performanceQualified": False,
}
temporary = root / "toolchains.json.partial"
temporary.write_text(json.dumps(receipt, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
temporary.replace(root / "toolchains.json")
print("Pinned toolchain receipt: " + str(root / "toolchains.json"))
PY
