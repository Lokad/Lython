# Final native-read / CPython checkpoint — 2026-11-06

Final production source **4ace68ff**, frozen core **a45b5c5d**, unchanged worker
b94fa98c/protocol2; Q02 reference2adf73f3/core3eed50ec. The core was built before
the source commit (ff45fafe assembly metadata); exact final source hashes and
focused-tested bytes are recorded in [identity](identity.json). R06 is excluded.

B338 completed **381.56 seconds /14jobs**, with two independent identical current
processes, Q02 reference and CPython3.13.16. Ordinary .NET11RC3/CPythonGC/tiering,
five-second warmup per role/job, seven rotated150ms rounds; quiet endpoints and
every source/fixture/output/file/Closed/funding/join/fixture-removal gate pass.
[Raw archive](observations.tgz), [verified receipt](archive-receipt.json),
[all medians/IQRs](summary.json), [paired assessment](assessment.json).

| Whole script / boundary | Current candidate / identical control | Q02 reference | Paired change |
| --- | ---: | ---: | ---: |
| Text, synchronous warm |2.293 /2.302ms|2.293ms|+0.005 /+0.43%|
| Text, synchronous compile-to-result |2.452 /2.467ms|2.412ms|+1.63 /+2.27%; adverse debt|
| Text, completed-I/O async warm |2.630 /2.636ms|2.832ms|−7.14 /−6.91%|
| CSV, synchronous warm |4.768 /4.759ms|4.781ms|−0.27 /−0.45%; small/control-limited|
| CSV, synchronous compile-to-result |4.888 /4.917ms|4.863ms|+0.51 /+1.11%; small adverse|
| CSV, completed-I/O async warm |5.670 /5.586ms|5.677ms|−0.12 /−1.60%; inconclusive|

Report has4.18–5.10% current-role IQR and remains variable. Warm JSON current
roles differ1.42%; candidate+1.62% versusQ02, control+0.18%. Fresh JSON−2.02/−0.61%.
These do not establish a broad CPU improvement. Compile-only, warm and fresh
CPython comparisons are maintained in the [current guide](../../../docs/BENCHMARKS.md).
Ranges there are role medians, not confidence intervals. Preserve every row.

**Decision:** retain the shared native read proof for the removed transport and
confirmed affected async-text elapsed improvement. B336's synchronous warm
text+1.51–2.11% does not repeat here; its original conditions remain intact.
Fresh-text+1.63–2.27% remains explicit regression debt, not explained away by a
different API's gain. No allocation, causal CPU, RSS, startup, pending-I/O or
embedding-latency claim. Performance objective remains open.

Clocks include execution, mediated RAM I/O, complete validation and prior-result
cleanup; fresh also compilation/disposal. Compile-only excludes execution/I/O.
Startup, fixture staging/reset, protocol transport and final teardown are outside.
CPython retains imported modules; Lython isolates execution imports. Actual pending
I/O, cancellation and retained-alias lifetimes are separately tested.

## Failed conditions and setup correction

B337 first encountered ENOSPC during extraction before any service started.
Bounded housekeeping removed only hash-verified duplicate owned transfer archives
with retained local copies and terminal owners. Source images, dependencies and
sole evidence were preserved. [Original headroom recovery](b337-disk-headroom-recovery.json),
[verified extraction recovery](setup-recovery.json). Disk growth cause is unknown.

Its subsequent collection stopped before the first timing at the loaded-core
identity assertion: .NET resolved the symlinked main worker beside B336's old core,
despite the new directory's core hash being correct. All users joined/fixtures
removed. [Original failed archive](b337-failed-observations.tgz),
[hash receipt](b337-failed-archive-receipt.json),
[preservation/Ready diagnostic](b337-preserve-failure.py).

B338 copies only the unchanged main worker assembly and host manifests; other
dependencies remain shared. A separate Ready/Closed check verifies the actually
loaded a45b5c5d before launch. [Correction](startup-path-correction.json).
The old combined extract/setup launcher then refused its already prepared folder
before starting a service; [standalone launch correction](launch-correction.txt)
and [sole service launcher](start-corrected.py) preserve that setup error. The one
corrected460s-owned collection completed. No runtime admission retry, source
optimization or measurement row was involved in either failed setup.

R07's [cached delivery](../2026-11-06-native-read-prefix/delivery-ci.json) is GREEN
for Windows/Ubuntu, both runtimes, original Stress and installed consumers. Never
query that terminal run again. No live VM job remains; publication stays postponed.
