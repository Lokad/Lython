# Isolated development-producer qualification: 0.5.0-dev.7

Qualified 2026-11-01 UTC / 2026-11-02 Paris. **Development qualification passes;
final released-adapter qualification remains pending.** No production source,
ordinary 0.4 pin, normal lock file or core dependency changes. No sibling edits
or package publication. Lython core source is `4cae38f4`; adapter source remains
`d703767c`. Evidence-only checkout revision is `fad771f4`.

Both immutable packages identify producer **2ff58324cb10f04e27699eb01df1d13fadd71c68**:

| Package | SHA256 |
| --- | --- |
| Lokad.Utf8Regex.PythonRe 0.5.0-dev.7 | `b54fd36e256f9283c8b4753786a6a08dff044f014537037a9f0013b2ee8f6fad` |
| Lokad.Utf8Regex 0.5.0-dev.7 | `6b1a87971eaa0a2e5db9851c1892e26709f8d78d3ab0df05d1dec8e0f3698d32` |

Later producer HEAD changes are excluded. Copied local feed, cache, outputs,
intermediates and lock files are isolated under `.git/agent-notes`; no new Lython
package directory is created. Every tested producer DLL is checked against the
exact package asset, including the normal matching net11.0/net10.0 selection.

## Semantics, cancellation and capabilities

Existing focused regex/fnmatch/glob selections pass **84 white-box / 424 public
checks on each actual Windows .NET 11 RC3 / .NET 10.0.8 runtime**. Six additional
owned direct compile/match/cursor probes observe active work at cancellation,
preserve the caller token, join actual work and return allowance to zero. Existing
25-second process bounds and containment assertions are unchanged. Semantic
coverage includes errors, effective flags, windows, offsets, caches, refusal and
retained ownership; cooperating-provider tests remain distinct from real matching.

The same-target public `Utf8PythonRegex` surface has 143 declared members in both
dev.6 and dev.7. Native UTF-8 lookup/pattern copy exist, but native capture-name
enumeration and one-shot ranges with a working-funding receipt remain unavailable.
The existing adapter explicitly rejects unsupported reversed windows. Its bridges
cannot be removed based on this release. Core/test-provider semantics remain
independent; missing producer capabilities do not block Lython development.

The initial narrow public invocation has seven **setup** failures: four missing
workflow fixture directories and three missing private root-relative worker paths.
Copy unchanged fixtures and the actual isolated worker output to the private test
layout, then use the full original regex/fnmatch/glob selection. All checks above
pass. Failed observations remain in the log archive; no runtime assertion was
relaxed and no ordinary build output was replaced.

## Allocation diagnostic: unqualified, not rerun

One existing four-case diagnostic runs dev.6/dev.7/identical-dev.6, with common
current core/adapter/worker bits, ordinary GC/tiering, four warmups and seven
batches of five. Public RunAsync, full return/stdout goldens and result disposal
are included; startup/compilation are excluded. All three processes exit 0 and
are joined. Text benchmark cleanup completes.

**The producer target assets differ:** original/control use net10.0 DLLs while
candidate uses normal net11.0 DLLs. This confounds version attribution. Preserve
all observations and stop; no retiming, allocation improvement or latency claim.
The later same-target API inspection runs reflection only, not guest workloads.

Observed median allocations are 410,779.2 / 410,779.2 / 410,774.4 bytes for split;
245,992 in all images for sub; 203,504 for expand; and 228,168 for text. These are
unqualified diagnostic observations. They do not establish a version effect or
substitute for final released-package backend performance qualification.

[Exact identities, loaded assets, test hashes and limitations](summary.json),
[actual cancellation results](native07-cancellation.json),
[test/setup logs](validation-logs.tar.gz), public API inventories, the frozen plan
and unchanged case/driver artifacts preserve the evidence. Final qualification
still requires a released immutable package, managed-safety assessment and a
focused real-backend comparison with matching target assets. Core releases remain
independent of that producer delivery.
