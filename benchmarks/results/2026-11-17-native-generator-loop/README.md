# Share the existing native loop with buffered generators

R494/B912,2026-11-17 UTC. **Private prototype; retention pending.** Fully
[qualified33bd7efd/frozen5e7058a3](../2026-11-17-json-scalar-construction/delivery/README.md)
versus1894be6b on22d5a5f3. Seven production files generalize the existing smaller
native view to borrowed generator values/PC and existing admitted caches, cursor
retirement and disposal chain. Share yield publication and buffer consumption;
transfer prepared child frames once. No third opcode loop, expression/JIT backend,
call arena, new owner, public switch or independent resource policy.

Eligible buffered generators avoid repeated general instruction selection. Protected
or mirrored frames keep the ordinary engine. Closed builtin cursors prove that
pulling cannot invoke guest/async code; unknown cursors decline before any pull.
Genuine suspension resumes existing state without replay. Existing ordinary helper
call ABI stays intact; the larger native view is a possible cost to measure.

Actual Windows11/10 each627internal/1801public existing regressions pass after a
forced nonincremental build, plus7new public continuation cases. Those check
effects/aliases, guest faults, terminal refusal and cancellation after a partially
executed helper reaches real file I/O. Held cleanup keeps the result pending/file
open/storage charged until explicitly released; later executions restore allowance.
The first fixture left malformed input for its final successful reuse assertion:
two cases failed there, not in the main execution. Reset only that input; original
source/failure retained. No timeout/semantic/ownership assertion relaxed. Both
actual32file workers/catalog goldens are verified (unchanged reference reused after
all hashes match); candidate five scripts agree with CPython3.13.2.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Warm | **−6.04..−4.38%** | **−6.74..−5.26%** | 1.69/0.07% |
| Same | Compile and execute once | −0.15..+4.05% | −1.65..+4.94% | 0.79/**3.40**% |
| Original text normalization | Warm | **−7.32..−3.70%** | **−7.30..−2.07%** | 0.97/2.90% |
| JSON-lines helpers | Warm | −5.70..+2.74% | −7.52..+2.88% | **8.03**/0.86% |
| Positional-call control | Warm | −0.24..+14.25% | −1.17..+14.55% | 0.85/**13.56**% |

191.82seconds/five jobs, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3; six-second
rotated full-request warmups/five rounds/four processes, natural GC/tiering.
The candidate call-control disagreement is adverse evidence, not explained away
by tight within-process IQR≤1.15%. One unchanged-image/catalog confirmation of
fresh floating/helper JSON/call control with8second warmups is justified. No
promotion, broad CPU claim or permanent design verdict from unstable controls.

All four other allocation medians/peaks are identical; floating difference298B
is effectivelyflat, not an established saving/cost. All allowances restore.
Warm guest clocks include Run, mediated RAM I/O, stdout/file goldens and prior-result
cleanup; fresh adds compilation/compiled-owner disposal. Startup, fixture reset/
protocol/final cache teardown excluded; external CPU is wider. This source pair
does not refresh CPython performance ratios or explain the whole external gap.

All actual-image/golden/quiet/Closed/exit/join gates pass. RAM traces SHAarchived
persistently under lease before unmount. Only superseded d739/841 VM inode aliases
and exact committed raw JSON replicas retired with fresh path/UID/hash/link/all-alias
proof. Current qualified5e7/candidate1894, catalog/toolchains/canonicals remain.
Terminal cached; no unchanged correctness qualification replay. Publication held.

Read-only siblinga370f693 metadata/cursor facts also verified against committed
texts: UTF8 group-name enumeration, detached creation/preparation token lifetime
and separate per-match staging allowance remain outstanding. Recent text/scalar
removals do not close those gaps. No producer mutation, adapter pin change or
unchanged dev105 qualification; core/runtime work remains independent.

[Summary](summary.json),[allocation](allocation-summary.json),[source](candidate-sources.tar.xz),
[prototype](prototype.patch.gz),[identity](identity.json),[focused checks](checks.json),
[held cleanup](continuation-checks.json),[raw](first/observations.tgz),
[procedures](procedures-and-checks.tar.xz),[producer facts](regex-source-check.json).
The broader complete-script objective remains active.
