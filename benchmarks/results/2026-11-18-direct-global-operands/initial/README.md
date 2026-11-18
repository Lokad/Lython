# Direct global operands: initial measured source

R509/B933, private9e184cc7 on7664a04d versus qualified0691d64f/core65fcf7d0.
Adjacent global reads join constants in the existing complemented source-index
space; names resolve live, with cold original spans. No guest marker/cache/owner/
opcode/policy. Remove producer instructions/publication/retirement, centralize
native/resumable/numeric readers and preserve constant-only aggregate proofs.
The numeric-index probe rejects inapplicable keys before unused receiver resolution.

Actual11/10 each400internal/113public focused pass; seven new ordering/protocol/
real-I/O/cancellation/retained-result/reuse cases also pass unchanged qualified
source. Six exact-worker goldens/allocation/reuse pass. Static actual-core IR
removes two floating-gen loads(15→13instructions), storage remains9slots. Allocation
and peaks effectively flat; do not claim storage capacity or allocation reduction.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering, fourjobs154.96s/six-second
warmups/five rotated rounds/two processes per source:

| Complete script/phase | Elapsed change | Wider CPU change | Ref/candidate spread |
| --- | ---: | ---: | ---: |
| Floating warm |−17.40..−4.15%|−16.57..−3.25%|14.20/1.62%|
| Floating compile-to-result |+3.22..+6.89%|+3.12..+8.89%|0.04/3.52%|
| Text warm |−1.51..−0.20%|−1.35..+0.59%|0.72/0.60%|
| Positional calls warm |+1.07..+3.21%|+1.22..+3.29%|1.43/0.67%|

Keep private/unpromoted: warm reference instability limits magnitude, while fresh
and calls are adverse. Removing genuine VM work does not establish CPU improvement.
One changed-source refinement will remove immediate-pool interface dispatch and
the extra generic probe layer while retaining the same semantics. No unchanged
retiming, permanent design veto, rigid threshold or source-condition subtraction.

Warm includes Run, mediated RAM I/O, goldens and prior-result cleanup; fresh adds
compile/compiled-owner disposal. Startup/reset/protocol/final cache teardown
excluded, processCPU wider. All32image hashes/catalog/quiet/join/archive/localSHA/
RAM teardown pass. Exact archived old5e7core aliases alone retire; current65fc/
catalogs/toolchains/unmatched evidence preserved,254/252MiBguards unchanged.

Original static-type-rejected fixture, missing sparse workflow input and invalid
inspector CLI option are retained; these are local preparation errors, not runtime
or elapsed failures. Untimedcbfe is preserved before probe-read correction. Final
source was nonincrementally rebuilt before checks/freezing; all measured files
copied/hashed, not hard-linked to mutable build outputs.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[checks/failures/procedures](procedures-and-checks.tar.xz).
No production promotion/full qualification/publication; whole-script objective active.
