# One active structural identity path — 2026-11-06

Retain R02: one vector of currently active identities replaces separate single/
pair cycle hash tables, their identity comparer/hashing and reversed-pair probes.
Scope keeps only its originating state and identity key; no duplicate kind/active
fields or generated record equality/hash. Native callers and public API stay the
same. The partial repr recursion marker reads the same active-single membership.
No new cache, frame pool, ledger, owner, tracing or resource policy.

Source d319dffa / production bf741ec2 plus [recorded patch and exact hashes](identity.json).
Candidate867ebf7a versus Q01 production df1f1f2c; both frozen Release builds before
the R02 source commit. [Contract](contract.md), [correctness history](correctness-history.tgz),
[independent Python controls](independent-goldens.json), [validation](validation.json).
The [bounded source/cost gates](../2026-11-06-runtime-cost-gates/README.md) selected
this work without predicting a percentage gain.

## Preserved contract and tradeoff

Immediate identical/reversed pair cycles still fail; single and pair traversals
of the same object are independent. Scope exit removes and clears its entry,
including out-of-order disposal and continuation on another thread. Explicit
async execution state, ambient lifetime, first-refusal/cancellation behavior and
managed depth/actual stack checks are unchanged. Depth-only scopes allocate no
cycle storage. Noncyclic repeated aliases remain valid after unwind.

Membership is O(active depth), bounded by existing managed admission (at most512
structural levels, often fewer with active Python/interpreter calls). Deep nesting
can do more comparisons than expected constant-time hash lookup; no depth limit
or timeout is raised. Small active paths lose identity hashing/table maintenance.
Existing coarse runtime metadata ownership remains, with no additional allowance.

New controls preserve reversed cycles, mixed kinds and out-of-order cleanup;
two independent Python examples hash active comparison operands and compare an
active hash operand. All23 hash-query family examples agree with CPython through
both APIs. Existing tests retain real async hops/interleaving/cancellation, actual
managed-depth rejection, first admission refusal, container preservation, shared
budget recovery, retained sources and recursive partial repr. The first build
identified a direct partial-render Singles consumer; it was migrated to the shared
membership check before validation, with no dormant compatibility field.

Focused477 internal/1913 public per actual.NET11RC3/.NET10.0.8 pass; after removing
unused generated record methods, targeted38/42 and all23 independent Python cases
pass. Full local Release4,215 internal/16,103 public pass on each actual runtime.
Final platform/stress/installed qualification remains separate; Q01 GREEN receipt
does not qualify this edit. Batch it with the next coherent construction checkpoint.

## B329 complete-script comparison

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16; unchanged b94 worker/protocol2,
normalGC/tiering, five guest warmup seconds/role and seven rotated150ms rounds.
Two identical candidate processes, Q01 reference and CPython.112.73s/four jobs;
full goldens, quiet endpoints, Closed/final funding, joins and fixture cleanup pass.
[Raw observations](observations.tgz), [summary](summary.json).

| Warm script | Candidate / identical control | Q01 reference | Changes versus Q01 |
| --- | ---: | ---: | ---: |
| Complete compound-key aggregation, width2 |3.252 /3.303 ms|3.878 ms|-16.13% /-14.82%|
| Complete compound-key aggregation, width8 |5.995 /5.965 ms|7.194 ms|-16.66% /-17.08%|
| Primary text summary |2.269 /2.313 ms|2.317 ms|-2.09% /-0.17%, control-limited|
| Primary CSV summary |4.709 /4.735 ms|4.724 ms|-0.30% /+0.25%, roughly flat|

Compound-key identical-role spread1.56%/0.50%; Lython IQR below0.96%. Measured
affected-script elapsed gains support retention alongside the removed mechanisms.
Text's1.96% identical-role spread limits its smaller apparent gain; CSV reference
IQR3.11% is explicit. No broad improvement, process allocation, guest-only CPU,
working set or embedding-latency improvement is established. Width2/8 still cost
7.23x/6.51x CPython here. Do not add these deltas to Q01's different source pair.

Warm clocks include execution, mediated RAM I/O where present, full output/file
validation and prior-result cleanup. Compilation, startup, staging/reset,
transport and final teardown are outside these clocks. The primary four-script
compile/warm/fresh table remains tied to1134d6e8. Whole-script work continues;
publication/tagging are postponed.

Combined source794e552b is committed/pushed. Both installed Release consumers
pass. Platform/stress/package delivery42170841788 is running; see validation.json
for exact scope/status. The B331 primary CPython refresh is independent of this
affected-workload evidence and will identify its actual source and boundary.
