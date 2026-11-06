# Canonical builtin initialization — R22

Retain one fixed plain-function catalog and immutable constructor signatures.
Remove repeated plain wrapper/parameter-array construction and signature-interning
lookups from every invocation. Existing15 shared functions become37, and17 type
signatures are prepared once. Every run still constructs mutable types, exceptions,
namespaces and type caches. Plain functions' __self__ resolves through the active
caller; no host, token, run owner or guest storage enters this catalog. No public
option, type-graph cache, factory framework or memory subsystem is added.

Fixed runtime metadata already has application lifetime; result/cache/guest
storage keeps its existing owners. Admission estimates stay conservative and
unchanged. Builtin namespace entries are grouped by initialization lifetime;
supported names and user-dictionary insertion semantics are unchanged. Namespace
inventory order is an implementation detail, not a claimed CPython ordering match.

## Evidence and limits

2026-11-06 UTC. Baseline runtime4fad63c5 on a4a1c306; candidate a4a1c306 plus
archived initializer patch. All baseline production hashes match frozen c3e65ad3
used in B367/B368. Candidate corea87cd017, unchanged b94fa98c VM worker/host,
normal0.4 optional adapter/producer. Exact source/binary identities remain here.

Private reflected factory diagnostic, normal GC/tiering,512warmups/seven1024-call
batches:13,064→10,376B/table, exactly2,688B removed. Desktop8.15→4.01us is a
diagnostic boundary with reflection and unrelated runtime activity, not a complete
script or CPU claim. The initial diagnostic used a nonexistent frozen binary path;
no measurement ran. Correct to the actual b366 file and verify its recorded hash.
The source-edit inventory preflight also first expected105entries; the actual104
entries were inspected before any edit. Neither failure changes an assertion.

Separate existing complete-job allocation driver: eight scripts/controls per API,
32warmups and five16-job batches, baseline/current/identical-baseline isolated
images sharing CoreLib/hash seed. All goldens, isolation/witness, balances and
fixture cleanup pass. Ordinary text/CSV/JSON/report allocation reductions are
under0.2%; large config about1.17%. Most rows remove roughly2.7KB/job; asynchronous
process allocation includes background noise. These are not RSS/heap/embedding
latency results. Repeated constructor metadata does not dominate the text gap.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 source pair B368:88.20s, four complete jobs,
5s warmups/role, seven rotated150ms rounds, reference/current/identical current,
normal GC/tiering. All actual identities, quiet endpoints, output/file goldens,
terminal balances, Closed/joins, fixture cleanup and under-lease persistent archive
verification pass. No SSH before150sowner+30sstop. Collection is terminal.

| Boundary | Reference | Current / identical control | Elapsed change |
| --- | ---: | ---: | ---: |
| Config16paths / warm |76.893us|76.022 /75.014us|−1.13 /−2.44%|
| Config16paths / compile-to-result |150.756us|150.951 /148.605us|+0.13 /−1.43%|
| Text3,072lines / warm |2.229ms|2.115 /2.202ms|−5.13 /−1.24%|
| JSON / unchanged compilation control |64.837us|63.780 /64.060us|−1.63 /−1.20%|

Config role spreads1.33/1.55%; text4.10%, despite within-role IQR below0.85%.
The unchanged compilation control improves too. Warm config has modest elapsed
evidence, fresh is essentially flat, and text remains role-dependent. Do not claim
a general CPU gain, causal repair of prior text debt or a refreshed CPython ratio.
Process CPU ticks remain coarse, and fresh config includes an adverse current role.
This useful initialization simplification does not complete the performance goal.

Warm includes mediated RAM I/O, full validation and prior-result cleanup; fresh
adds compilation/program disposal. Startup, staging/reset, transport and final
teardown excluded. Independent block medians are not additive/subtractable costs.

## Qualification and cleanup

Release solution builds both targets with0warnings/errors. Focused486internal/
2812public pass per actual.NET11/10. Existing isolation scenarios now exercise33
plain functions: attributes/type identity, module rebinding, sequential/suspended
executions, cancellation, retained results and shared-budget reuse. Existing
function/builtin/exception/refusal/ownership/cancellation checks remain intact.
Full platform/original Stress/installed-package delivery belongs to the next
combined boundary; these focused checks do not establish full qualification.

VM initially had7MB free. Retire only the exact8,954,180B expanded old JSON perf
copy after verifying it against the independently committed/pushed original raw
archive93074207 and its member hashe68d464d. All users ended, no symbolic-link
dependency, no binary/source/archive removed. This preserves the original old
capture in benchmarks/results/2026-10-31-architecture-profiles/profile-evidence.tar.gz.
No further collector run or broad ownership audit was needed. Publication postponed.
