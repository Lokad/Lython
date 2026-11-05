# Dictionary-view containment and relations — retained consolidation

2026-11-05, baseline `353b28b1`/frozen `48f4690c`; final view core `f2078b20`.
[Verified source and focused checks](validation.json): zero-warning/error Release
build,372 public/132 internal focused tests per actual .NET11/10. These changes
need their own full delivery qualification. The preceding Counter revision is
independently GREEN/all8; its result does not qualify this later source.

One count/all-contained rule replaces24 duplicated native/ChainMap view relation
bodies and the separate raw equality HashSet. Dictionary key/item membership uses
dictionary lookup instead of two linear scan helpers. Items-to-items comparisons
use stored pairs directly, allocating no item tuples or comparison sets. Values
may be unhashable; only item keys need hashing. Ordinary relations and explicit
methods preserve Python's NotImplemented/reflected-dispatch distinction.

Current dictionary lookup and value comparisons can genuinely suspend; enumerator
callbacks establish current provenance only while advancing, never across await.
Source backing remains conservatively funded through callbacks/awaited cleanup,
including after clear. Extra allowance can be required even without mutation.
Protocol-key callbacks and set-membership callbacks retain their existing
synchronous-completion boundaries. No new backend, policy or ownership mechanism.

## Bounded source pair

Same dedicated Azure Standard_F4as_v7, Ubuntu24.04.4, four exposed AMD EPYC9V45
CPUs, .NET11 RC3 and CPython3.13.16 GIL release. Ordinary tiering/GC; existing
frozen worker `b94fa98c`/protocol2. Five seconds warmup per role/job, seven
rotated150ms batches, separate candidate/current-control/prior-Lython/CPython
processes. Initial B30359.07s and final B30459.02s pass quiet/source/golden/file
validation, successful worker joins, final-zero and fixture-cleanup gates.

| Final warm workload | Prior Lython | Final Lython | Current control | CPython | Candidate/control versus prior |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native dictionary-view validation,256 keys,8 helper iterations |2.271834ms|0.450433ms|0.470636ms|0.078224ms|−80.17%/−79.28%|
| Complete text helper summary,3,072 lines |2.235475ms|2.250379ms|2.207021ms|1.190489ms|+0.67%/−1.27%|

The affected script still takes5.76× CPython. Current-role spread4.49%, candidate/
control/prior IQR1.73%/0.86%/1.60%; this variability does not approach the measured
source-pair difference. Initial view reduction−80.68%/−81.05%, spread1.92%, is
preserved. Its source predates the necessary explicit-contains terminal check.

Complete text is inconclusive: initial−1.19%/+2.57%, current spread3.81%; final
+0.67%/−1.27%, spread1.93%. The unchanged reference also moves between blocks.
Retain the consolidation and affected-workload elapsed improvement; do not claim
a general whole-script CPU, allocation, compilation, startup, RSS or embedding-
latency improvement. No timing against corrected custom-callback/unhashable-value
baselines. This does not replace the representative fresh CPython checkpoint.

Warm clocks include invocation, mediated RAM I/O, full output/file validation and
prior-result cleanup. Compilation/startup/staging/reset/transport and final cached
program/result/process/fixture teardown are excluded. The view script constructs
native dictionaries, checks keys/items equality through operators and an explicit
method, and performs key/item membership. Its prior output is independently valid.

[Initial raw observations](initial/observations.tgz), [final raw observations](final/observations.tgz),
[final summary](final/summary.json), [contract and discoveries](final/source-contract.md).
Archives retain exact source snapshots, identities, catalogs, workers, every round
and machine/service receipts; archive receipts record their hashes.

## Correctness and remaining work

[Correctness history](correctness-history.tgz) retains original four failures,
the first incorrect native-view/ChainMap NotImplemented answer, four swallowed-
refusal/cancellation failures and their final regressions. Nine isolatedCPython3.13.2
goldens cover shapes, list/dict values, native/custom keys, all relations, explicit
methods, reflected callback order, tuple families and source mutation. Sixteen
white-box cases include nine actual I/O suspensions, retained backing after clear,
native false-result cancellation/refusal and refusal before value effects. They
check awaited cleanup, structural/frame unwind and returned shared allowance.

E3b2 remains separate: mapping rest retains an equal custom key, dynamic duplicates
are accepted, too-short subjects evaluate keys, and key extraction interleaves with
value-pattern evaluation. The original and phase-order probes are preserved.
Fixing these requires coherent extraction phases and funded captured references,
not changing the raw comparer alone. Generated dataclass storage coupling and
public compound hashes also remain open. Publication stays postponed.
