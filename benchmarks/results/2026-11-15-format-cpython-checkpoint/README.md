# Method-report CPython checkpoint

2026-11-15, **b06b8593 / frozen8afb146e** against stock **CPython3.13.16**.
Full runtime qualification is active at capture. The existing report's method
variant and f-string control share 1,024 records / 189,441B JSON, report layout
and complete stdout/file goldens. Diagnostic external comparison, not a causal
comparison with earlier Lython sources.

| Complete report / phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | --- |
| Format method / warm | 5.310–5.372ms | 2.597–2.606ms | 2.04–2.07× |
| Format method / compilation | 0.037–0.038ms | 0.036–0.036ms | 1.03–1.05× |
| Format method / compile-to-result | 5.488–5.513ms | 2.742–2.782ms | 1.97–2.01× |
| F-string control / warm | 2.723–2.833ms | 2.334–2.353ms | 1.16–1.21× |

Ratio above one means slower. Compilation is near parity; the remaining method
gap is in execution. **Fresh CPython IQR8.86%** limits precise magnitude, while
f-string Lython controls move3.88%. All distributions/controls are retained in the
[summary](summary.json); neither is silently dropped or called neutral. The method/
f-string contrast includes different argument evaluation/binding/parsing and does
not isolate one removable share. No unchanged confirmation is needed to establish
a material execution gap; a concrete redesign needs its own source pair.

**B810 succeeds138.629s** on quiet F4as_v7 Ubuntu24.04.4, four EPYC9V45vCPUs,
16GiB/no swap,.NET11RC3 workstation GC/normal tiering, CPython ordinary GIL/GC.
Two independent processes/engine,5s full-request warmup, five rotated ~0.5s batches.
Warm includes synchronous Run, mediated RAM I/O, complete stdout/file checks and
prior-result cleanup; excludes compilation/startup, fixture reset/protocol and final
cache teardown. Compilation includes prior compiled-owner disposal; fresh includes
compilation, one execution and its compiled-owner cleanup. Wider process CPU includes
reset/protocol. Phase medians are not additive. CPython uses native buffered files
without equivalent host capability/admission checks. No pending-I/O latency, RSS,
contention, startup or embedding-tail claim.

Complete images/catalogs, goldens, budget reuse, quiet before/after and Closed/exit/
join gates pass. Evidence is archived and verified persistently under the lease;
no users/RAM fixtures remain. Terminal cached forever. Staging retires only exact
committed B809 working replicas after hash/path/UID/link/all-alias proof, preserving
current images/toolchains, canonical evidence and254/252MiB floors.

[Raw](observations.tgz),[identity](identity.json),
[procedure/inputs](procedure-and-inputs.tar.xz),[previous guide](prior-BENCHMARKS.md),
[separate source-pair/semantic/ownership evidence](../2026-11-15-formatting-final-sink/README.md).
