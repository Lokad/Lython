# Native CSV record construction retained

2026-11-02, source c23d1a10 plus [exact implementation](prototype.patch). Selective revised follow-up to [B74](../2026-11-01-csv-records-rejected/README.md); its original failed percentage gates and adverse text points stay unchanged. The revised constructor handles native string-list headers for equal, short and extra-width records. **Retain the change for substantial allocation reduction and consistent affected-workload CPU improvement.** No universal elapsed speedup is claimed.

Normal records now use existing whole-capacity admission and direct traversal under one ambient scope. This removes native header cursors, repeated field/length dispatch, short-row tail slicing/cursors, incremental table growth and per-key wrappers. Dynamic/nonstring headers retain the required Python protocol route. There is no new owner, pool, cache, backend, public option, parse-time dictionary fusion or retained-row copy.

| Complete4,096-record job | Allocation reduction vs both originals | CPU reduction vs both originals | Elapsed change vs both originals |
| --- | --- | --- | --- |
| CSV plain, block1 |15.07-15.09%|7.23-8.36%|-0.15%/+0.48%|
| CSV helper, block1 |13.74%|8.49-8.54%|-5.89%/-6.52%|
| CSV plain, block2 |15.10%|10.44-11.12%|-6.72%/-6.96%|
| CSV helper, block2 |13.70-13.72%|8.14-9.19%|-7.20%/-7.52%|

Positive elapsed means longer. The first plain-CSV elapsed result is effectively unchanged despite lower CPU; preserve it. Unchanged text allocation stays within0.03%. Its block1 elapsed IQR is16.7-17.9%, so no elapsed effect is established. In block2, text candidate lies between originals:6.642ms vs6.771/6.553ms; CPU likewise between controls. Original text CPU drift is roughly4-5%, candidate changes direction relative to them, and no repeated candidate regression against both is demonstrated. This retains uncertainty about control CPU dispersion rather than calling it a speedup. CSV CPU IQR0.29-1.83%; complete elapsed0.95-3.12%.

Two independent counterbalanced processes reuse the existing three-image complete-job driver on the quiet Standard_F4as_v7 Ubuntu24.04.4 VM, actual.NET11RC3, ordinary GC/tiering.256 interleaved warmups/image/case,9 rotating64-job batches,2s settle/case. Images share CoreLib and verified Python key hashes; all auxiliary binaries match. Clock includes fixture reset, efficient mediated I/O, full stdout/file validation and result disposal; compilation/startup excluded. CPU includes process-wide background runtime activity, not a causal fraction. All goldens and per-run/final budget balances pass; all fixtures/users clean up. Both quiet gates pass; owned service129.226s/107.550CPU seconds, exited0, inactive/dead/MainPID0/empty group/lease free.

Fresh nonincremental Release solution build, zero warnings/errors.117 internal/388 public focused cases pass on each actual.NET11/10: CSV semantics, protocol suspension, cancellation, refused admission, retained owners/shared-budget reuse, simple helper and Unicode regression controls. Two new independent CPython3.13.2 goldens cover duplicate missing/extra fields, restkey collision, header mutation, direct shared-reader consumption and retained earlier rows. Existing transition golden switches string headers to protocol keys and back. No timeout/assertion relaxation. Full platform/stress/installed-consumer delivery passes in coherent8f07764a; terminal proof is below.

Duplicate names and restkey collisions can leave unused preallocated capacity. It stays charged; earlier conservative refusal is intentional. Exact tiny-budget thresholds are not a compatibility requirement. Exposed fieldnames and queued parser rows remain shared and lazy; no dictionaries are constructed during parsing.

[All samples](assessment.json), [source/image identities](identity.json), [prospective design](pre-code-plan.md), [independent goldens](goldens.json), [raw driver/quiet/cleanup evidence](evidence.tar.gz), [build/TRX](validation-logs.tar.gz). Measured source is exactly recorded; future revisions need their own external baseline.

Full coherent delivery **8f07764a is qualified**: [terminal CI proof](delivery-ci.json),
Windows/Ubuntu ordinary.NET11/10, original Stress both and installed Release
consumers both all pass. Basef56338f2 was [superseded while incomplete](delivery-superseded.json)
to batch the retained casing change; its earlier successful scopes and both prior
actual failures remain visible. No repeated local matrix, publication or tag.
