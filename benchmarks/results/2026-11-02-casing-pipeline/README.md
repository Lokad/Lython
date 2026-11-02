# One governed UTF-8 casing pipeline

2026-11-02, runtime sourcef56338f2 plus [exact final change](refined/prototype.patch). **Retain the unified pipeline:** lower/upper/capitalize/title/swapcase now share one builder, UTF-8 decoder, output completion and cleanup loop. Three duplicate loops and four eager final-sigma tracking states disappear:43 net production lines removed. Context is inspected only at capital Sigma; title retains its distinct previous-cased word rule. No new backend, pool, cache, owner or public option.

The original UTF-8 subject remains borrowed. Backward Sigma scans stop at an earlier non-ignorable/cased Sigma, so intervening ignorable runs are visited at most once per direction. Existing property tables, Python expansions, Unicode17 compatibility, builder capacity/replacement admission, per-scalar/context cancellation and final-output refusal/refund behavior remain. No CLR strings or per-rune storage are introduced.

## Whole-script comparison and one justified refinement

B29 identified property-search work; B56 had promising but inconclusive6-10% text points. The new design removes duplicate pipelines as well as those lookups. Its initial two controlled blocks lower complete text-helper CPU8.2-10.7%/elapsed6.8-7.8%, unchanged allocation. Uppercase CPU rises16-19µs (2.6-4.5%); second-block elapsed is adverse. [All initial samples](initial/assessment.json), [first patch](initial/prototype.patch) and [raw evidence](initial/evidence.tar.gz) remain unchanged.

Source shows prefix/following windows still being constructed on every rune. The one prospective [refinement](refinement-plan.md) passes the already-borrowed whole subject/offset and creates windows only inside Sigma's branch. It retains the single pipeline. Final two-block results against BOTH identical-source originals:

| Complete job | Block | CPU change | Elapsed change | Allocation |
| --- | --- | --- | --- | --- |
| Text summary/helpers,3,072 lines |1|-6.81%/-10.12%|-5.75%/-7.55%|Within0.03%|
| Text summary/helpers |2|-10.27%/-8.83%|-7.86%/-7.35%|Within0.03%|
| Text uppercase |1|-1.72%/-0.30%|-0.68%/-0.20%|Unchanged|
| Text uppercase |2|-0.19%/-0.16%|+3.69%/-2.53%|Unchanged|
| Unaffected JSON |1|-2.55%/-1.70%|-2.21%/-0.96%|Within0.03%|
| Unaffected JSON |2|+0.86%/-2.77%|+1.08%/-2.02%|Within0.03%|

Positive means longer/more. Final text candidate6.354/6.313ms, CPU4.774/4.847ms. Text elapsed IQR0.71-3.11%, CPU0.06-1.49%. Uppercase elapsed remains variable2.3-8.3% IQR, candidate between originals in block2; CPU essentially matches both originals. JSON controls drift around the originals; no speedup is attributed to unchanged JSON. Decision: no consistent remaining upper/control CPU regression is demonstrated, retain the deletion and repeated affected-job improvement. No further tuning campaign is opened. These are controlled named-workload diagnostics, not a causal CPU fraction or refreshed CPython ratio.

Same quiet Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3 VM, ordinary GC/tiering. Existing three-image complete-job driver, shared CoreLib/verified hashes and identical auxiliary binaries. Two independent counterbalanced processes,256 warmups/image/case,9 rotating64-job batches,2s settling/case. Clock includes fixture reset, efficient mediated I/O, all stdout/file goldens and result disposal; startup/compile excluded. All measured jobs restore budget baseline, compiled owners end at0 and fixtures/users finish. Both quiet gates pass. Initial owned service97.890s/79.105CPU seconds; refined97.504s/79.363CPU seconds. Both exit0, inactive/dead/MainPID0/empty group/lease free. Process-wide background GC/JIT remains part of CPU diagnostics.

Fresh nonincremental Release solution builds have zero warnings/errors.203 internal/435 public focused cases pass on EACH actual.NET11RC3/.NET10.0.8, before and after refinement. Existing all-scalar printability and complete property-boundary/gap sigma goldens stay unchanged. All110 later-platform case partners now exercise five methods through both APIs with independent CPython output. Existing cancellation, retained-output funding, refused admission and shared-budget checks pass; no timeout/assertion relaxed. Full platform/stress/installed-consumer delivery is pending at this coherent checkpoint.

[Final samples](refined/assessment.json), [image identities](refined/identity.json), [all final raw evidence](refined/evidence.tar.gz), [pre-code design](pre-code-plan.md), [focused qualification](validation.json), [TRX/builds](validation-logs.tar.gz). Earlier prototype/conditions/adverse observations preserved separately. Publication postponed.
