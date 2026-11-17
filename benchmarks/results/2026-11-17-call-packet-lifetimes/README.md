# Caller scratch and retainable bound prefixes

R489/B905,2026-11-17UTC. Committedadd2c00a/frozendfc4c83c versus fully
qualifieddbdf8262/frozena95ff2a4. Four production files;585production/2382inputs.
Corrects both [cleared bound prefixes](../2026-11-17-call-copy-ownership/delivery-first-failure/README.md)
and [universal packet retention](../2026-11-17-owned-call-contract/delivery-failure/README.md).

Ordinary call arrays belong to their caller through actual completion. Admit major
copies before allocation, preserve funding/references through pending users and
awaited cleanup, then clear/refund. Existing small-packet control headroom remains.
Prepared constructors keep their scratch through all stages; existing initializer
code copies real escapes. Fresh non-function bound receiver prefixes separately
promise retainable arrays: their major capacity remains charged to the existing
execution/result owner after return, failure or cancellation.

Remove blanket AllocateOwnedCallArguments/universal ordinary-packet retention and
bound-prefix pending-owner state. The borrowed completion wrapper remains because
actual suspended scratch users need it. No new lifetime layer, classification
framework, cache, tracing, quota, retry, fallback or abandoned work. Public API
unchanged. Retained major prefixes can deny earlier until owner cleanup; ordinary
cache hits/caught invalid factories must remain bounded.

All nine existing regression files implicated by the two failed deliveries are
unchanged from qualifieddbdf8262. New tests imposing universal retention/exact
small-copy admission were removed; six independent copy cases preserve major
preflight/refusal, pending funding/cleanup, temporary refunds, bound retention and
owner-end reuse. Final nonincremental Release build; actual Windows11/10 each
pass all4830nonstressinternal and200focusedpublic. Two actual copied32file workers
pass catalog goldens versus CPython3.13.2 and owner-reuse allocation diagnostics.

| Complete workload / phase | Elapsed change | Wider CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| Bound-wide max diagnostic / warm | -1.82..+0.68% | +0.32..+2.16% | 1.46/1.08% |
| Same / compile and run | +0.14..+4.68% | +2.00..+4.75% | 2.68/1.81% |
| JSON-lines helpers / warm control | +0.21..+2.26% | -0.74..+2.21% | 1.29/0.74% |

Bound allocation is identical; accounted peak3079→298279B adds exactly295200B
for1025 retained288B receiver prefixes. JSONallocation+64B/peakunchanged. All owner
baselines restore. These observations do not establish a precise CPU benefit or
cost; small adverse JSON/fresh observations remain explicit. Retain the required
funding/retention correction without an unchanged timing campaign. Ranges combine
four independent process medians, not confidence intervals.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering;3jobs117.58seconds.
Six-second rotated warmups/five samples/four processes. Mediated I/O, full goldens
and prior-result cleanup included; compilation only fresh. Startup, fixture reset/
protocol and final cache teardown excluded; wider CPU includes outside-clock
request work. All quiet/golden/Closed/exit/join/archive gates pass; RAM archived and
SHAverified before unmount. Terminal cached. Staging retires only exact committed
failed6c core/raw/archive and withdrawn B902catalog, with fresh lease/path/UID/hash/
link/all-alias proof. Unmatched older archives remain untouched. Qualifieda95,
toolchains/canonicals and254/252MiB floors preserved.

CI44103214418 qualifies exactadd2c00a across Windows/Ubuntu actual11/10, original
stress and installed Release package consumers; currently pending. No publication.
R488 synchronization prototype and substantial call/frame/record/text/frontend
roadmap remain independent; this correction does not finish performance.

[Decision](decision.json),[sources](candidate-sources.tar.xz),[catalog](catalog.json),
[summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[identity](identity.json),[checks/procedures](procedures-and-checks.tar.xz).
