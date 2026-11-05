# D43 current whole-script CPU gate

2026-11-05, before capture. Committed7dcc3b48, frozen finala4ce2b48 (built before
commit metadata), source/body hashes recorded. FullCI42014687629 runs independently;
4111internal/7360public per actual11/10 and final85target checks per target pass.
No production prototype. Publication held.

One <=45s owned diagnostic using existing perf/checked comparison worker: complete
text summary and ordinary-helper CSV, same frozen fixtures/full CPython goldens
as B267. NormalGC/tiering, only PerfMapEnabled instrumentation. Six warmup guest
seconds plus two settle seconds per case;1536 complete invocations/capture, about
800–1600guest CPU samples/case expected. It is a CPU map, not a timing/allocation
comparison or claim that an inclusive frame is removable overhead. No heap dump,
guest tracing, forcedGC, new production dependency or runtime instrumentation.

Require actual Ready/core/aux identity, independent full goldens before/after,
pre/post quiet gates, normal runtime, named active guest TIDs, perf completion,
Closed/exit0/final-zero reservations/joins, owned fixtures removed and free lease.
Preserve malformed/truncated maps, unknown symbols and collector conditions.
Frozen source/dependency paths remain immutable; verified hardlinks only if needed.

Stop at this capture/source gate: choose one coherent work/storage deletion with
affected full scripts, semantic/cancellation/ownership acceptance and bounded
comparison, or identify precise missing proof and select another actual boundary.
Do not infer universal tagged values from boxing, fusion from allocation share,
or revive slower call-arena/control records. Current calls already reuse shared
argument/local storage and native bodies; target dispatcher is now removed.
No broad measurement/ownership-audit campaign or automatic retiming.

## One bounded pre-capture correction

B268 failed its immediate pre-gate at6% CPU; no guest/perf samples, exit3.
Original failed-condition archivecf307e229e55ec922942e2bf89722f07152d9fcf0d187419e7b2d9d294a50ab5
and exact thresholds remain. B269 uses the same frozen source/worker/cases,
5s settling before both quiet gates;1536text/1024ordinary invocations keep the
owned capture within45s. Offline symbol export runs afterward under the exclusive
lease, after every worker/profiler is joined, with20s bound per analysis command.
No relaxed threshold, failed-design inference, new runtime policy or automatic
second correction. Preserve unknown/malformed maps and all initial conditions.
