# Immutable JSON input windows — R54, withdrawn

2026-11-07, baseline production d04024ee. The prototype let plain JSON strings
reference immutable UTF-8 input under the same execution owner. It replaced
zero-offset array operations with ReadOnlyMemory windows and transferred the
already-admitted backing charge to batch lifetime. Wrapper/cache refunds kept
surviving aliases and the full array/header funded. Other-owner/unowned input and
escaped strings retained independent copies. No collector, reference ledger,
new owner, alternate parser, pool or public option.

**Withdraw the entire prototype.** No substantial report CPU/elapsed improvement
is established, ordinary allocations increase and ownership state grows. This
is a decision about this representation and its complete costs, not a permanent
rejection of immutable sharing or a fixed improvement threshold.

One unchanged complete-script diagnostic, sync/async separately, shows report
allocation −4.978/−5.031%, JSON-lines −0.952/−0.943%, but search/text/CSV controls
about **+3–4.5%**. A separate precise allocation probe confirms eight extra bytes
per string wrapper. Source storage/copy savings do not outweigh that general cost
on these scripts. Desktop clocks are unstable; async-report baseline drift72%
is preserved, with no CPU claim.

## Controlled whole-script comparison

Quiet Azure F4as_v7, Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Existing worker,
normal optional0.4 adapter and unchanged maintained scripts/fixtures/goldens.
Two independent baseline/two prototype processes, five-second warmup per role/job,
seven rotated150ms rounds. Full warm invocation, mediated RAM I/O, complete stdout/
file validation and prior-result cleanup included; compilation, startup, staging/
reset and final cached teardown excluded. Positive means slower.

| Complete job | All new/old elapsed pairings | New / old process spread |
| --- | ---: | ---: |
| JSON-to-Markdown report | −5.00 to +2.14% |2.34% /5.05%|
| CSV control | −2.32 to +1.01% |2.77% /0.62%|
| Text control | −0.49 to +7.80% |6.23% /1.58%|

Report's3.96% candidate IQR and5.05% unchanged-baseline spread leave uncertainty;
coarse process CPU is adverse/mixed. Text's adverse candidate and divergent
identical-current role remain visible. No substantial speedup or cause of that
variation is established. Do not combine these percentages with earlier gains.

**B412 passes all three jobs in86.7502s**: actual core/auxiliary identities,
goldens/funding, normal runtime, quiet endpoints, Closed/exit/join, empty fixture
mount/unmount and persistent archive verification under the lease and locally.
No live workers/fixtures remain; no terminal service is queried again.

B411 failed **before sampling**: a symlinked worker loaded original83af401e
instead of intended0c126b2e. A Ready-only diagnosis records the actual old path/hash,
then Closed/exit0 with no guest request. Original failed setup/raw state is
archived under a maintenance lease and verified locally before fixture unmount.
B412 changes only auxiliary staging to hard links; intended source/cores, protocol,
inputs, timings and gates are unchanged. This correction is not a failed-design
conclusion or discarded adverse measurement.

## Correctness and restoration

Prototype focused Release **979 internal /4,778 public per actual .NET11/.NET10**
pass. Four small storage cases cover full backing/header retention, nested aliases,
source-shell refunds, reentrant admission/refusal, separate owners and allowance
reuse. Existing Unicode, strict/lenient JSON, raw prefixes, hooks, original error
offsets, actual asynchronous callbacks, cancellation and terminal refusal remain.

The initial eight failures are preserved: tests mutated governed white-box input
backing or required the previous copied-output charge. Prototype controls retained
external-buffer independence and refused all available output funding rather than
checking the old representation's exact threshold. **All those test edits and the
four view-specific cases are restored/removed with the discarded design.** Original
governed-input copy/independence and admission assertions remain unchanged.

No string-view factory, constructor, ownership transfer, field-size change or
alternative representation remains. Exact R53 production is restored; forced
nonincremental rebuild and focused restored checks are recorded separately below.
Full retained-runtime qualification is R53's cached
[CI42393422539](../2026-11-07-admission-publication/delivery-ci.json), excluding this
withdrawn prototype. Publication postponed; broader performance work remains open.

[Decision](decision.json), [baseline](baseline.json), [prototype checks](focused.json),
[restored checks](restored-validation.json), [allocation/diagnostic samples](local-summary.json),
[quiet summary](b412/comparison-summary.json), [quiet raw](b412/observations.tgz),
[quiet receipt](b412/archive-receipt.json), [failed original](b411/failed-observations.tgz),
[failure receipt](b411/failure-receipt.json), [Ready diagnosis](b411/ready-diagnosis.json)
and [source/core/log/command evidence](development.tar.xz) preserve all conditions.
