# B1533 keep owned module slots through plain class declarations

Retained `0112138c1` keeps the module's existing slots when a public
script declares a bare class with only plain method definitions. The
previous mirror published all module scalar bindings, including the
private integers returned to the invoice's unpack operation. Calls
using an already-owned context keep their live dictionary. Imports
retain their published namespace, and `globals()` can still expose the
namespace and its aliases. The correction preserves all existing
reusable-context tests. The measured source is `a6b80dd3d`, against
`48bf2bc2f`. [Initial prototype](../2026-11-23-plain-class-module-slots/README.md).

| Script and boundary | Change in elapsed | Change in process CPU |
| --- | ---: | ---: |
| Quote-heavy CSV invoice, warm | −11.18% to −9.79% | −11.11% to −9.52% |
| Quote-heavy CSV invoice, compile to result | −21.96% to −9.96% | −22.90% to −8.89% |
| Small configuration edit, compile to result | −0.33% to +1.74% | −0.35% to +0.82% |

The ranges span all four process-median pairings. Warm spread remains
below 1.17% per image. The 14.08% reference spread limits the estimate
of the compile-to-result change. Warm allocation falls from 3,938,470
to 3,281,558 bytes per invoice, a 16.68% reduction. Its admitted peak
rises 608 bytes to 9,433,482. The six other admitted peaks match and
all budgets recover after cleanup.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on November
23, 2026 UTC. Warm includes mediated RAM-backed I/O, verification and
prior-result disposal. Compile to result adds compilation and code
disposal. Process CPU includes request handling. Each worker contributes
seven rotated batches after eight seconds of warming. All 84 batches,
the full image hashes, quiet gates, worker joins and fixture cleanup
verify. Initial staging stopped at its disk floor. The completed run
used the same floor after verifying and reclaiming two redundant copies
whose exact core images and observations were already archived.

Seven scripts match CPython goldens in both frozen images. Windows .NET
11 and Ubuntu .NET 10 each pass 1,089 public and 1,071 white-box focused
Release checks. The new namespace regression covers class methods
reading and writing module bindings before and after a live `globals()`
view, including identity and deletion.

[Raw observations](observations.tar.xz) · [Timing](summary.json) ·
[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Source, verification and tests](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
