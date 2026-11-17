# Complete helper/container construction

R498/B920,2026-11-17 UTC. **Private four-file prototype; retention pending.**
Qualified runtime0d2a6ab7/frozen8d26963c (fixture-only242ca284/fullGREEN44173843690)
versus private8e738bd7 onf5064aec. Same complete copied32file images/catalog,
only core differs; native16slot ABI/fast loop/cleanup frontier retained.

Remove SupportsNativeInstructions/IsInlineInstruction's duplicate whole-code
opcode inventory. Lexical/storage admission stays; the existing native loop
hands off untouched at the actual PC when a frame/protocol/handler is needed.
Shared final-slot list/set/dict kernels replace ordinary construction loops and
serve closed dict/set returns without a general call frame. Protocol/composite
keys hand off before traversal/callback; hashing/order/identity preserved.
Reserve maximum display capacity, including duplicates, before allocation;
earlier rejection is intentional. Unpublished backing refunds on construction
or input-cleanup failure; contained aliases retain independent funding. No new
dispatcher/ABI/arena/owner/identity ledger or resource policy.

The new complete JSON-lines record transform reuses existing input/output goldens:
4,096records/110,592B, helper returns an ordinary two-field dictionary. Before,
MakeDict rejected the whole helper's native route; after, the closed call
constructs directly from final slots. Existing floating report, normalization
and positional calls are controls. This is a new useful workload shape, not a
claim that it dominates all existing scripts or explains CPython gaps.

Actual11/10 each1,191internal/4,858public focused checks pass. After final explicit
rollback catches,401internal/47public pass each (overlap), plus7new observable
semantic/ownership/refusal cases. The same7pass unchanged production. Initial
projected-CLR-identity assumption fails both images: assert Python alias identity
inside the guest and snapshot values/charged owners at the public boundary.
Keep original source/failures; no production projection contract changed. Initial
build missed a namespace import; original failed build retained. Both actual
worker images pass6goldens and allocation/reuse; no full prototype delivery yet.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference/candidate spread |
| --- | --- | ---: | ---: | ---: |
| scripts.json-record-transform.large | compile | -1.44..-0.64% | +0.62..+1.13% | 0.74/0.07% |
| scripts.json-record-transform.large | warm | -20.32..-16.54% | -21.05..-17.72% | 3.92/0.80% |
| scripts.json-record-transform.large | compile-run | -20.66..-19.65% | -20.68..-20.00% | 0.53/0.74% |
| scripts.numeric-squared-error.floating.large | warm | -2.59..+6.69% | -2.88..+7.17% | 3.60/5.71% |
| scripts.numeric-squared-error.floating.large | compile-run | +2.65..+10.07% | +1.77..+9.70% | 2.32/4.80% |
| scripts.text-normalization.large | warm | +0.70..+1.57% | -0.18..+0.21% | 0.03/0.83% |
| calls.positional.large | warm | -2.46..-0.03% | -2.76..-0.40% | 1.76/0.71% |

New transform allocation4,806,562→2,905,606B/invocation(−39.55%); accounted peak
6,576,643→6,576,387B. Others effectively flat; all shared allowances restore.
Record fresh improvement has close controls/supporting CPU; warm reference
movement limits magnitude. Floating fresh is adverse, with moving controls;
warm is inconclusive. Text elapsed about1%higher without supporting CPU direction.
Do not promote yet. One unchanged-image floating warm/fresh confirmation with
8second rotated warmups is justified to decide debt/refinement. Keep original
adversity; do not infer recovery or permanently close a design from uncertainty.

Seven jobs267.50seconds, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,6second rotated
full-request warmups/five rounds/four processes, natural GC/tiering. All actual
image/catalog/golden/quiet/Closed/exit/join gates pass. Warm includes Run/mediated
RAM I/O/goldens/prior-result cleanup; fresh adds compile/compiled-owner disposal;
compile includes compiled-owner disposal. Startup/reset/protocol/final cache
teardown excluded; external CPU wider. No refreshed CPython ratio/startup claim.

Staging uses only exact committed old raw replicas after fresh idle lease/path/
UID/hash/nlink/all-alias proof. Different B202raw preserved. Initial read-only
space probe's8MiB estimate was insufficient; actual existing254MiB staging/252MiB
archive guards pass without changes. Qualified/current images/catalog/toolchains
remain; RAM evidence SHAarchived persistently while lease held, all workers joined.
Terminal cached. Broader performance objective active; publication postponed.

[Summary](summary.json),[allocation](allocation-summary.json),[sources](candidate-sources.tar.xz),
[patch](prototype.patch.gz),[identity](identity.json),[checks](checks.json),
[final checks](final-checks.json),[raw](first/observations.tgz),
[procedures](procedures-and-checks.tar.xz).
