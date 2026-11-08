# Direct operand transport — 2026-11-08

R96 removes ExecutableInputs, its reader and repeated runtime arity projection.
The existing handlers capture prepared slots directly; async operations carry
only required references, and synchronous type subscription borrows immutable
instruction metadata. Third-input validation and all managed bounds remain.
No replacement bundle, pool, owner or public option. This is a source-level
transport removal; JIT optimization limits attribution to a particular copy.

Baseline: committedcb2b015e/core9d30623c, fully qualified CI42472284713 separately
after collection. Candidate:cb2b015e plus archived R96 patch/core d6254b2e.
Full hashes/source inventories are in identity.json. Retained production94cb8963.
Full Windows/Ubuntu actual11/10, originalStress and installed-core delivery is independently green at [CI42475420182](delivery-ci.json). The
[combined checkpoint](../2026-11-08-direct-operand-checkpoint/README.md) updates
the primary CPython guide; it does not alter this source-pair attribution.

| Complete script | Warm allocation | B457 elapsed, all four median pairings |
| --- | ---: | ---: |
| CSV helper, sync |approximately neutral|−4.26 to −1.37%|
| CSV helper, async API |approximately neutral|−2.88 to −1.41%|
| Returned CSV records, sync |approximately neutral|−2.35 to −1.50%|
| JSON aggregation, sync |approximately neutral|−4.32 to −1.73%|
| Ordinary rsplit text, sync |approximately neutral|−10.56 to −2.04%, variable baseline|
| Report, async API |approximately neutral|+0.88 to +3.65%, inconclusive/possible debt|

Coarse CPU agrees for CSVsync(−5.19to−2.27%), JSON(−6.25to−1.33%) and text
(−13.04to−2.78%). AsyncCSV/returnedCSV/report CPU spans both signs. The text
unchanged-baseline spread8.70% prevents a precise gain claim. Report baseline
spread2.72% and mixed CPU leave uncertainty; retain its possible elapsed debt.
R95's text regression is still recorded: this unmatched newer-baseline comparison
does not establish its repayment. Retain the simpler transport with those limits;
no universal CPU, exact-copy attribution or whole-performance-objective closure.

B457168.421seconds. Same unchanged complete sources/fixtures/goldens/worker7cf37ca5/
optional0.4, Ubuntu24.04.4/F4as_v7,4EPYC9V45 cores, .NET11RC3. Normal GC/tiering,
5second warmup per process/job,7rotated150ms rounds, two copies of each source.
Timing includes complete execution, mediated RAM I/O/full checks/prior cleanup;
compilation/startup/staging/final cache teardown excluded. I/O completes inline;
real suspension is validated separately. Desktop adverse JSON/report timings and
all unstable controls remain diagnostic evidence, not controlled CPU claims.

Focused Release:62public/24internal per actual11/10. Existing type-subscription,
descriptor rebinding, source failures, genuine suspension, current cancellation,
first terminal refusal, retained input aliases, cleanup and reusable shared-budget
checks pass. Leaf reads remain before operation cleanup; snapshots survive getters.
No new architecture-only tests or public API changes.

The source gate also rejected speculative string layout work: old R94 samples
showed64B PyString objects in1928text/1852CSV samples. Current8Binline/owner/cache
fields already have small storage; a new cold union would add states/objects.
Those samples are not exact type-allocation totals or CPU causality.

All source/core/auxiliary/Ready/golden/quiet/Closed/exit/join/fixture/archive/local
verification gates pass. Exact baseline image/core reused by verified hardlinks;
no evidence deletion. Raw observations, sources and original failed migration
conditions are in observations.tar.xz. No remaining VM users or guest fixtures.
