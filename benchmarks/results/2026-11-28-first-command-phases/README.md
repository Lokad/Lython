# B1927 Attribute first-command configuration startup

The November 28, 2026 local diagnostic uses the exact `486b6bb60` core,
the B1926 configuration payload and .NET 11 RC3 on Ubuntu WSL, pinned
to one logical CPU. The instrumented image differs from the frozen
32-file comparison image only in its benchmark worker DLL. It adds
internal timestamps and a final timing line. Each timed observation
launches a fresh process. Twelve rounds rotate the frozen Lython,
instrumented Lython and stock CPython 3.13.16 workers. All 36 timed
verifications agree on the output and file goldens.

| Worker | Median launch to result, exit and drain | Median child process CPU |
| --- | ---: | ---: |
| Frozen Lython | 403.38 ms | 225.65 ms |
| Instrumented Lython | 403.41 ms | 228.79 ms |
| CPython | 216.37 ms | 31.46 ms |

The parent starts timing after creating an empty RAM-backed fixture
directory and stops after joining the child and draining both pipes.
Fixture reset, compilation, one run, worker cleanup and reply writing
are inside the child. Parent payload validation is outside. The images
and toolchains reside on the Windows-mounted workspace. Elapsed
outliers exceed one second in every worker. Preserve them. These local
elapsed measurements describe this diagnostic and leave the published
quiet measurement at its original revision.

| Instrumented Lython phase | Median elapsed |
| --- | ---: |
| Catalog load | 12.48 ms |
| Once-worker entry after the catalog | 35.12 ms |
| First compile, with engine creation | 141.69 ms |
| First public run | 75.81 ms |
| Reply identity, serialization and write | 78.74 ms |
| In-worker disposal and return | 0.18 ms |
| Launch and exit remainder | 46.37 ms |

The remainder subtracts the measured in-worker interval from each
process's outer clock. It includes time before the first managed
marker, the timing line, exit and drain. Phase medians do not add to the
outer median. The similar frozen and instrumented elapsed medians and
roughly 3 ms CPU difference bound the observed instrumentation effect.

Two separate EventPipe captures launch the frozen worker and each
verify the same configuration edit. Each capture matches 2,679 JIT
start and method-load events on the same thread and method. The
intervals are wall times. They can overlap, include stalls and do not
estimate an exclusive CPU share. The traced worker entry varies from
37.20 to 173.54 ms. The frontend's callable data factory has 16,573
bytes of IL and takes 4.52 and 5.05 ms between its JIT events. The
static module catalog takes 8.05 and 4.17 ms. The interpreter dispatch
does not dominate these traces. Inspect metadata that initializes
before any compatible receiver requests it.

The first launcher used the wrong fixture directory shape and the
first library check assumed CoreLib came from the worker folder. Both
pre-sampling failures remain in the evidence. The corrected launcher
uses the existing fixture convention and verifies the runtime library
path separately. The trace reader's first build also records the
repository package allowlist refusal. Its successful build uses
diagnostic-only project settings.

[Phase summary](summary.json) · [JIT intervals summary](jit-summary.json) ·
[Raw timings, traces and procedures](observations.tar.xz).
