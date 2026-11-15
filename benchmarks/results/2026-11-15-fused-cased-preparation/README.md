# Fused cased preparation — initial coherent prototype

R408/B794,2026-11-15, **4e49a5ed** versus fully qualified **43bce13a/9e890d18**.
One scanner cases directly into existing cursor-owned scratch and records final
byte ranges; it removes deferred per-word casing/source retention. It retains
ranges and avoids R343's extra complete-buffer scans. One conservative scratch
window reserves source byte count × maximum mapped scalar width (lower4/upper
maximum), including replacement overlap; independent eventual words are admitted
before loop effects. Scratch can retain several times output/deny earlier.
No new pool, owner subsystem, cap, controller or public option. **Not retained.**

| Complete workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / warm | −3.40..+6.81% | −4.63..+7.45% |
| Captured text / compile-to-result | −7.06..+0.37% | −7.94..−1.96% |
| Direct text / warm | −2.33..+2.11% | −0.78..+0.52% |
| Fulfillment control / warm | −0.56..+0.28% | 0.00..+2.42% |
| JSON control / warm | +2.53..+5.79% | +2.17..+7.96% |

Every independent process-median pairing remains. Captured candidate spreads
9.00%warm/6.18%fresh; direct reference3.14%; JSON reference2.72%,candidate0.44%.
Text is uncertain, fresh CPU favorable and JSON adverse across all pairings;
do not discard a process or call controls neutral. Separate existing four-job
Windows allocation comparison is effectively flat (+152B/+0.034%captured text).

Five jobs finish259.624s on the quiet established F4as_v7 Ubuntu24.04.4/fourEPYC9V45/
.NET11RC3 workstation GC/normal tiering VM. Two processes/source, five rotated0.5s
batches,12sfull-request text warmup/5scontrols. Warm includes synchronous Run,
mediated RAM I/O, full stdout/file goldens and prior-result cleanup; fresh also
includes compilation and compiled-owner disposal. Startup/reset/protocol/final
cache teardown are excluded; wider CPU includes reset/protocol. No new CPython,
heap/RSS, pending-I/O, contention or embedding-tail comparison.

Focused69internal/268public per actual11/10 include3original64MiB rollback cases;
another127internal/171public per target covers shared casing/builder behavior.
Original three ownership-fixture failures/source and their correction remain.
Storage reporting now includes scratch, failed growth is released while earlier
cache capacity can remain, and the cleanup assertion captures total capacity
before disposal without relaxing its exact balance. Initial chunked-write source
and core also remain; no timing is attributed to that unmeasured first shape.

**Selected bounded refinement:** restore the general byte-builder path. Its R408
context/span arguments and growth checks change unrelated users, while the cursor
requires replacement only before a line starts and needs no previous-byte copy.
Use ordinary coarse preflight on that existing cursor, replacing only its private
byte capacity with overlap, no new owner or allocator abstraction. This removes
an incidental builder object and global helper changes; it is not proof of the
JSON cause. Preserve this exact source/data and compare the changed source against
the same9e baseline with all controls before deciding retention/regression debt.

All identity/golden/budget/quiet/Closed/exit/join/archive gates pass. Original ff2
historical image is retired only after exact committed binary/member proof and
all six hard-link aliases/path/UID/hash/lease checks; current images and canonical
evidence stay.254/252MiB floors remain. Every user is joined and fixture removed;
B794 is terminal cached, never requery. No full qualification/publication yet.

[All controls](summary.json),[allocation](allocation-summary.json),[exact source/images](identity.json),
[raw observations](observations.tgz),[cleanup](archive-receipt.json),
[source/core/checks/procedures and failed conditions](sources-checks-and-procedures.tar.xz).
