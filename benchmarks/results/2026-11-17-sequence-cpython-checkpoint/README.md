# Sequence-runtime representative CPython checkpoint

R461/B868,2026-11-17 UTC. Production46343804 / frozen aaef443f versus stock
CPython3.13.16. Subsequent9677d8f7 corrects two accounting tests/evidence only;
full delivery is separate. Reuse existing six-case catalog36c0a528 and unchanged
workers/protocol. Four representative complete CSV,text,JSON and early-exit file
scripts timed; earlier config/report remain explicitly named older measurements.
All six frozen-worker/local isolated CPython preflight cases pass. Every timed
job verifies goldens against the actual VM CPython3.13.16 and loaded core hashes.

| Script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | compile | 0.100–0.101 ms | 0.093–0.094 ms | 1.06–1.08× | Diagnostic; controls L/C 0.62/1.03%; IQR≤0.96% |
| CSV invoice | warm | 6.844–6.963 ms | 6.064–6.114 ms | 1.12–1.15× | Diagnostic; controls L/C 1.74/0.83%; IQR≤1.62% |
| CSV invoice | compile-run | 7.078–7.560 ms | 6.289–6.324 ms | 1.12–1.20× | Diagnostic; controls L/C 6.81/0.55%; IQR≤0.68% |
| Text normalization | compile | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Diagnostic; controls L/C 0.80/0.08%; IQR≤0.79% |
| Text normalization | warm | 1.041–1.049 ms | 0.504–0.504 ms | 2.06–2.08× | Diagnostic; controls L/C 0.76/0.09%; IQR≤2.12% |
| Text normalization | compile-run | 1.132–1.175 ms | 0.631–0.686 ms | 1.65–1.86× | Diagnostic; controls L/C 3.86/8.75%; IQR≤1.64% |
| JSON-lines aggregation | compile | 0.050–0.051 ms | 0.051–0.051 ms | 0.99–1.01× | Diagnostic; controls L/C 1.98/0.13%; IQR≤0.53% |
| JSON-lines aggregation | warm | 3.504–3.579 ms | 4.069–4.080 ms | 0.86–0.88× | Diagnostic; controls L/C 2.16/0.27%; IQR≤1.11% |
| JSON-lines aggregation | compile-run | 3.610–3.725 ms | 4.193–4.197 ms | 0.86–0.89× | Diagnostic; controls L/C 3.17/0.10%; IQR≤0.47% |
| File search / defensive helper | compile | 0.031–0.031 ms | 0.036–0.036 ms | 0.85–0.85× | Diagnostic; controls L/C 0.44/0.02%; IQR≤1.41% |
| File search / defensive helper | warm | 0.259–0.264 ms | 0.160–0.161 ms | 1.60–1.65× | Diagnostic; controls L/C 2.25/0.87%; IQR≤1.10% |
| File search / defensive helper | compile-run | 0.342–0.352 ms | 0.225–0.227 ms | 1.50–1.56× | Diagnostic; controls L/C 2.94/0.95%; IQR≤1.10% |

Above one means slower. Four independent-process median pairings are ranges,
not confidence intervals. Warm normalization retains a2.06–2.08× gap with close
controls; CSV1.12–1.15× and search1.60–1.65× remain slower,JSON0.86–0.88× faster.
Fresh CSV Lython controls move6.81%,fresh normalization Lython/CPython3.86/8.75%;
those magnitudes are uncertain. All adversity and raw controls remain visible.
No causal source improvement is inferred from earlier collections. Earlier
CPython normalization0.73→0.50ms shift and preceding warm control10.85%movement
remain unexplained; this collection does not explain them or erase earlier data.

Twelve jobs399.997s,two independent processes per engine,five-second full-request
warmups,five rotated approximately0.5s samples/process. Frozen32-file image/core
verified; optional adapter0.4 unchanged. Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45
vCPUs,16GiB/no swap,.NET11RC3natural workstation GC/normal tiering,CPythonGIL/GC.
All image/Ready/golden/quiet/Closed/exit/join/archive gates pass. No runtime change
or benchmark-framework/protocol addition for this refresh.

Warm includes synchronous Run,mediated RAM I/O,complete stdout/file checks and
prior-result cleanup. Excludes compilation/startup,fixture reset/protocol and
final cache teardown. Compilation includes construction/compiled-owner disposal;
compile-to-result adds one execution. Independently warmed medians are not
additive. Wider process CPU includes reset/protocol. CPython uses native buffered
files without equivalent capability/shared-budget containment. No startup,
pending-I/O,heap/RSS,contention or embedding-tail claim.

All users joined and fixtures removed; archive SHA-verified persistently under
lease before owned RAM teardown. Terminal cached forever. No new core copies or
retirement; current images/toolchains/canonical evidence and254/252MiB staging/
final floors preserved. No unchanged retiming pending. Publication/tagging held;
whole-script simplification/performance work continues.

[Summary](summary.json),[exact identities](identity.json),[raw observations](observations.tgz),
[procedures/preflight/custody](procedures.tar.xz),[preceding guide](prior-BENCHMARKS.md),
[retained runtime](../2026-11-17-sequence-construction/README.md).
