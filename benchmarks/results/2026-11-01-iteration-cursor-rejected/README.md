# Rejected shared iteration-cursor prototype (B35)

**Rejected and removed.** Runtime source is restored byte-for-byte. The fixed
comparison fails both its control-validity and material-improvement criteria.
No second collection, tuning, alternate iteration engine or cursor fields remain.

Baseline runtime **ae22392d**, unchanged at **4cca2041**; candidate is 4cca2041
plus [source.patch](source.patch.gz). Measurements are dated 2026-11-01 on the
dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, pinned
.NET 11 RC3. These are historical old/new diagnostics, not current CPython ratios.

## Hypothesis and deletion

The [B34 allocation capture](../2026-11-01-current-allocation-origins/README.md)
shows about 62.4 MB of sampled text allocation in CheckedCollection,
CheckedEnumerator and the native list yield enumerator together. That category
is about 8.85% of guest bytes; not all its state can disappear.

Synchronous executable GetIter currently uses that wrapper chain. Async execution
already uses PyIteration.Cursor. The prototype routes synchronous GetIter and
CheckedSequence.GetEnumerator through the existing cursor, deletes the separate
60-line CheckedEnumerator, and directly indexes exact PyList/PyTuple/object[]
sources. Reads follow the current list owner and count, preserving backing-store
replacement and live mutation. Native sequence exhaustion is final. Guest
protocols, retained generators, native upstream cleanup and awaited async disposal
retain their routes; cancellation may prevent native acquisition earlier.

No pool, dispatcher framework, policy, representation change or memory-management
infrastructure is added. The source replacement's value depends on complete-script
acceptance, rather than simply removing a class or reducing object counts.

## Frozen comparison

[Pre-code criteria](pre-code-plan.md) require at least 5% lower text CPU **or**
allocation against **both** before and identical-before controls, with the other
metric at most 2% worse. CSV may regress at most 5% CPU / 2% allocation. Identical
baseline CPU drift must be at most 5%, allocation drift at most 0.2%, and every CPU
sample must exceed 100 ms. No favorable retiming after invalidity or rejection.

The existing complete-job diagnostic performs four warmups then seven batches of
128 complete scripts per process. Warm public synchronous execution includes real
file work, fixture reset, complete stdout/file goldens and declared result cleanup;
compilation, process startup and transport are excluded. Ordinary GC/tiering and
common reflection overhead remain. Six processes consume **47.847 seconds**;
the entire owned collection is **49.246 seconds**. Every process is below its
30-second deadline; the aggregate is below 90 seconds.

| Complete script / metric | Before | Candidate | Identical-before control |
| --- | ---: | ---: | ---: |
| Text CPU ms/job | 6.30248 | 6.46491 | 6.65143 |
| Text allocated B/job | 5,518,406.875 | 5,321,945.375 | 5,518,646.938 |
| Text wall ms/job | 7.03711 | 6.94487 | 6.92370 |
| CSV CPU ms/job | 7.78692 | 8.00103 | 7.91689 |
| CSV allocated B/job | 9,542,639.625 | 9,378,766.063 | 9,542,231.313 |
| CSV wall ms/job | 8.42148 | 8.60487 | 8.56838 |

Text CPU is **+2.58% / -2.80%** against the two baselines; text allocation is
**-3.560% / -3.564%**. Neither metric meets the 5% target. Identical text CPU
also drifts **+5.537%**, exceeding the predeclared validity bound. CSV CPU is
+2.75% / +1.06%, with allocation -1.72% / -1.71%. All CPU samples exceed 100 ms,
and allocation controls remain within 0.005%; these do not rescue CPU validity.
All wall-time observations and adverse rows remain visible.

Both quiet-machine gates, all complete goldens and allowance balances pass.
The service is inactive, every worker joined, and the VM lease is available.
The SSH wrapper's final log-print command failed on a trailing carriage return;
the saved successful collection, inactive service and released lease were read
afterward. No worker was restarted or observation repeated.

## Validation and retained coverage

Candidate Release checks pass **172 native / 427 public per actual .NET 11 RC3
and 10.0.8 target**. They include mutation, protocols, cancellation, native
abandonment, genuine host suspension, refusal, retained results and budget reuse.
The new fixture initially used unsupported builtin-class inheritance; it was
corrected to an ordinary user-defined iteration protocol before measurement.
That initial fixture failure is preserved alongside the passing focused logs.

One independent public regression remains: replacement across list storage
growth, exhaustion after append, mutation before first pull, tuples, user
iteration and retained generator break/resume/close, through both APIs with a
finite shared allowance returning to zero. Its stdout matches isolated CPython
3.13.2. It passes the restored runtime on both targets. Four existing cancellation
cases pass per target after removing their redundant exact pull-count assertion;
the bounded interruption and deterministic cleanup assertions remain.

The subsequent nonincremental rebuild below verifies restored Release outputs
with zero warnings/errors. No public API or runtime contract changes. Full
unchanged platform/package qualification and repacking are not repeated for a
discarded prototype.

**Restoration correction from B36:** the incremental local "restored" build
and checks above still used B35's candidate binary, because copying source back
preserved old timestamps. The frozen B35 performance comparison itself used the
intended before/candidate binaries, so its rejection is unchanged. The subsequent
[nonincremental rebuild and binary audit](../2026-11-01-sigma-on-demand-inconclusive/README.md#corrected-restoration-and-useful-coverage)
verify the actual restored runtime and rerun the new semantic plus bounded-
cancellation cases on both targets. Fresh-checkout checkpoint 41273799899 is also
terminal green on Windows/Ubuntu. Original local logs remain as superseded evidence.

[acceptance.json](acceptance.json) retains exact decisions and raw archive hash.
[raw-results.tar.gz](raw-results.tar.gz) retains every fixed batch, loaded binary
identity, catalog, goldens, machine gates, drivers and owning-process records.
Readable driver/source, byte-identical compressed patch, original criteria and focused/restored logs
accompany it. Stop this prototype; do not reintroduce its pieces as rescue tweaks.
