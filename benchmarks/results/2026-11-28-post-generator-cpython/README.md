# B1958 Quiet shipment and JSON comparison after private generator reduction

Lython `09cecc9ce` and stock CPython 3.13.16 ran on November 28, 2026
UTC on the four-vCPU Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Both quiet gates pass. The complete 32-file image and five output and
file goldens verify locally and on the VM. All 196 timed batches verify.

| Warm workload | Lython API | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary helper and tuple-generator reduction | `Run` | 2.42135–2.45242 ms | 1.98465–1.99082 ms | 1.216–1.236× | 1.199–1.204× |
| Ordinary helper and tuple-generator reduction | `RunAsync` | 2.62034–2.67304 ms | 1.97306–1.98094 ms | 1.323–1.355× | 1.320–1.379× |
| Iterable shipment | `RunAsync` | 2.16221–2.24932 ms | 1.94789–1.96819 ms | 1.099–1.155× | 1.106–1.159× |
| Repeated explicit tuple loop | `RunAsync` | 1.10356–1.16394 ms | 1.60551–1.63864 ms | 0.673–0.725× | 0.703–0.747× |
| Markdown report from JSON | `RunAsync` | 2.66623–2.75064 ms | 2.57045–2.58583 ms | 1.031–1.070× | 1.069–1.114× |
| File-fed CSV quantity map | `RunAsync` | 0.41933–0.44923 ms | 1.35960–1.36084 ms | 0.308–0.330× | 0.341–0.363× |

The ordinary shipment compiles and runs in 2.45588–2.51680 ms on Lython
and 2.05818–2.05852 ms on CPython. This lane uses Lython's synchronous API,
with a 1.193–1.223× elapsed ratio and a 1.180–1.216× process CPU ratio.
It includes compilation and retirement of previous compiled storage.

Ratios divide Lython by CPython and span the pairings of two worker medians
per engine. Warm elapsed includes RAM-backed I/O, output and file checks,
and disposal of the previous result. Process CPU also includes fixture
reset and request framing. Both boundaries use running processes.
Lython uses the mediated host with shared memory admission. CPython uses
native file access and its stock interpreter under `-I -S -X utf8`.
The report reads 3,072 records from a 189,441 B JSON file. Each shipment
script reads 2,048 rows from a 27,350 B CSV.

The ordinary async reduction has a smaller gap than at B1954, whose
elapsed ratio was 1.449–1.475×. B1957 supplies the controlled
revision-to-revision comparison for attributing the improvement.
The report's CPython worker spread is 0.60%, down from 11.23% in
the earlier comparison. The file-fed map remains faster than CPython
with 7.13% spread between Lython workers. [B1954](../2026-11-28-combined-loop-cpython/README.md) ·
[B1957](../2026-11-28-borrowed-generator-sum/README.md).

## Paired profiles

Ubuntu WSL captures use the same frozen image and catalog. Four seconds of
warmup precede each six-second EventPipe capture on one logical CPU.
Both scripts use `RunAsync` and verify 75 sampled batches in total.
Percentages describe resolved managed samples with known waits and
unclassified native leaves removed. Inclusive regions overlap.

| Region | Generator reduction | Explicit loop |
| --- | ---: | ---: |
| Borrowed generator lifecycle, inclusive | 34.5% | 0.0% |
| Borrowed generator instruction prefix, inclusive | 20.0% | 0.0% |
| Preparing the borrowed operand, inclusive | 8.4% | 0.0% |
| Executable frame construction, inclusive | 3.4% | 0.0% |
| Shared executable instruction dispatcher, nearest | 18.1% | Outside the top 15 |
| Native instruction loop, nearest | Outside the top 35 | 32.7% |

The ordinary helper still uses the shared executable dispatcher. Its
generated child has seven slots, and the private reduction keeps those slots
through the numeric loop or promotion. The explicit helper uses the native
loop. The locality of the setup and release costs favors examining repeated
argument binding and operand ownership ahead of a parent-frame redesign.
The profile gives an attribution for these builds and workloads.

Staging streams the new core DLL and links 31 files from the verified
read-only donor image. The VM had 19.1 MB free before staging preparation.
Two completed images' core DLLs were losslessly compressed. Their restored
byte hashes and the original observation archives verified. The timed
worker processes join and the guest fixtures are removed before the
archive verification finishes.

[Timing](summary.json) · [Image](identity.json) · [Observations](observations.tar.xz) ·
[VM status](terminal-status.json) · [Attribution](managed-attribution.json) ·
[Layouts](layout.json) · [Raw profiles](profiles.tar.xz) · [Procedures](procedures.tar.xz).
