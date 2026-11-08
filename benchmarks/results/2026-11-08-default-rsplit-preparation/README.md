# Default-rsplit cased-word preparation — 2026-11-08

Retain the existing cased-word pipeline for default zero-argument whitespace
rsplit. Two compiler eligibility checks no longer restrict it to split. The runtime
already uses the same unlimited-whitespace planner; no new operation/backend,
owner/pool/cache/configuration or execution route. Affected chains avoid the
intermediate cased UTF-8 line and separate casing/splitting work.

Baseline committedcbe88fef/core57e1ef6c, independently fully qualified at
[CI42477957586](../2026-11-08-shared-split-construction/delivery-ci.json).
Candidatecbe88fef plus archived two-file
compiler patch/core3fb37cc3. Complete hashes/production inventory in identity.json.
Informational source metadata names the precommit baseline. Committedc74c5802 independently passes Windows/Ubuntu actual11/10, originalStress
and installed core consumers at [cached CI42479543467](delivery-ci.json). The primary
CPython guide remains the recorded94cb8963 checkpoint.

| Complete helper report | Allocation sync / async | Controlled elapsed, all four median pairings |
| --- | ---: | ---: |
| Default lower-rsplit, sync |−16.46% / —|−14.19 to −16.70%|
| Default lower-rsplit, async API |— / −16.46%|−12.88 to −13.44%|
| Default upper-rsplit, sync |−11.88% / —|−7.85 to −11.77%|

Upper async allocation is also−11.88%; its controlled elapsed is unmeasured.
Coarse process CPU agrees on all affected jobs. Baseline spreads2.05%lower-sync/
0.01%lower-async/2.53%upper; candidate spreads0.94/0.64/1.77%. These are short
controlled observations, not confidence intervals or universal CPU gains.

Unchanged ordinary-split control elapsed−1.04to−4.15%, mixedCPU/baseline2.67%spread:
no attributable gain. CSV−0.12to−1.31%, CPU mixed/near baseline. Report−3.94to+2.69%,
baseline5.77%spread/mixedCPU, inconclusive. Every adverse desktop observation and
original49%async-baseline drift is retained. R95 text/R96 report/R98 desktop debt
or uncertainty is not repaid by this different newer source pair.

All reports read the complete maintained fixture, aggregate words through an
ordinary helper and write the full output. Lower/ordinary text:3072lines/35840B;
upper is the same input, independently confirmed with isolated local CPython and
full stdout/file goldens. Existing worker7cf37ca5, optional0.4 adapter, actual.NET11RC3,
quietUbuntu24.04.4/F4as_v7,4EPYC9V45vCPUs/16GiB/noswap. NormalGC/tiering,5s warmup
per process/job, two independent copies of each core,7rotated150ms rounds. Six
jobs167.959s. Command-v2 includes complete execution/mediated RAM I/O/full output
checks/prior-result cleanup; compilation/startup/staging/final cached teardown
excluded. I/O completes inline; genuine suspension is tested separately.

120public/35internal focused Release peractual11/10 pass. Existing independent
Unicode inventory and terminal-ownership tests now cover both method names:
all29 Python whitespace scalars, expansions/final sigma, aliases/result owners,
refusal/cancellation/cleanup, helpers/branches/protected bodies. Custom methods
retain real suspended host reads and exact file-close counts. A separate Python
oracle preserves supplied maxsplit/separators and visible cased locals; only
zero-argument defaults qualify.

All source/core/auxiliary/Ready/golden/quiet/Closed/exit/join/fixture/archive/local
verification gates pass. B459 is terminal/cached, no remaining users or fixtures.
Persistent data remain separate from owned RAM. A bounded exact-byte hardlink
operation recovered27975680B while retaining every original path/alias/content and
all archives; fresh pre/post proof is included. Raw collection verified240MiB
OSfloor+7MiB headroom and actual payload before launch. No evidence was discarded.

[Controlled results](controlled-comparison.json), [allocation/desktop evidence](allocation-and-local.json),
[identity](identity.json), [status](status.json), [sources/cores/logs/raw](observations.tar.xz).
