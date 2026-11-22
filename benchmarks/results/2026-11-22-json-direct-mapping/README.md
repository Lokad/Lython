# B1376 Bind single expanded JSON dictionaries directly

Compared frozen `927f0ca88` and `a3338b0a8` cores on November 22,
2026 UTC. The same patch is retained at `e4df38816` on top of the
separately qualified regex correction. Each frozen image includes
32 verified files. The core DLL is the only difference.

The four JSON module entry points can bind a single exact dictionary
straight to the final named slots. This replaces the argument
accumulator, the named binder's second pass and the heap UTF-16 name
for recognized options. A cursor reads the live mapping after the
fixed binding has been admitted. Extra constructor arguments go to
their own admitted final list in source order. Invalid mapping keys
precede duplicate or missing arguments. General mapping protocols
continue through ordinary expansion.

| Complete warm script | Elapsed change | Wider process CPU change | Local allocation change |
| --- | ---: | ---: | ---: |
| JSON Lines export, expanded options | −11.09 to −7.21% | −9.55 to −7.21% | −17.84% |
| JSON Lines export, explicit options | −1.14 to +0.45% | −1.52 to 0.00% | +0.02% |
| Guest function forwarding | −2.12 to +1.44% | −2.93 to +1.22% | 0.00% |
| Positional calls, unchanged control | +1.55 to +10.46% | +1.79 to +11.14% | 0.00% |

Ranges encompass every pairing of two process medians per source.
The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Both
JSON scripts transform 3,072 records from the same 189,441-byte
input. Warm includes mediated RAM-backed I/O, stdout and file
verification, and previous-result disposal. Wider CPU includes
worker reset and request handling. Compilation, process startup and
final teardown sit outside the elapsed clocks.

The expanded case allocates about 1.33 MB less per run locally and
reduces the admitted peak by 331,952 bytes. The other admitted peaks
match and all allowances return to baseline. The explicit and guest
controls have inconclusive elapsed changes. Preserve the positional
control's adverse CPU and elapsed results. Its candidate workers
vary by 7.95%, with 0.57% maximum within-worker IQR. B1375 had 7.26%
reference-worker spread on this same control. The workload never
calls the expanded-dictionary route. The observation gives no cause
for the difference and warrants a bounded repeat with reversed order
before release. Retain the demonstrated JSON improvement with that
debt open. The repeat reproduces a 2.87–4.07% elapsed increase
with stable workers. [Confirmation](../2026-11-22-json-positional-control/README.md).

Both actual images match CPython 3.13.2 on all four complete scripts.
Each platform passed 235 white-box and 957 public focused Release
checks. These exercise callback suspension, cancellation, terminal
refusal, host mutation before the cursor reads, alias identity,
invalid keys before argument errors, and Unicode custom keywords.
The first white-box compile caught a test helper parameter that was
typed too narrowly. Its output and the corrected tests are archived.

All 112 timed batches verified. The two quiet gates passed, all
workers joined, and the archive verified under the lease. Collection
took 223 seconds. The run reused two verified catalogs. Staging
retired only an older core's four aliases and two duplicate archives
whose exact bytes are preserved in Git.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Source identity](source.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz).
