# Reusable dispatcher: withdrawn

2026-11-02, runtime baseline8f07764a (source HEAD2ec3f913); quiet dedicated Ubuntu/.NET11RC3 VM. One mutable dispatcher plus stable lexical views and admitted value continuations removed ordinary child interpreter objects. Existing direct locals/operand arrays, native returns, actual contexts and one executable engine remained. This materially differs from the earlier arena/control-holder trials.

**No prototype remains; no performance improvement is retained.** Original two counterbalanced blocks established real allocation savings alongside CPU regressions. One prospectively documented correction removed unnecessary by-value activation transport. The corrected two blocks still regress affected CPU:

| Complete job | Refined allocation | Refined CPU versus both originals |
| --- | ---: | ---: |
| Text helpers |–12.88%|+7.5–12.8%|
| File search |–26.37%|+15.5–24.0%|
| Captured-generator text |about+4.6%|+0.7–5.6%|
| Native CSV/JSON helper controls |approximately unchanged|mostly flat; second JSON block drifts|

Each block uses256 warmups/image,9 rotating rounds x64 complete jobs/image and an identical original control. Full public Run/fixture reset/efficient mediated I/O/stdout+file checks/result disposal are included; compilation/startup are excluded. Shared CoreLib/hash witnesses/auxiliary bits and ordinary GC/tiering are verified. Detailed clocks, IQRs and every adverse result are in assessment/raw archives. Counts are allocation/CPU/elapsed observations, not an external ratio or a causal fraction.

Both owned services complete naturally in186.932s and186.631s, all goldens/balances/fixture cleanup and quiet gates pass, inactive/MainPID0/empty groups/lease free. No abandoned work. The original prototype passes1248 internal/2265 public focused Release cases on each actual .NET11/10 runtime; the mechanical correction passes1248/188 on.NET11. Initial21 failures came from four inspection helpers expecting the formerly published dispatcher; live-slot/physical-tail and semantic assertions were preserved. Those representation-only test changes are removed with the prototype. The captured-generator output and all files match independent CPython3.13.2.

The original tracked source is restored and the new activation file removed. Its next nonincremental build is successful. Existing full8f platform/Stress/installed-package qualification is reused; no new full cycle is justified by unchanged source. The performance objective remains open. Stable-view allocation, continuation transport and mutable hot fields add costs; these runs do not isolate one exclusive cause. A future call/frame design must remove construction without reproducing this transfer cost. No permanent architectural veto follows from this trial.

[All measurements](assessment.json), [original raw evidence](initial-evidence.tar.gz), [corrected raw evidence](refined-evidence.tar.gz), [exact source and driver](prototype-and-driver.tar.gz), [validation including initial failures](validation-logs.tar.gz), [restoration](restoration.json).
