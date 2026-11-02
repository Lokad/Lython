# Complete report rendering: formatting cost gate

2026-11-02. **No runtime change, before/after speedup or CPython timing ratio.**
Source is `43936b8b` plus the maintained report-rendering workload added here;
runtime source remains `4cae38f4`. The previous numeric report chiefly exercises
JSON and builtin sums. This case fills the missing record-wise report workload.

The same plain Python reads JSON, calls a helper for each Markdown row, formats
Unicode names with scalar precision/alignment, centers status strings and groups
integers, then writes the report. Catalog scales contain 0 / 48 / 3,072 records.
Goldens are independently constructed outside timing. Isolated CPython validates
all stdout/file bytes at all three scales, locally and on the VM. Existing file
worker/micro tests pass **75 focused Release cases on each actual .NET 11/10**,
including fresh-fixture replay for the new cases. No benchmark framework or
runtime policy is added.

## One capture and decision

Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11
RC3, ordinary GC/tiering. The existing checked worker, budget witness, phase
driver and external native profiler are reused. Quiet gates and full goldens
pass before/after. **One 2.08344 s capture**, 160 complete warm jobs, in an
**11.299 s** owned service. The actual named guest consumes 1.38 CPU seconds;
background JIT consumes another 0.63 s. All users join, final allowance is zero,
fixtures disappear normally, service PID/control group are empty and lease is free.

Of **273 actual guest samples, 78 (28.6%)** have a formatting frame. This passes
the prospective 20% cost gate. Method-inclusive counts overlap: 77 include
FormatInterpolatedStringPart, 63 FormatInterpolatedStringValue, 30 padding,
9 numeric formatting and 7 spec parsing. **Do not add these counts or describe
28.6% as removable time.** Sixty-three guest leaves are unknown. No named string
decode frame survives in these samples; inlining and shared numeric/string paths
prevent a measured string-only fraction. There is one malformed map tail and no
ambiguous managed addresses among the exported frames. Three declaring-assembly
classification controls pass; parameter types do not determine assembly ownership.

Current source nevertheless confirms the remaining string pipeline: specified
width/precision calls AsString, truncates/pads CLR strings and re-encodes the
result into PyString. Default f-string conversion and format-field parsing already
have native paths and are not this proposal. Select one coherent trial of native
UTF-8 string precision/padding shared by f-strings, str.format and format(),
preserving the existing spec parser, numeric/date/custom hooks, admission and
current-token checks. This is a source-backed hypothesis, not a predicted gain.

## Direct phase diagnostics

Eight observations per lane after 32 warmups, separate from the CPU sample:

| Direct clock | Median |
| --- | ---: |
| Public warm execution/result disposal | 9.510 ms |
| Public fresh compile/run/compiled-owner disposal | 10.404 ms |
| Compilation nested in each fresh observation | 467.8 µs |
| Host calls nested in each public warm observation | 168.7 µs |
| Reflected context setup | 62.4 µs |
| Reflected execution, including host work | 8.038 ms |
| Reflected resource / execution-owner / result cleanup | 0.50 / 0.35 / 0.30 µs |

These diagnostic clocks exclude fixture reset and golden verification, include
decorator/reflection/stopwatch overhead where applicable, and are not additive.
Do not subtract medians to derive I/O or compilation overhead. Public process
allocations and reflected current-thread allocations have different coverage.
Startup and suspended I/O are not measured. The maintained external comparison
table remains revision-specific; this capture does not replace it.

[Frozen question/gates](pre-collection-plan.md), [exact loaded identities](identity.json),
[cost assessment](cost-assessment.json), [phase boundaries](phase-summary.json),
[offline analyzer](analyze.py), [resolved chains](resolved-chains.json.gz),
[checks and exact workload source](validation-logs.tar.gz),
[raw capture, maps, collector/driver and normal cleanup](evidence.tar.gz).
Raw SHA256: `bb1c28a3ef5e28453b77715f87f6cb1695dca4b0076754bd9a3495003fd2a423`.
An archival directory-mtime warning was corrected by writing the archive outside
its input directory; the capture and guest work were not repeated.
