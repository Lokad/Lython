# Asynchronous hash/read boundary — 2026-11-06

Retain Q02 for usable asynchronous key protocols and consolidated ownership,
with completed-key async performance debt explicitly accepted at this milestone.
This is a capability/correctness change, not a synchronous CPython speedup.

Public hash, nested tuple/slice and explicit tuple/slice hashes, generated
dataclass hashes, native dictionary get/subscript reads, and dictionary/set/view
membership expressions await supported descriptor/field/hash/equality callbacks.
All generated field reads precede field hashing. Explicit cycle state and admitted
field/candidate snapshots survive suspension; native keys complete without
allocating a pending continuation. Synchronous execution keeps its direct walk.

Delete sync-only public hash registration, protocol-query sync detours, separate
builtin-key lookup naming and captured lookup closures. The refinement also
deletes four newly redundant preparation/completion helpers, consolidating their
work into the existing async lookup. No key preclassification pass, type whitelist,
dispatcher framework, owner, policy or option. Correct two uncovered Python bugs:
explicit tuple hashing now invokes key protocols, and empty-set queries no longer
prehash a whole tuple before its earlier custom callback.

Mutations/cache keys/other mapping wrappers and pattern hooks retain synchronous
restrictions. No claim of universal async key support. This replaces the
[earlier documented read limitation](../2026-11-06-key-suspension-contract/README.md)
inside the stated boundary.

## Complete-script cost and decision

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16; immutable b94 worker/protocol2,
ordinary GC/tiering. Two independent identical current roles, original51db4209
reference from its verified installed Release package (e9d86337), and CPython.
Five guest warmup seconds/role, seven rotated150ms rounds. Both B334/B335 take
86.01s/three complete jobs, owned130s. Full goldens, quiet endpoints, Closed/final
funding, actual joins and fixture removal pass. Raw archives locally hash-verified.

| Variant / whole script | Candidate / identical control | Reference | Changes versus reference |
| --- | ---: | ---: | ---: |
| B334 initial async text |2.813 /2.733ms|2.647ms|+6.29 /+3.25%|
| B334 initial async CSV |5.563 /6.412ms|5.501ms|+1.12 /+16.56%;15.27% role spread|
| B334 sync text control |2.334 /2.311ms|2.320ms|+0.57 /-0.40%|
| B335 consolidated async text |2.777 /2.815ms|2.677ms|+3.72 /+5.13%|
| B335 consolidated async CSV |5.884 /5.563ms|5.523ms|+6.54 /+0.72%;5.46% role spread|
| B335 sync text control |2.289 /2.257ms|2.340ms|-2.19 /-3.55%;no source gain attribution|

Native async text is adverse in both collections; the simpler completion path
does not establish recovery. CSV remains uncertain due identical-role variation.
Retain the working semantic boundary and deleted detours, accepting the measured
text cost while prioritizing completed-native execution transport in the next
whole-script source gate. Do not keep retiming this unchanged design or imply
the performance objective is complete. B61/B69 slower call-control/arena lessons
still apply; no generic wrapper framework is justified by these observations.
No allocation, causal CPU, RSS or embedding-latency gain measured. Preserve all
original adverse results, and do not combine source-pair percentages.

Warm-async uses RunAsync with completed host I/O; CPython's matching lane executes
ordinary Python. Clocks include mediated RAM I/O, full output/file validation and
prior-result cleanup. Compilation/startup/staging/reset/transport/final teardown
excluded. Genuinely pending I/O is separately tested for semantics/ownership, not
benchmarked for latency. The primary four-script table remains794e552b.

## Qualification and evidence

Focused1,012 public/429 internal per actual.NET11RC3/.NET10.0.8 pass. Twenty-one
independent CPython goldens cover descriptors, whole queries, namespace bridges,
field order, cycles, mutation and lowered class bodies. Real suspended hash
descriptors/bodies and dataclass field reads retain funding; cancellation/refused
admission waits for cleanup, preserves first origin despite finally-return, leaves
containers unchanged and permits shared-budget reuse after owner cleanup. New
continuation tests bound deep/cyclic hashes through managed admission and recover.

Full local.NET11 passes4,227 internal/16,137 public before the two additional depth
cases; those separately pass with the ownership family (18cases) on both actual
runtimes. Production source is unchanged. Full platform/originalStress/installed
qualification remains pending;51db4209's closed receipt excludes Q02.

[Initial contract/source/raw data](initial/), [refinement](refined/),
[validation](validation.json), [focused/build/full logs](focused-checks.tgz).
The failed nullable test assertion/build is retained and corrected; no runtime
change was needed for it. B335 setup refused for insufficient disk headroom before
any service launched. Only hash-verified redundant transfer archives were removed;
source images/dependencies/raw evidence remained. Source/worker hashes and free
space were rechecked before the sole launch. Cleanup/recovery records are retained.
No live comparison work remains; publication/tagging postponed.

Committed/pushed2adf73f3 contains the recorded refined runtime sources. Both
installed Release consumers pass on the actual.NET11/10 runtimes. Exact source
and scope: validation.json; package-consumer.log. CI42180013420 is running for
Windows/Ubuntu, both runtimes, originalStress and installed consumers. Pending
qualification is not a green receipt. Earlier51db4209 is closed and never rechecked.
