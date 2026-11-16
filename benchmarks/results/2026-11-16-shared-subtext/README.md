# Shared immutable subtext storage

R435/B832,2026-11-16UTC, retained **1c8e1be5** versus qualified
**86d9344e /ecd9e956**. Full delivery pending; publication held.

Ordinary slices and split fragments now use the same admitted immutable UTF8
window representation as JSON. This removes their independent payload arrays and
copies. Aggregate split construction admits list/fragments/header together before
allocation; source backing/header stay funded after private shell cleanup. Short
inline values, transformed output and foreign/unowned input keep independent
construction. No new owner, cache, tracing, ledger or execution route.

**Tradeoff:** a small surviving view conservatively keeps the whole backing until
its existing owner ends. Earlier rejection from sibling retention is acceptable;
no exact tiny-budget compatibility. Source sharing metadata can stay charged after
an interrupted private construction; all scope cleanup still returns to baseline.
No retained backing is refunded while an alias or pending user can access it.

| Complete workload / phase | Elapsed candidate/reference | Wider CPU | Ref/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Normalization warm | −4.62..−1.84% | −6.02..−0.95% | 0.05/2.86% |
| Normalization compile-to-result | −4.97..−1.15% | −4.30..−0.13% | 3.37/0.63% |
| JSON-lines helpers warm | −5.57..−2.86% | −5.56..−1.59% | 1.40/1.45% |
| Unchanged normalization compilation control | −1.99..+0.67% | −2.64..−0.14% | 1.52/1.18% |

All four process pairings, not confidence intervals. Five rotated approximately
0.5s batches after5s full-request warmup(8s compile), two processes/source;
151.601s/all image/golden/quiet/Closed/exit/join/archive gates. IQR≤1.65%.
Fresh reference moves3.37%; exact magnitudes remain diagnostic. No compiler gain
is attributed to unchanged frontend code. Source-pair gains cannot be added to
earlier experiments or inferred from the separate CPython collections.

Local natural-GC whole-job allocation: normalization **−19.8%**(both hosting APIs),
JSON-lines **−11.3%**, protected search unchanged. Accounted peaks fall2,515,196→
2,324,834B and4,060,103→3,954,823B respectively; these are conservative estimates,
not physical heap/RSS. JSON-lines is affected through ordinary line subtext, not
an unchanged workload control. No async CPU/startup/embedding-tail claim.

Same F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
normal tiering/workstation GC. Warm includes mediated RAM I/O, full stdout/file
goldens and prior-result cleanup; excludes compilation/startup, reset/protocol and
final cache teardown. Fresh includes compilation/compiled-owner disposal; wider
process CPU includes reset/protocol. Separate phase medians are not additive.

Focused actual.NET11/10 each **548internal/3,619public** pass. Coverage includes
Unicode/scalar boundaries, nested slices/splits/trims, hashing, retained backing,
foreign-owner copies, terminal refusal, current cancellation, suspended users and
cleanup/budget reuse. Original new-test ambient scope incorrectly spanned await,
contaminating later white-box tests; correct it to end before suspension, preserving
all assertions. Original5failures/source retained, measured core unchanged after
test-only builds.602corrected production/build/test inputs frozen; original staged
identity retained separately. No unchanged runtime measurement repeated.

Original staging failures stopped before mutation/capture: already-retired old
working archive, a rejected old-core read, Windows command length and unchanged
disk headroom. Correct transport and retire only exact committed completed working
replicas with fresh path/UID/hash/all-alias proof; no floor lowered. Both current
images/catalogs/toolchains retained. Persistent evidence verified before RAM teardown,
all users joined, terminal cached. Broad call/frame/operand work remains open.

[All controls](summary.json),[allocation](allocation-summary.json),
[raw](observations.tgz),[focused checks](focused-checks.json),
[corrected source identity](source-identity.json),[source/TRX/original failure](sources-and-checks.tar.xz),
[exact image/procedures/staging conditions](procedure-and-image.tar.xz).
Baseline auxiliary image is unchanged from
[line construction](../2026-11-16-line-construction/README.md).


[Initial full qualification failed on12obsolete copy-size fixtures per platform;
original failures preserved and corrected tests pass, runtime unchanged](qualification-corrections/README.md).
New full delivery remains pending; this does not change the original measurements.


[Second full checkpoint passes ordinary/platform/package checks but exposes two
original-scale fixtures awaiting removed64MiBcopies; corrected stress passes each
actual11/10 with unchanged runtime](stress-fixture-correction/README.md). Final combined
qualification remains pending; both original failed terminals are cached.


[Corrected full Windows/Ubuntu actual11/10 ordinary/stress and installed Release
consumer delivery passes](delivery/README.md) at6581454d/measured1c8e1be5. Capture-time
pending/failed states remain historical evidence; no runtime retiming required.
