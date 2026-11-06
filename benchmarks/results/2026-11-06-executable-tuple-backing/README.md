# Executable tuple final backing — 2026-11-06

Retain R04: executable tuple literals now transfer their private admitted array
through the existing FromReservedOwnedArray helper. Remove the second backing
copy/admission/construction overlap and stale preflight7249 local. Final capacity
and conservative scalar headroom are funded before publishing the tuple and
retiring input slots. The private array is never executable workspace and is
never reused. Borrowed-array/enumerable constructors still copy their input.
No new factory, representation, cache, ledger or owner.

Source1863cb3e plus [recorded source hash/patch](identity.json), candidate4011fcad
versus R02/frozen867ebf7a. Both Release images built before the R04 commit;
source snapshots in [raw evidence](observations.tgz). [Contract](contract.md),
[validation](validation.json), [check history](check-history.tgz).

Existing tuple factory/display failure, scalar/key identities, operand retirement,
real cancellation/refusal, retained ownership and shared funding tests cover the
changed path/helper. Focused120 internal/272 public per actual.NET11RC3/.NET10.0.8
pass; full local4,215 internal/16,103 public pass on each actual runtime. No new
tests mirror the trivial transfer implementation. Final platform/stress/installed
consumer qualification batches R02/R04; Q01's cached GREEN receipt is separate.

## B330 complete scripts

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16, unchanged b94 worker/protocol2,
normalGC/tiering, five guest warmup seconds/role and seven rotated150ms rounds.
Two identical candidate roles, R02 reference and CPython.85.83s/three complete
jobs; full goldens, quiet endpoints, Closed/final funding, joins and fixture
cleanup pass. [Raw evidence](observations.tgz), [summary](summary.json).

| Warm script | Candidate / identical control | R02 reference | Changes versus R02 |
| --- | ---: | ---: | ---: |
| Complete compound-key aggregation, width2 |3.084 /3.106 ms|3.310 ms|-6.82% /-6.17%|
| Complete compound-key aggregation, width8 |5.307 /5.357 ms|6.051 ms|-12.29% /-11.48%|
| Primary CSV summary |4.783 /4.648 ms|4.751 ms|+0.67% /-2.16%, inconclusive|

Affected compound-key identical-role spread below0.94%, Lython IQR at most1.78%.
The copy removal and measured affected-script gains support retention. CSV's
2.81% identical-role spread prevents a source attribution; accept that uncertainty
explicitly and retain both outcomes. No automatic retiming or permanent closure;
the next coherent primary checkpoint can revisit it.

Source proves one private backing copy removed per nonempty executable tuple.
No process allocation percentage, guest-only CPU, RSS or embedding-latency gain
was measured. Do not add these deltas to Q01 or R02's different source pairs.
Width2/8 remain6.77x/5.72x CPython in this diagnostic comparison; CPython width8
IQR4.90% is explicit. No broad speedup claim.

Warm clocks include execution, mediated RAM I/O where present, full output/file
validation and prior-result cleanup. Compilation/startup/staging/reset/transport/
final teardown excluded. These affected warm jobs do not update the primary
four-script compile/warm/fresh table. Coherent refresh and final qualification
remain separate. Complete-script work continues; publication/tagging postponed.
