# Unicode capture scalar cursor — R40

2026-11-07. Baseline fully qualified b0889a46/corec21c0c4b; candidateeb6052d0
(.NET11)/49f2de02(.NET10) changes only core scalar projection. Precommit image
SourceLink3604f935 versus baseline79fddd4a is recorded separately; frozen source/
images match the recorded production changes. The optional normal0.4 producer,
core provider contract and memory ownership are unchanged.

ProjectScalarBounds previously restarted at byte/scalar0 whenever a lookbehind
capture preceded the previous whole-match end. Consecutive nearby captures rescanned
growing prefixes. Seek from the nearer known byte boundary: retain origin for far
captures, otherwise decode backwards with the existing cursor, then project sorted
endpoints forwards. This removes the unconditional nearby-prefix restart. No dense
index/cache, pool, owner, policy, API or backend route. Existing capture validation,
leases, metadata arrays, original-subject/result ownership and first-refusal behavior
remain. Both scan directions check current cancellation; cursor publication stays
after successful projection. Arbitrary far captures can still require long walks;
this does not promise linear matching for every regex.

## Controlled complete diagnostic scripts

B392 uses the unchanged b94fa98c worker/host/normal PythonRe0.4, quiet AzureF4as_v7/
Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Two baseline/two current processes,
5s warmup per role/job, seven rotated150ms rounds. Four source-pair combinations
are included in each change range; role differences are not confidence intervals.

| Workload | Baseline ms | Current ms | Elapsed change | Current/reference role spread |
| --- | ---: | ---: | ---: | ---: |
| Unicode lookbehind,1,024scalars |1.983–2.080|1.363|−34.50 to−31.28%|0.02/4.66%|
| Unicode lookbehind,4,096scalars |15.450–16.604|5.226–5.325|−68.53 to−65.54%|1.86/7.47%|
| Unicode lookbehind,8,192scalars |49.842–50.403|10.349–10.472|−79.47 to−78.99%|1.18/1.11%|
| ASCII lookbehind,8,192scalars |8.267–8.382|8.368–8.788|−0.16 to+6.31%|4.78/1.39%|
| Unicode forward captures,4,096scalars |2.359–2.424|2.342–2.429|−3.38 to+2.96%|3.70/2.69%|

Scripts construct their subject, iterate real backend captures and aggregate Python
scalar starts/ends with full checksum validation. No file I/O; the owned fixture is
empty. Elapsed includes public execution, actual backend/guest work, complete output/
file checks and prior-result cleanup. Compile/startup/staging/reset/final cached
teardown excluded. This is a growing-input diagnostic, not the primary representative
CPython comparison, isolated projection CPU or a general regex speedup. Controls
remain variable and possible ASCII overhead is accepted debt; neither parity nor
its cause is established. Retain the algorithmic change; no unchanged retiming.

All5jobs complete143.7459s. Ready/core/every auxiliary, goldens, terminal funding,
quiet endpoints, Closed/exit/joins, fixture removal and archive verification under
lease pass. Observe07:55:25UTC after07:55:07.649950bound(210+30+40s). Terminal
inactive/exit0/no users or fixtures; retrieved archive2a808f28 independently verified.
No more service queries. Raw process-CPU samples stay separate; no compound gains.

## Correctness, ownership and separate allocation

Focused Release853internal/1,224public pass per actual target. Four independent
CPython3.13.2 cases exercise consecutive lookbehind, lookahead/empty matches, real
substitution callbacks and original pos/endpos windows through both APIs. Six new
scaling/control scripts also pass supervised .NET/CPython verification. All script/
result cleanup returns shared funding to baseline and final zero.

An additional active-backward-scan test passes on each framework (183/188ms). It
warms the private projection entry and subject count, starts a substantial funded
scan, verifies its user remains active, then cancels. Structured current-token/span,
unpublished cursor, funding through actual joined completion and root reuse pass.
No provider or interpreter exit supplies that cancellation. The8M-scalar fixture
uses one admitted16MiB UTF8 buffer; no host string copy/GC forcing/checkpoint counter.
It joins even after assertion failure. Test-only builds use --no-dependencies;
frozen core hashes remain unchanged. The small result receipt transcribes tool
output without rerunning unchanged checks merely to capture a log.

Separate same-ALC/CoreLib/process-GC precise allocation is essentially flat; all
admitted peaks match. All six output/file/isolation/balance/fixture gates pass.
Desktop clocks are diagnostic:4,096input can be slower than8,192on the original,
consistent with unresolved tiering/process effects, not a scaling proof. Preserve
all raw medians, non-monotonic/adverse conditions and original source/images; no
desktop CPU claim or subtraction/combination of unrelated phase medians.

R36/R38 receipt42351006316 excludes R40. Subsequent terminal GREEN42357660230
on exactfba865ef qualifies retained R40/R41 production:Windows/Ubuntu ordinary
actual11/10, original Stress and installed core-package consumers.
[Delivery receipt](../2026-11-07-receiver-methods/delivery-ci.json). Publication
remains postponed; the larger complete-script performance objective continues.

[Controlled summary](comparison-summary.json), [raw capture/gates](observations.tgz),
[allocation/desktop observations](summary.json), [focused checks](focused-initial.json),
[active cancellation](active-cancellation.json), [supervised semantics](catalog-verification.json),
[source/images/all development conditions](development.tgz), [commands](collection-commands.tgz).
