# Immutable dev105 regex development qualification

R443/B846,2026-11-16UTC. Compare the earlier immutable0.5producer qualified under
R409 against0.5.0-dev.105. Both use identical R409native adapter source,qualified
0443core and unchanged comparison workers. Only the two producer assemblies and
necessary adapter-reference recompilation differ. This is development qualification;
the normal nonpackable adapter remains0.4,and core has no producer dependency.

| Complete warm script | Elapsed change | Wider process CPU change | Ref/candidate elapsed control spread |
| --- | ---: | ---: | ---: |
| Extract512named integer captures | -0.76..+1.64% | -0.18..+1.46% | 0.30/2.12% |
| Rewrite512named captures | -8.59..-6.25% | -8.09..-6.61% | 1.92/0.63% |

All four source-process median pairings remain. Rewrite has supporting elapsed/
CPU improvement; extraction is inconclusive with2.12%candidate-control movement.
Allocation is unchanged: extraction341592B and rewrite170432B per local complete
invocation. Accounted peaks174351/21803B are unchanged. No core/CPython gain,
cross-producer attribution to a particular optimization,physical heap or latency
claim. Earlier R409adapter-transport improvements remain a separate source pair.

Both immutable producers pass363public regex checks each on actual.NET11and10,
using matching private parent/child images. [Fixture identity correction](fixture-identity/README.md)
documents the historical private-parent/normal-child gap and three deliberate
hash-mismatch failures. Real current-token compile/match/cursor interruption,
unwind,refused admission,retained owners and budget reuse pass. Core's cooperating-
provider contract tests remain separate from actual-backend qualification.

Retain dev105as a qualified development input,not a normal pin or released adapter.
Native cursor ranges/current advance tokens and program disposal work. Keep the
UTF8flag/construction-token/coarse-storage bridges. Native UTF8name enumeration,
preparation with the current token and separate per-match result-stage allowance
remain precise final adapter limitations. Single-shot matching still uses backend
detailed snapshots; native range-only cursor transport does not imply all producer
string projection disappears. No producer unsafe-policy certification follows.
The official NuGet index still lists only0.1through0.4at the recorded observation;
no newer published asset was assumed. Producer timing does not block core work.

Same two complete Unicode/capture scripts and independent output/file goldens in
every source process. Quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45vCPUs,16GiB/no swap,
.NET11RC3 workstation GC/normal tiering. Four-second full-request warmups,five
rotated approximately0.5s rounds. Warm includes Run,output/file goldens and prior-
result cleanup; excludes compilation/startup,fixture reset/protocol and final cache
teardown. Wider CPU includes reset/protocol. The explicit empty file host performs
no guest I/O. Local allocation includes reset/validation/cleanup and uses natural
GC process counters; it does not establish elapsed/CPU. Compilation/startup,
pending-I/O,contention and embedding tails were not measured.

Initial B845fails before Ready because the reused historical comparison catalog
omitted required fixture size/text metadata. No timing observations exist. All
workers joined; failure/journal and every RAM byte were archived/verified before
unmount. B846uses the actual corrected historical catalog: only those unused
metadata fields and their digest change; scripts,inputs,outputs,binaries/settings
remain identical. Both frozen images then pass the existing local comparison
verify command against independent CPython,with no timing claim. This preflight
should precede future VM staging. All initial conditions remain preserved.

B846finishes64.057s; full-image/golden/quiet/Closed/join/archive gates pass. All
users joined and RAM fixtures removed; terminal cached forever. No working-copy
retirement was needed; canonical assets/current images and254/252MiBfloors remain.
Cumulative core Windows/Ubuntu,actual11/10,stress and installed-package delivery
43869939863 is independent and pending at this record. Publication remains held;
non-regex complete-script execution gaps remain unresolved.

[Raw](observations.tgz),[medians/controls](summary.json),[allocation](allocation-summary.json),
[asset/worker identity](identity.json),[procedures/images/preflight](procedure-and-images.tar.xz),
[initial failure](initial-failed-observations.tgz),[published-version observation](published-observation.json).
