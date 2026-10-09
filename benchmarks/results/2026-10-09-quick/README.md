# Bounded comparison run, 2026-10-09

All four lanes completed inside the **ten-minute wall limit per lane**.
Collection took **14 minutes 57 seconds** in total, from 11:20:38 to 11:35:35 UTC.
**No workload ratio qualified.** These results record a completed diagnostic
run; they do not establish a qualified performance baseline or a winner.

| Lane | Wall time including preparation and cleanup | Collected all three sessions | Excluded during collection | Qualified workload ratios |
| --- | ---: | ---: | ---: | ---: |
| [Warm public invocation](warm.md) | 3m43s | 9/14 | 5/14 | 0 |
| [Compile and run](compile-run.md) | 1m53s | 1/14 | 13/14 | 0 |
| [Compilation](compile.md) | 1m45s | 1/14 | 13/14 | 0 |
| [Fresh process](fresh-process.md) | 7m36s | 12/14 | 2/14 | 0 |

Counts include the empty and tiny invocation controls. Finishing collection
does not establish eligibility. The linked tables were rendered from the exact
raw receipts and show every case, retained session median and exclusion. A dash
means missing evidence or an ineligible ratio, rather than zero elapsed work.

## Frozen profile

Measured source: **`9358c4e72f1a29e55703eee02b28ea404d01bc0a`**, clean committed
checkout, locked restore and Release build. Protocol/catalog versions are 1;
sampling policy and eligibility versions are **4**. Workload selection preceded
timing: twelve jobs cover integer loops, keyword calls, stable sort, tuple-key
dictionaries, generators, supplementary Unicode, ASCII text, JSON, retained CSV,
XML and zlib. The 131-case correctness catalog remains broader than this timing
profile. Regex waits for Utf8Regex qualification.

The dedicated Azure **Standard_F4as_v7** VM ran Ubuntu **24.04.4 LTS**, x86_64,
with four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB provisioned memory and
no swap. Both engines used the same machine sequentially, with normal CPU
topology and no affinity or priority changes. The guest clock source was `tsc`;
parent and worker monotonic clocks reported a frequency of 1,000,000,000 ticks/s.
The guest exposes no CPU governor interface, and these checks cannot prove an
idle physical host.

Lython used SDK **10.0.401**, runtime **10.0.12**, workstation GC with Interactive
latency, normal worker tiering/PGO and ordinary public limits. Optional instruction
fuel was unset. CPython **3.13.16** was independently built with GCC **13.3.0**,
PGO/LTO, ordinary GIL and cyclic GC, without debug/JIT, and launched with `-I -S`.
Its build records zlib **1.3**, Expat **2.8.5** and glibc **2.39**. Native-library
jobs measure their complete applications, including different implementations.

Only the collection supervisor used `DOTNET_TieredCompilation=0`; every worker
launch removed that inherited setting. Workers reported no runtime overrides.
This preparation change followed separate thread-CPU diagnostics and did not
disable Lython worker specialization. Failed earlier-policy trials are retained
separately and their samples are not included here.

Persistent workers warmed for at least 32 invocations and one second; fresh
preparation used eight exactly-once jobs and at least 100 ms. Each case requested
three independent sessions with seven alternating AB/BA pairs per session.
Calibration required two confirming batches of at least 10 ms; retained measured
batches had to last at least 5 ms. Half-second idle checks followed 50 ms settling.
The frozen spread, order, paired interval, cross-session and control-overhead
rules are in [the comparison contract](../../COMPARISON.md).

Each lane had its own systemd service with `RuntimeMaxSec=600`,
`TimeoutStopSec=0` and `KillMode=control-group`, plus a common exclusive VM lease.
The driver reserves ten seconds for cleanup and retains the original deadline
across at most three attempts. All four lanes finished on attempt 1 with exit 0;
terminal service records show inactive state and no main process. No build,
test or artifact transfer ran on the VM during collection. Reports were rendered
offline after all timing stopped.

## Why no ratios qualified

Warm invocation collected nine complete cases, but its empty control failed
spread and interval checks. The first empty-control session had Lython
IQR/median **12.79%**, above the frozen 10% ceiling. Consequently every workload
failed the required-control rule. Other rows also failed spread/order checks,
the ten-times-control requirement or an idle gate. The tiny control qualified.

Compilation and compile-and-run each excluded thirteen cases during collection,
primarily following idle CPU activity. Their empty controls did not complete all
three sessions. Compilation's tiny control qualified; compile-and-run's tiny
control failed spread. Passing initial and final idle checks does not override a
failed sampling check. The separate earlier diagnostics observed both supervisor
and worker background compilation, but this uninstrumented run does not establish
the cause of every remaining noise event.

Fresh-process collection produced twelve complete cases and both controls
qualified. Every workload nevertheless failed the frozen requirement to exceed
ten times each engine's larger invocation control: the jobs were too small to
separate workload execution from startup on this boundary. The ASCII pipeline
and zlib also stopped on idle checks. Startup is deliberately included in this
lane; this outcome calls for a prospective review of its eligibility contract,
not reclassification of these receipts. No control was subtracted.

The recorded medians can guide follow-up diagnostics, with their displayed
exclusions. They cannot establish comparative speedups, an overall ranking or
runtime optimization priorities. Short collection preserves the statistical
rules but provides limited evidence and coverage. No memory ratio is reported:
managed allocation, governor peaks and Python tracemalloc cover different things.
Any memory comparison requires separate runs using the same OS process metric
and invocation count for both engines.

## Evidence identity

Raw JSON, prepared payloads, logs, toolchain receipts, service records and test
results remain in ignored local evidence storage. The following digests identify
the exact receipts used to generate the linked reports. Source and file hashes
were checked before and after each lane. The renderer recomputed eligibility
from raw evidence rather than trusting saved statistics or qualification flags.

| Receipt | SHA-256 |
| --- | --- |
| warm.json | `fe6b4385abf1e13ded93e28f7a31805f2bb7286200c55fdc66c8f85138cbbba5` |
| compile-run.json | `161a909596867db4b3e2591afb834d804e0cfa5b8b83303e842deb9b2bbfcdcd` |
| compile.json | `f2ca5315980dbac89b017dd4928610e5f7fac8a66b6445487a8f38b5d86cf479` |
| fresh-process.json | `165a684c52fb0b3398813b1be99ec85ddac71caee16aeff5777cbb36cf52438d` |

Common quick catalog SHA-256:
`e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1`.
Toolchain receipt SHA-256:
`1569c441a1c20a1ac5f895491cbe7f24668e60e637137e67a8640c1e0d38ef4f`.

| Artifact | SHA-256 |
| --- | --- |
| Linux Release Lokad.Lython.dll | `b85d0a8d950d2f1e909a819b2ffce6d5fb4e454bcd6e46896cca85dc9c5f3270` |
| Linux Release benchmark adapter | `916d6127c4f24b6b3160dfe336ab2622d5ef54b9b9ff201ea69c40b546e57346` |
| cpython-worker.py | `e6a81d0f72b6ee2f9438b76a9012b42e944e8ce52d616a9445837dbac976cc1d` |
| CPython executable | `673525f3e1ac992831891979187e259c06bbd5e331ab50c0bb697f274ecb0f2a` |
| dotnet launcher | `01d89e0a0191052bfea616cd4ce624c8faf13b05bbddf7f64499c23e2a9d9269` |
| System.Private.CoreLib.dll | `26304a2985357b9ee277f273667fcd9c892edae3ee6eba76f739033e95eb39b3` |

The exact producer passed 296 focused checks in local Debug/Release and pinned
Linux Release. Both Windows and Ubuntu full Release suites, explicit probe builds
and exact package-consumer checks passed on the first
[CI attempt](https://github.com/Lokad/Lython/actions/runs/37922865885).
These checks validate correctness and delivery, not timing eligibility.

## Reproduce

Check out the measured revision in a clean directory, prepare the pinned
toolchains and build Release with locked dependencies before timing. The driver
exports the frozen quick catalog. Run the four lanes serially; each command
needs a fresh service name and fresh output directory, and all commands share
one VM lease. For example, from the repository root on the dedicated Linux box:

```bash
dotnet="$TOOLCHAIN_ROOT/dotnet/10.0.401/dotnet"
python="$TOOLCHAIN_ROOT/cpython/3.13.16/bin/python3.13"
toolchains="$TOOLCHAIN_ROOT/toolchains.json"
root=$(pwd)

"$dotnet" restore Lokad.Lython.slnx --locked-mode
"$dotnet" build benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release --no-restore
"$dotnet" build-server shutdown

sudo systemd-run --wait --pipe --unit=lython-comparison-warm \
  --property="User=$USER" --property="Group=$(id -gn)" \
  --property=RuntimeMaxSec=600 --property=TimeoutStopSec=0 \
  --property=KillMode=control-group --working-directory="$root" \
  /bin/bash "$root/benchmarks/run-comparison-linux.sh" \
  "$dotnet" "$python" "$toolchains" "$OUTPUT_DIRECTORY" "$VM_LEASE_FILE" warm
```

Set `TOOLCHAIN_ROOT`, `OUTPUT_DIRECTORY` and `VM_LEASE_FILE` to absolute paths.
Repeat the service invocation with distinct unit names and lane arguments
`compile-run`, `compile` and `fresh-process`, preserving the same ten-minute cap.
Do not rerun completed exclusions to obtain better eligibility. Once all four
services are terminal, render each receipt offline:

```text
dotnet <benchmark.dll> --compare render-report --receipt <lane.json> --out <lane.md>
```

A qualified comparison remains pending. Diagnose residual idle activity and
control variance with bounded separate instrumentation; review startup and
compilation eligibility prospectively. Any revised policy needs its own frozen
manifest and receipts within the same ten-minute lane budget. These results
remain excluded under policy v4.
