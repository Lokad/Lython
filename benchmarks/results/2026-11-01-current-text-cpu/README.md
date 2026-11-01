# Current text/CSV CPU assessment (B29)

Source **830c6251**, 2026-11-01, dedicated Ubuntu 24.04.4 Azure F4as_v7,
four EPYC 9V45 vCPUs / 16 GiB, pinned .NET 11 RC3. Two fixed captures,
**15.676 seconds total**, below the predeclared 30-second capture limit.
Both quiet-machine gates and all complete stdout/file goldens pass. Workers
and profilers close/join; the collector service is inactive and lease available.
No runtime prototype, recapture, elapsed improvement or CPython ratio is claimed.

The preceding [representative checkpoint](../2026-11-01-representative-checkpoint/README.md)
finds the largest current gap in warm text, including an adverse old/new point
median. This assessment asks which current native work supports one coherent
next change. Earlier B05/B22 captures describe different source revisions and
do not establish this source's removable fraction.

## Fixed boundary and observations

Reused validated B22 external perf driver, actual synchronous public warm runs,
1,024 complete jobs per case, cpu-clock at 199 Hz with frame-pointer chains and
CLR perf maps. Compilation/startup excluded; real file work, full validation and
declared cleanup included. Ordinary GC/tiering. Native profiling stays entirely
outside production; Lython remains fully managed and memory-safe.

| Complete workload | Capture wall | Actual guest CPU | Guest samples | Unknown leaves |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers, control | 8.609 s | 6.94 s | 1,380 | 120 |
| Text helpers | 7.067 s | 5.33 s | 1,059 | 74 |

Actual guest thread IDs come from the recorded execution thread, never a max-CPU
heuristic. All thread deltas are retained; background tiered-JIT CPU is 0.00 s at
the kernel counter's resolution in both capture windows. That does not prove no
JIT work or qualify steady-state latency. All sampled chains have multiple
frames, but inlined/missing/native-transition frames remain possible. Perf-map
malformed trailing records and unknown leaves are preserved rather than repaired.

## Findings and bounded source assessment

Text has **113/1,059 self samples (10.7%) in SigmaPropertyContains**, versus zero
in the CSV control. Lowercasing currently binary-searches generated Unicode 15.1
Cased/Case_Ignorable intervals for each rune's final-sigma context. The same
properties are searched during forward context scans. This is a concrete,
material algorithmic cost; it is not proof of a particular whole-script speedup
or the cause of the checkpoint's entire text regression.

Main dispatch self samples are 176 text / 151 CSV; this includes required opcode,
cancellation and admission work. Monitor self leaves total 85 text / 117 CSV,
distributed over actual text/CSV storage, iteration and native working capacity.
Unknown/TLS/barrier/monitor origins remain in the raw analysis. Named call/frame
helper self leaves are small; they do not establish that all frame allocations,
boxing or argument transport are free, or justify a universal value/frame/async
redesign. Inclusive native work must not be relabeled as removable overhead.

One bounded **offline data assessment**, without changing runtime or collecting
more CPU, expands the existing two property arrays into exact two-bit flags.
Deduplicating 256-scalar pages yields **98 pages**, 4,352 one-byte page IDs and
6,272 packed-data bytes: **10,624 immutable bytes**, versus 4,752 interval bytes.
A constant-time lookup can replace both binary searches, trading **5,872 bytes**
of generated static data for less repeated search work. No Unicode database,
runtime cache, property framework or new memory mechanism is required.

This proposes replacing the property representation, while keeping the existing
casing/builder pipeline and context rules. The rejected bulk-ASCII prototype
remains closed. Exact flags for every scalar, Greek final sigma, case-ignorable
overlap, supplementary/context boundaries, expansion behavior, cancellation and
governed output need independent acceptance before any candidate is retained.

## Evidence and stopping point

[summary.json](summary.json) records exact loaded identities, counts, source and
archive hash. [raw-capture.tar.gz](raw-capture.tar.gz) preserves perf data/maps,
complete records, guest leaves, caller chains, native origins, all thread deltas,
goldens, catalog, machine gates and collector logs. Readable collector/analyzer
sources accompany it. One offline derivation uses the same saved events; no new
timing or sampling is hidden in that analysis.

Stop B29. PLAN.md records the proposed property-table replacement, deletion,
metadata tradeoff, semantic/ownership checks and a fixed complete-job
before/candidate/identical-before comparison **before implementation**. Samples
identify a bounded next hypothesis; they do not predict its accepted speedup.
