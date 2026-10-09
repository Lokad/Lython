# Bound loop-name stores, 2026-10-09

Compiling simple loop targets to existing bound-name store instructions reduced
the 16,384-iteration integer loop's median time by **20.2–22.4%** in two short
comparisons. Collection took **6.95 / 6.91 seconds**. No full comparison lane ran.
These observations are diagnostic; milestone qualification remains pending.

The candidate is `28e612c4e13d013798f68768131f4b23dc0ca54d`; the baseline is
`9b6230378b263763238d14e45c8ba8d58594a022`. Both include the same independent
function-scope correctness fix. Their sole production difference is six added
compiler lines replacing generalized simple-name loop assignment with existing
local/global/closure stores. Module-loop reads can then use existing local slots.
Complex and suspending targets retain their previous paths; instruction spans,
checkpoints, numeric exactness, storage ownership and public containment remain.

This differs from the previously [rejected runtime shortcut](../2026-10-09-integer-addition/README.md):
that shortcut still used general name lookup/storage and showed no repeatable gain.
The consolidated baseline's separate CPU trace had 6.23% exclusive samples in the
loop-assignment helper and 5.29% in ResolveName, including inlined work. Those
sample shares do not predict or certify the measured improvement.

| Collection | Baseline Lython | Candidate Lython | CPython context | Median time reduction |
| --- | ---: | ---: | ---: | ---: |
| 1 | 2,814.203 µs | 2,183.901 µs | 598.428 µs | 22.4% |
| 2 | 2,700.855 µs | 2,154.101 µs | 577.923 µs | 20.2% |

IQR/median was **1.23% / 0.96% / 0.16%** for baseline/candidate/CPython in
collection 1 and **0.89% / 1.08% / 0.45%** in collection 2. Both collections
are retained. There are no idle/noise qualification gates, confidence intervals,
selected fastest retries or qualified Lython/CPython multipliers here.

The [short comparison contract](../../COMPARISON.md#short-improvement-rounds)
uses identical precompiled module source, fresh public invocations and complete
independent golden checks. Startup/transport stay outside worker timers; normal
budgets, GC, tiering and containment remain enabled. Workers warm for at least
one second, settle for two seconds, then take seven rotating/reversed-order
batches calibrated to at least 25 ms. Pinned tools are SDK 10.0.401/runtime
10.0.12 and optimized CPython 3.13.16 on the same four-core AMD EPYC VM.
Both services completed successfully with a configured 30-second whole-group
cap, then stopped; recorded worker PIDs were absent and input hashes unchanged.
All VM builds, tests and transfers finished before collection.

The independent correctness fix prevents an empty function loop from reading an
outer/global variable with the same name, and registers `UnboundLocalError` as a
`NameError` subtype. Function bindings apply throughout the function; uninitialized
free variables keep `NameError`. The original public probe returned outer `99`
where CPython raised `UnboundLocalError`; the corrected probe matches CPython,
including messages and deletion/free-variable cases. Existing compile-time
diagnostics for statically certain unbound reads remain unchanged.

All **9,013** local Release tests passed for that correctness fix. The compiler
candidate passed **755** focused local Release and **757** focused Debug checks;
the latter include two additional global/nonlocal rebinding regressions. Each frozen VM build
passed **518** focused Linux Release checks. Both-mode regressions cover empty
loops, namespace visibility, global/nonlocal writes, late-bound closures,
generators, comprehension isolation, class bodies and abrupt control flow.

Raw receipts, inputs, responses, source/loaded-assembly identities, service
configuration/journals, terminal audits, probes and tests are retained locally
under `.git/agent-notes/bound-loop-names-20261009/` and in corresponding VM folders.
Each receipt contains every attempted batch and every timed input/binary hash.

| Receipt | SHA-256 |
| --- | --- |
| `micro-bound-loop-28e612c4-large-1-20261009/micro.json` | `bda7294795ebf371d2d0b454650a05b07c594d665534859f3d3ce743faaf5259` |
| `micro-bound-loop-28e612c4-large-2-20261009/micro.json` | `dea09c2a41f1c24d8b6ba7100c2e1245c9265d862de323ee7435412a9172a985` |

The core-loop manifest SHA-256 is
`6f2a5f018909e56a1d7fc62cceec75f4803687a0413c86f7a17ffc9727cd2092`;
source `a07b1a8ef5b104e808d7cf79269d969544cff102ad5c89e8009f361b265d13fb`,
fixture `c1c16fa8a40dac165afc8c9915fab90012d9af6a9836b6a2e03812eb3f441155`,
and complete golden output
`c7939ac0ea0d28651f4d14f9dc0ebe2f3071fdac68b0a490b64651b863de6cb9`.
Later delivery documentation/tests do not change the timed production code.
