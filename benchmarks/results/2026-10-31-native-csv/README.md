# Native UTF-8 CSV record materialization

Retained: this replaces one complete conversion pipeline and removes duplicated
scratch accounting. It does not explain the remaining interpreter/CPython gap.
Source is `3d0cd5e6` plus the exact CSV `source.patch` in the raw archive; baseline
is `07c150a6`. These are revision-specific diagnostics, not current-HEAD or
qualified overall speedup claims.

The [existing allocation stacks](../2026-10-31-architecture-profiles/README.md#subsequent-offline-attribution)
identify CLR strings in CSV line decoding and field accumulation. The new parser
borrows native UTF-8 lines, compares scalar dialect tokens as UTF-8, accumulates
bounded ordinary-text runs in the existing governed byte builder and copies each
completed field once. It removes whole-line decoding/reservations, CLR dialect
copies, StringBuilder capacity/growth/append helpers and field re-encoding. The
file decoder and writer remain unchanged. No pool, alternate parser or new policy
is introduced. Working capacity/replacement peaks stay funded; EOF releases scratch
without refunding queued fields/rows. Cancellation also checks after native growth.

## Allocation diagnostic

One fixed local before/after/identical-before observation reuses the maintained
`CsvReaderBenchmarks` scripts and seeded six-column file, **1,000 records** per
invocation. Warm public RunAsync includes the return-count golden (`1000`) and
result disposal; compilation/startup are excluded. Four warmups precede seven
rounds of five invocations per shape. All setup scripts are disposed. The
[one-off driver](allocation/Program.cs) adapts the existing regex diagnostic;
reflection overhead is common. This is no additional benchmark framework.

| Shape | Before bytes / invocation | After bytes / invocation | Reduction |
| --- | ---: | ---: | ---: |
| Retain all dictionaries | 2,724,309 | 2,331,941 | 14.4% |
| Discard rows after counting | 2,738,386 | 2,346,018 | 14.3% |

The identical-before allocation control matches both baselines. The reduction is
392,368 managed bytes per invocation in each shape. Raw observations retain timing
too, but this development machine has no quiet gate: no qualified elapsed claim
follows. Windows 10.0.26200, 28 logical processors, .NET 11 RC3; tiering disabled
consistently, ordinary GC, no concurrent local builds/tests. All owned processes
complete within their 30-second caps. [Before](before-allocation.json),
[after](after-allocation.json), [control](before-control-allocation.json) record
all rounds and loaded assembly hashes. These allocation boundaries differ from
the complete-script timing below.

## Fixed representative-script comparison

2026-10-31 UTC, dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs,
16 GiB, identical .NET 11 RC3 runtime. Both quiet-machine gates pass. The fixed
sixteen-job collection takes **130.87 seconds** within its 180-second budget,
holding the shared VM lease. Two observations cover existing CSV summary scripts
with/without helpers, with unchanged text/JSON helper scripts as controls. The
existing micro command rotates baseline/candidate/CPython order across seven
25-ms batches after one-second warmups and settling. Worker GC/tiering is ordinary.
Each process group has a 30-second external deadline; every worker joins cleanup.

Warm milliseconds per complete script:

| Script | Observation 1 old / new | Observation 2 old / new | Status |
| --- | ---: | ---: | --- |
| CSV summary, 4,096 records | 6.438 / 6.186 | 6.466 / 6.201 | 3.9% / 4.1% lower point medians; first baseline noisy |
| CSV helpers, 4,096 records | 7.922 / 7.268 | 7.914 / 7.389 | 8.2% / 6.6% lower point medians |
| Text helpers control | 6.100 / 5.786 | 5.754 / 5.767 | 5.2% lower / 0.2% higher point medians |
| JSON-lines helpers control | 8.116 / 8.039 | 7.821 / 7.896 | 1.0% lower / 1.0% higher point medians |

The time changes are modest and short diagnostic samples do not establish a
qualified multiplier. Complete stdout/file goldens match for both sources and
CPython before/after every job. Warm runs reuse compiled code, including fixture
reset/staging, real file work and declared ownership cleanup, excluding startup
and compilation. CPython remains the external baseline in
[BENCHMARKS.md](../../../docs/BENCHMARKS.md); its primary table is not relabeled.

Fresh compile/run observations remain visible: the plain CSV row reverses from
8.041/42.471 ms to 48.014/7.019 ms; text also has large reductions and substantial
variation. These rows are inconclusive, even with quiet gates. Helper CSV point
medians fall 4.1%/3.6%, while JSON controls are mixed. Do not select the favorable
fresh rows, infer compilation time by subtracting unrelated medians, or claim a
new CPython multiplier. [collection.json](collection.json) preserves every row,
IQR and the CPython context.

[raw-results.tar.gz](raw-results.tar.gz) contains exact source patch, collection
source, process/assembly/toolchain identities, inputs/goldens, every response/sample,
machine gates and logs. Full source/control identity is reproducible from Git and
the patch. No failed observation was rerun or removed.

## Acceptance and stopping point

Focused Release validation passes 100 internal and 341 public CSV checks on each
supported target (.NET 11 and .NET 10), including independent CPython Unicode
delimiter/quote/escape and chunk-boundary controls. Existing tests retain actual
denial/rollback assertions while using byte capacity instead of StringBuilder
layout. Coverage includes multiline/strict errors, nonnumeric conversion,
single-pass/header aliases, native cancellation during growth, terminal refusal,
genuinely suspended input/protocols, retained fields/rows, EOF queues and budget reuse.

Retain the simpler native pipeline for its removed mechanisms and material
allocation reduction; no repeatable control-adjusted elapsed regression appears
in the fixed warm observations. Its small elapsed changes leave most of the
performance gap unresolved. Stop this redesign here. Do not broaden it into the
file decoder, value representation or another tuning campaign. Full platform/
stress/package qualification belongs to the next coherent delivery boundary.
