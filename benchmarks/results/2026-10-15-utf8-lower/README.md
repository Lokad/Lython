# Direct UTF-8 lowercase output

Ordinary `str.lower()` scalars now use managed `Rune.ToLowerInvariant` and encode
directly into the existing governed byte builder. This removes each ordinary
scalar's temporary CLR string, string casing and short-string re-encoding.
Dotted-I retains its full expansion; final sigma keeps the same contextual
lookahead and property tables. No new Unicode database, pool, cache or builder.
Upper/title/capitalize/swapcase retain their current mapping paths.

The scalar append admits the required UTF-8 capacity before writing, using the
existing replacement-overlap checks. Input/result owners, final output admission,
scratch cleanup and periodic cooperative checks are unchanged. All code remains
managed and memory-safe.

| Complete text-summary file job, warm synchronous execution | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 3,072 lines / 35,840 UTF-8 input bytes / 6,144 words | 7.107 ms | 6.553 ms | 1.231 ms |

The observed 7.8% lower median is close to baseline IQR-to-median 7.5%; candidate
spread is 3.3%, CPython 1.6%. **Timing remains inconclusive**, with no qualified
speedup, confidence interval or current overall multiplier. Retain the measured
allocation removal without attributing all job cost to casing. One
[diagnostic](warm.json) preserves seven rotating batches, calibration and warmup.
No retry, retuning or replacement samples. The owned command took 8.571 seconds
including cleanup, under a 30-second bound.

The complete [ordinary Python script](../../scripts/text-summary.py) opens a
UTF-8 file, lowercases/splits lines, counts words in a dictionary, sorts keys and
writes a report. Both worker timers include matched disk input/output staging,
fresh invocation state, execution/projection, exact file-byte checks and prior
result cleanup. Compilation, process startup, transport and full response hashing
are outside timing. This is warm complete-job execution, not compile-to-result.
Host operations complete synchronously for timing; yielded-host correctness is
checked separately. The comparison host supplies efficient file overrides.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GC/GIL, `-I -S -X utf8` and explicit UTF-8 protocol I/O. Lython's ordinary
policies, shared allowance and GC/tiering remain. CPython has no equivalent
in-process containment. Independent local goldens use CPython 3.13.2.

Baseline is d083c8cfdc5d836e8931d050ec9a1cf3faf81491; the working candidate adds
only the two production files frozen in [the declaration](comparison-declaration.json).
The benchmark catalog, execution tooling, Python worker and script are unchanged.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent stdout/report-byte goldens at all three existing sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies seven worker processes
and their file fixtures exited/cleaned, exact source differences, identical tooling,
inactive services and machine-lease availability.

Focused checks preserve all sigma property boundaries/gaps and ASCII contexts,
Unicode width growth/shrinkage, dotted-I, supplementary scalars, embedded NUL and
empty input. [Independent Python goldens](cpython-goldens.json) match four added
boundary witnesses. Two short allocation checks bound ASCII/mixed-UTF-8 lowering
to input-sized scratch/output plus fixed overhead, detecting the former
per-scalar CLR strings. Fuel/cancellation failures preserve spans and input while
returning scratch allowance; repeated denial also covers growing UTF-8 scalars.

All 2,221 ordinary white-box tests pass in 11.573 seconds including cleanup.
Focused groups pass 28 white-box, 189 public Unicode/representative/ownership and
231 public governed/string-lifetime/CSV/text/JSON/file controls; groups overlap
and each takes under 30 seconds. [Validation](validation.json) retains receipts.
Original stress coverage is unchanged; no full stress or local full public rerun
is claimed. Package/platform qualification follows at the delivery boundary.

This closes ordinary lowercase scalar output only. Other casing families,
remaining JSON async/token work and aggregate/scalar/frontend changes remain open.
