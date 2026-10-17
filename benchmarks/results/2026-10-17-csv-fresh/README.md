# Complete CSV job, fresh compile and run — 2026-10-17 UTC

This fills the fresh compile/run gap for the existing complete CSV summary.
Both Lython controls use the identical Release source
0f1cff1fd2ff945b086d19183e5bae497ef01f41; CPython is the external baseline.
The controls are separate processes, with independent execution state.

| Engine | Fresh compile/run median | IQR / median |
| --- | ---: | ---: |
| Lython control A | 8.114 ms | 14.79% |
| Lython control B, identical binary | 8.133 ms | 7.15% |
| CPython 3.13.16 | 3.8777 ms | 0.89% |

One short diagnostic, with substantial Lython spread and no qualified multiplier.
The 0.23% difference between identical Lython controls is within their spread.
This capture measures compile-to-result cost for a useful job. Historical warm
CSV [C](../2026-10-14-representative-csv/README.md) uses a different revision;
compilation-only and process startup remain separate unmeasured boundaries here.

The unchanged scripts.csv-summary.large reads 4,096 records / 57,361 UTF-8 bytes,
including quoted multiline café notes, emoji and negative amounts to ignore.
It aggregates accepted amounts by key, sorts the keys and writes report.csv:
blue=7,168, red=3,072; stdout reports 3,072 accepted records. Source: 533 UTF-8
bytes, SHA256 2e83f4a6b80d1eb3e326fe6eba8e4ef37a31ae4ec19a7d59c848a1736f098f2c.
Independent exact input/stdout/report goldens pass at the existing 0/16/1,024
block sizes (0/64/4,096 records), including empty input. Complete output-file
sets and bytes are checked; every measured invocation must match.

Boundary: fresh compilation and one synchronous execution per timed job in
already-running workers. Both engines include namespace/context setup, file
reset/staging, execution and complete output-byte checks. Lython includes prior
result and temporary compilation disposal. Process startup, transport and full
response hashing are outside the worker timer. Timed jobs compile fresh source
without identical-script execution caching. Compiled verification caches and the
final response retain their recorded owners; cleanup follows the existing worker
contract. Cooperative accounting uses a shared unbounded allowance with ordinary
public execution settings and instruction fuel unset. CPython has no equivalent
in-process containment machinery.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, Linux
6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs, 16 GiB RAM, no swap. .NET SDK
10.0.401 / runtime 10.0.12, ordinary workstation GC and worker tiering. CPython
3.13.16, GCC 13.3, PGO/LTO, ordinary GIL/GC, -I -S -X utf8. Only supervisor
tiering is disabled. Seven rotating-order rounds span 8.400 s under a 28 s
process-group cap. All 1,954 frozen files (including 524 production C# files)
match. Five workers and their fixtures are gone, service units inactive and the
VM lease free. One capture, no retry, retuning, new producer or full lane.

The source also reuses ten immutable numeric/text builtin definitions through the
existing fixed-definition block. Per-run wrapper, temporary parameter-array and
signature-catalog work disappear; mutable graphs/dictionaries/modules and current
host bindings remain per execution. A separate native allocation witness at
9e9135bb → 0f1cff1f observes 27,568 → 26,088 allocated bytes for each of 32 fresh
context constructions after two warmups: 1,480 bytes saved per construction.
This excludes first static initialization and measures allocation, not script
time or CPython allocation. Its temporary test was removed after capture;
[archived probe source](native-setup-probe.txt) is evidence outside live builds.
The identical-control CSV comparison does not measure that change's speedup.

Local Release gates pass: 2,939 ordinary white-box, 59 original stack/trace/copy,
483 public builtin/JSON/admission/lifetime and 93 representative CSV/text/JSON/
file retention/cancellation checks. All 68 frozen fuel/span traces match; original
fixtures, timeouts and managed protection bounds remain unchanged. Extended
existing isolation checks cover all thirteen shared plain definitions. Explicit
Release package and Windows/Ubuntu qualification belong to the delivery revision,
separate from this measured source snapshot.

[Samples](compile-run.json), [declaration](comparison-declaration.json),
[three-size correctness](correctness.json), [independent goldens](independent-goldens.json),
[catalog](catalog-micro.json), [pre-capture gate](before-proof.json),
[source/cleanup proof](cleanup-source-proof.json), [local checks](local-validation.json),
[separate native allocation evidence](native-setup-allocation.json).
