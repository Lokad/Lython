# Governed memory: short CSV diagnostic

One 7.320-second owned run on 2026-10-14 compares warm execution and shared
printed output for the existing `csv.discard.small` workload (16 data rows).
Compilation and process startup are outside the timer. The baseline is separately
built `9fc7e37e` source; the candidate is the uncommitted governed-memory migration
on that base, before the subsequent local-import compilation simplification.
The candidate's source manifest and loaded DLL hashes identify the measured state;
this result does not describe later changes or today's HEAD.

| Engine | Median per job | IQR / median |
| --- | ---: | ---: |
| Earlier Lython | 87.786 µs | 3.8% |
| Governed-memory candidate | 77.957 µs | 9.3% |
| CPython 3.13.2 | 28.463 µs | 2.4% |

This is diagnostic evidence only: one run, no qualified speedup, CPython
multiplier, confidence interval or embedding-latency conclusion. It will not be
repeated to replace the noisier samples. The shared Windows 11 x64 development
workstation has an i7-14700KF, 28 logical processors and 64 GiB RAM, with .NET
10.0.13 / SDK10.0.300-preview.0.26177.108. CPython uses ordinary GIL/GC and `-I -S`.
[All attempts, toolchain/input/output and loaded identities](micro.json) and
[measured production source hashes](production-sources.json) preserve provenance.

The runtime migration removes weak registrations, paging/sweeps and forced
collections in favor of coarse cooperative ownership. Focused controls verify
shared admission, retained output/compiled cache funding, nested executions,
cancellation waiting for borrowed buffers and batch cleanup returning allowance.
Legacy assertion migration and full platform/package qualification remain open.
Removing forced collections does not by itself qualify host tail latency.
