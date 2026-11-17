# First bulk UTF-8 width implementation

R483/B898,2026-11-17UTC. Qualifiedd8611c85/frozen0315de0a versus prototype
bcf4b5e3 on restored13a56f7d. One production change,585production/2381inputs
archived. Actual copied32file images pass5case CPython3.13.2goldens before VM.
Windows actual11/10 each63internal/49public focused checks pass. Compilation
source snapshot and core hashes identify the prototype; no HEAD speedup claim.

Existing SearchValues CountAny classifies continuation and astral lead bytes in
4KiB cancellable chunks. No new per-string state or owner. Allocation effectively
flat (−0.058..+0.006%), accounted peaks and compiled balances unchanged, each
shared-budget baseline restored. Dense Unicode regresses despite ASCII gains:

| Whole script/lane | Elapsed change | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| Original text warm | +0.28..+18.29% | +3.02..+17.70% | 5.90/11.39% |
| Original text fresh | −1.02..+5.10% | −1.26..+4.74% | 4.93/1.18% |
| Helper text warm | −1.08..+1.57% | −1.41..+1.18% | 1.50/1.16% |
| Report warm | −2.22..+0.59% | −2.92..+1.01% | 1.53/1.32% |
| ASCII text warm | −10.61..−8.40% | −10.00..−7.56% | 1.96/0.49% |
| Dense Unicode warm | +27.73..+28.76% | +22.90..+23.10% | 0.80/0.01% |

Do not retain this footprint. Stable dense adversity justifies one changed-source
refinement using safe managed Vector<byte> classification, removing both tables
and repeated search/count operations. No unchanged retiming or predicted gain.
Original text movement remains unresolved, not a permanent rejection of bulk counts.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,6jobs229.14seconds,6second rotated
warmups/five samples/four processes. Guest I/O/full checks/prior-result cleanup
included; fixture reset/staging/protocol/startup/cache teardown excluded. Wider
process CPU is separately quantized. Compilation only in fresh lane. All machine,
golden,Closed/exit/join/archive gates pass; RAM removed,terminal cached.

Initial source-input freeze still named the deleted architecture fixture; stopped
before timing. Original new-catalog output-file digest and Windows stdout newline
errors were corrected; both workers agree before staging. Initial VM space guard
stopped before mutation. Fresh lease/path/UID/hash/link/all-alias proofs retire
only committed old replicas and withdrawn27ed core. Current0315/canonicals and
254/252MiB floors remain. Original failures preserved in procedures/raw.

[Summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[source](candidate-sources.tar.xz),[identity](identity.json),[procedures](procedures-and-checks.tar.xz).
Publication held; overall performance objective remains open.
