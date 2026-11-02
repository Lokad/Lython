# Ordinary helper styles — B128/B129 source gate

2026-11-02; exact7da7335a/corea3a53e2e in every image. This compares equivalent
source styles, not runtime revisions or CPython. Only named intermediate variables
differ. Full source/fixture/stdout/file hashes are retained. No runtime prototype
or speedup is claimed by this experiment.

| Complete job | Named-source elapsed change | Process CPU change | Unchanged compact elapsed / CPU |
| --- | ---: | ---: | ---: |
| CSV, warm |+5.48%|+6.02%|−0.37% /−1.11%|
| CSV, fresh compile-to-result |+7.04%|−2.99%|+1.22% /+6.82%|
| Text, warm |+13.51%|+17.97%|−0.12% /−2.13%|
| Text, fresh compile-to-result |+8.09%|−7.81%|−3.81% /−20.73%|

Ordinary naming adds roughly6% complete CSV elapsed and12–14% text versus the
compact-source controls. Fresh CPU controls are unstable; do not claim a CPU
fraction or optimization from those clocks. Warm source-shape costs justify
addressing construction/lowering before a speculative frame/arena rewrite.

Separate complete-job allocation diagnostic, same core and complete goldens:

| Warm complete job | Compact bytes/run | Named source | Change | Unchanged compact control |
| --- | ---: | ---: | ---: | ---: |
| CSV |5,905,653|8,494,246|+43.83%|−0.0170%|
| Text |3,420,460|5,239,171|+53.17%|+0.0006%|

Static census confirms the compact helpers use native expression plans; naming
adds adjacent local store/load pairs and selects the general executable engine.
This remains executable supported Python, not lowered interpretation. Current
binding already targets final locals; argument copies are not the missing work.
Pure range's loop repeats8 instructions/two jumps. Operand storage is separate,
but merging buffers alone could lose existing tiny bound-array pooling and would
not remove value transport. Earlier slower arena/control designs remain relevant.

Selected next correction: let the existing native proof forward adjacent local
store/load values without changing ordinary executable instructions. Whole native
proof must still exclude observable bindings, callbacks and reuse. No new statement
VM, opcode, runtime scratch, owner or option. Dynamic inspection keeps original
bindings when proof fails. Runtime guard/evaluator stay shared and unchanged.
This is a separate candidate and measurement, not an inference of speedup here.

Quiet F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Four-second warmup/image,
nine interleaved ~200ms batches, independent current-core processes and compact
source control. Warm excludes compilation/startup; fresh compiles each invocation.
Fixture reset/staging, actual mediated I/O, full stdout/files and previous-result
cleanup included. Last result/program/process/fixture teardown outside batches and
joined. CPU is external Linux TGID user+system through response, not guest-only.

Timing76.643s/allocation25.713s. Allocation uses192 warmups/image and9x32 jobs,
isolated images sharing CoreLib and precise process-wide counting; its clocks are
diagnostic. All identities, goldens, quiet gates, reusable/final zero balances and
joined cleanup pass. Both owned services exit0/inactive; shared lease free.

The read-only source gate also inspected actual local/VM managed File.WriteAllBytes
IL: OpenHandle receives zero preallocation. There is no basis for the suspected
asymmetric fallocate fixture correction. Waiting and B125 search instability remain
unresolved. Raw census, IL, collector scripts and adverse results are retained;
no extra I/O retiming or broad profiling campaign followed that falsified lead.
