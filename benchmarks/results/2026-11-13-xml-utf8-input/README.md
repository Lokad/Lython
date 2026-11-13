# Bounded UTF8 XML parser input

2026-11-13. Retain frozen316142ab against qualified R326/frozen660a0338.
ElementTree now borrows its funded UTF8 source through a bounded managed
TextReader, removing the required whole-source UTF16 cache request. XmlReader,
graph construction, conservative parser admission and aliases remain. UTF16
declaration validation can still materialize text; this is not a universal
conversion removal. No new pool, memory policy or public option is introduced.

The complete script reads221,107B of UTF8 XML containing1,024records, sums
attributes, writes a Unicode report and prints its summary. Isolated local
CPython3.13.2 and independent arithmetic/byte goldens agree; this is semantic
validation, not a new CPython timing comparison. Search is the unchanged control.

## Separate allocation and CPU observations

Local Release.NET11 process-wide allocated-byte deltas, two fresh processes per
source in reverse order, five64-invocation blocks after warmup:

| Complete invocation | Reference | Candidate | Allocated-byte change |
| --- | ---: | ---: | ---: |
| XML summary |3,564,288–3,564,408B|3,187,518–3,187,629B|−10.57%|
| Search control |147,256–147,267B|147,267B|0..+0.0074%|

The unchanged public-API allocation tool includes host I/O, reset, stdout/file
checks and result cleanup. Compilation/startup/final compiled-owner cleanup are
outside the deltas; ownership balances are checked after every execution and
return to zero after compiled-owner disposal. No forced GC, live-heap or tail
latency claim. Its original manifest does not supply worker fixture globals.

B697 on the quiet F4as_v7 Ubuntu VM, ordinary.NET11 RC3 workstation GC/tiering,
same fixed B685 worker and optional0.4adapter binaries for both sources:

| Complete warm script | Reference elapsed | Candidate elapsed | Elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: | ---: |
| XML summary |2.023–2.090ms|1.814–1.869ms|−13.22..−7.63%|−13.33..−5.23%|
| Search control |257.4–263.0µs|257.8–263.5µs|−2.01..+2.36%|−1.54..+2.03%|

Two fresh processes per source/job,12-second full-request wall warmup budgets,
five rotating one-second wall-sized batches. Warm clocks include mediated RAM-file
I/O, exact stdout/file checking and prior-result cleanup. They exclude compilation,
startup, fixture reset/protocol and final cache teardown. Wider process CPU includes
reset/protocol. Ranges pair process medians, not confidence intervals. XML process
spreads are3.02%candidate/3.22%reference; search spreads about2.18%each.

The source-specific CPU direction supports the removed conversion; magnitude
remains diagnostic. All identity, golden, quietness, join and archive gates pass
in149.518seconds. Observations stay in persistent storage separately from the
temporary RAM fixtures. Actual users finish, fixtures are removed/unmounted, then
the persistent archive is SHA-verified. No whole-runtime or embedding-tail claim.

## Original failed conditions and qualification

B696 failed before its first worker Ready because the new fixture manifest
omitted required size metadata. No timings were collected; all users joined.
[Original failure](b696/observations.tgz), terminal and failure receipt survive.
The corrected manifest preserves guest sources, stdout and input/output bytes;
the same frozen runtimes are reused. Both actual CLI workers pass local
Ready/Equivalent/Closed before B697. Two incorrect parent fixture-directory
assertions during local preflight are documented separately, without fabricating
raw process streams or inferring performance.

Focused Release checks pass on each actual.NET11/10 target:23internal and43public
cases, with sync/async public execution, encodings/errors, mixed content, graph
aliases, depth/DTD protection, input scalar boundaries, terminal refusal and budget
reuse. Deterministic cancellation is triggered by actual decoded XML text during
one long parser token. Full platform/stress/installed-package qualification is
pending. R320 traversal/clear/iterparse remains separate; publication is held.

[Source/check/allocation evidence](source-checks.tar.xz),
[allocation summary](allocation-summary.json), [timing summary](summary.json),
[B697 raw observations](b697/observations.tgz) preserve exact source/binary hashes,
577production inputs, collector commands and original conditions. The unchanged
allocation tool's source is in the earlier
[word-buffer evidence](../2026-11-13-word-buffer-loan/source-checks.tar.xz).
