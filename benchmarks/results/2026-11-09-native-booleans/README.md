# Native Boolean results and singleton identity

R135/B495, 2026-11-09. Committed9db554b3/core36fd versus candidate76d.
Native comparison/truth, Boolean bitwise and core predicate results use the typed
RuntimeValue entry and two immutable CLR Boolean boxes. Arbitrary custom/reflected/
NotImplemented result objects are preserved; numeric representation and host
identity checks stay. No new runtime owner, value hierarchy, policy or public API.
Minor fixed scalars continue using existing metadata headroom. Allocation reductions
do not imply lower admission estimates.

**Retain removed construction, allocation savings and the Python identity fix.**
Actual warm allocation falls9.09% for validatedCSV and11.54–11.66% for search,
with control variation within0.022%; compilation allocation is unchanged.
66 typed Boolean-to-object source conversions are gone at the selected native
result boundary; source semantic analysis validates567 trees. Emitted core IL has
65 fewer Boolean boxing sites (475→410), including two new one-time static boxes.
Cold module exits still box; this is not a claim that every CLR Boolean box is gone.

The change uncovered existing inconsistency: is compares Boolean values, while
id() used CLR-box references. Identity keys now canonicalize booleans from literals,
modules and hosts. Original identity-key behavior fails both APIs in the new test;
that red run uses the Boolean-result candidate, not an entire9db baseline replay.
Independent CPython output and the failure are preserved. Equal integers keep
distinct Boolean identity; raw custom comparison results retain their own identity.
97 internal/385 public focused checks pass per actual.NET11/.NET10, including genuine
async coercion, cancellation/first refusal, retained aliases and shared-budget reuse.
Two new API identity cases; no existing assertions/timeouts changed.

Desktop elapsed is diagnostic: report+8.31..+12.56%, search+6.04..+13.53% and async
text+14.07..+33.75%, plus unchanged control drift up14.72%, remain preserved.
Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering, same frozen worker/catalog/
goldens,5swarmup perrole/job,7rotated150msrounds, four independent process roles:

| Complete script / API | Elapsed change | Wider process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| Validated CSV / warm | -0.40..+1.79% | -3.13..+3.33% |0.79% /1.38% |
| Search / warm | -1.79..+2.01% | -3.69..+4.15% |2.03% /1.73% |
| Text / warm | -2.23..+2.55% | -1.22..+1.23% |2.33% /2.39% |
| JSON / warm | -4.03..+1.65% | -3.53..+0.00% |4.90% /0.73% |
| Report / warm | +0.11..+2.44% | -1.79..+6.25% |1.26% /1.03% |
| Validated CSV / warm-async | -3.03..+0.57% | -6.25..+0.00% |3.52% /0.19% |

No demonstrated general CPU or elapsed gain. Report has small possible elapsed
regression debt with closer controls; CSV/search cross zero, JSON/asyncCSV controls
are variable. Large desktop adverse results are not consistently reproduced;
original results remain. CPU includes reset/protocol/validation outside execution
clocks and coarse ticks. No unchanged confirmation selected, not permanent closure.
Warm includes guest mediated RAM-file I/O, full validation and prior-result cleanup;
compile/startup/reset/protocol/final cache teardown are outside. Compilation is
separately measured. No CPython relabeling or embedding-tail/GC-latency claim.

Six jobs169.328s under240s finite ownership. All source/core/auxiliary/golden/
balance/quiet/Closed/join/RAM-unmount/archive-under-lease/local-SHA gates pass.
Archive SHA1185652e9315380834bdcef538159ba1c7d7bcf1bd06acfb06850cd8ad5c3858.
No space recovery/floor relaxation; catalog retained persistently, outside RAM.
Terminal cached, no users/fixtures. Initial six-case local catalog was rejected
by the existing five-case driver before images/users started. Preserve that
condition and correct the catalog, reusing the driver unchanged.
Full platform/stress/package delivery is a separate coherent checkpoint; publication
remains postponed. The larger performance objective remains active.

[Identity](identity.json), [focused validation/original failure](focused-validation.json),
[independent Python identity golden](cpython-identity.json), [construction/IL proof](boxing-proof.json),
[compilation](compile-assessment.json), [warm diagnostics](warm-assessment.json),
[quiet comparison](assessment.json), [terminal receipt](terminal.json),
[initial diagnostic correction](diagnostic-correction.json),
[quiet raw observations/both exact cores](observations.tgz),
[all changed source/local diagnostics/failures/source tools/exact cores](source-and-local-diagnostics.tar.xz).
[Pre-change allocation attribution](../2026-11-09-runtime-allocation/README.md).
