# One exact scalar comparison entry

R163/B513,2026-11-09. Retain the shared comparison entry for exact numbers and
strings; delete the executable string detour through general operator/protocol/
truth machinery. Numeric classification order retained, bounded UTF-8 kernel kept
outside common dispatch. No cache, tagged representation, backend, policy or option.
Custom/reflected/raw comparisons, actual async protocols, NaN, chains and owners stay.

Source pair: committedb59c2be3 versus final R163 precommit source, **two fresh
nonincremental freezes**, common unchanged worker/adapter/auxiliaries. Exact hashes
and complete source manifests retained. Not a postcommit/future-HEAD timing or a
CPython comparison. Original monolithic prototype is a different image.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Four jobs115.042s,
four independent processes,5s warmup/role,7rotated150ms rounds. Ranges compare both
candidates with both baselines. Includes mediated RAM I/O, full outputs and previous
result cleanup; excludes compilation/startup/reset/protocol/final cache teardown.
Coarse process CPU includes wider reset/protocol work and is separately diagnostic.

| Complete warm script/API | Elapsed change | Coarse CPU change |
| --- | ---: | ---: |
| CSV validation sync |−3.74% to−2.10%|−3.13% to+2.93%|
| CSV validation async |−6.08% to−4.53%|−6.90% observed|
| Search async control |−3.06% to−0.24%|−3.31% to−1.11%|
| Report async control |−4.31% to+0.92%|−2.79% to0%|

CSV controls spread at most1.13%; async elapsed/CPU supports this source-pair
improvement. Sync CPU is mixed. Report unchanged baselines differ5.37%, making its
elapsed inconclusive. Favorable search control does not establish an optimization
gain there. No general CPU improvement/equivalence claim or fixed-percentage veto.

Original desktop prototype: ordinary/compile allocations approximately flat;
config small process-allocation variation remains. Text unchanged controls27.87%
apart, CSV mixed, async search+8.79–9.86%, report+2.99–4.59%. Keep this unexplained
adversity. Before quiet timing, refine source to retain original numeric prefix and
separate bulk text loop. No demonstrated JIT/inlining explanation, original timing
relabeling or final-refinement allocation claim; no second desktop timing campaign.

Final focused Release **65internal/306public per actual11/10** pass, including the
new active-comparisonStress case, current tokens, terminal first refusal, retained
results/aliases, genuine suspension and cleanup. Twelve UTF-8 value/branch pairs
independently match CPython3.13.2: inline/heap, NUL, BMP/astral order, prefixes,
4095/4096/4097 boundaries. Stress uses two funded256MiB inputs and owned canceller/
runner joined before retirement. It observes cancellation before returning a value;
this is concurrent evidence, not deterministic scheduling or checkpoint counting.
Source contains explicit checks within the bounded comparison loop.

Quiet launch first refused a3.17MB space deficit before extraction/work. Under a
fresh idle lease, verify only two exact tracked/single-alias/SHA/owner/group/mode/
parent copies, persist pre-mutation plan, remove redundant B510/B509 compressed
archives4.12MB. Their raw/receipts/frozen workers/canonical Git copies survive.
Fresh sufficient space would skip deletion. Same staged payload resumed; floors
remain240MiB OS+12MiB evidence. All identity/golden/quiet/ownership/Closed/join/RAM/
archive-under-lease/localSHA gates pass. No users/fixtures remain.

Full Windows/Ubuntu actual11/10, originalStress on both platforms and installed
consumers pass in [CI42640859228](delivery-ci.json), sourcef6861aa5. Terminal success
cached; never requery. R164 is separate source and not qualified by this receipt. Publication postponed. Structural string equality/
ordering still use older whole-span primitives outside this operator path; next
R164 consolidates the existing kernel with explicit current context. No claim of
universal string-comparison interruption or completion of the performance objective.

[Quiet summary](summary.json), [original desktop summary](local-summary.json),
[decision/checks](decision.json), [source manifests](final-source.json), [baseline](baseline-source.json),
[identities](identity.json), [original identity](original-identity.json), [Python goldens](cpython-strings.json),
[quiet raw/cores](observations.tgz), [original raw/cores](local-observations.tar.xz),
[preparation/check logs](preparation-and-checks.tar.xz), [space proof](space-recovery.json), [terminal](terminal.json).
