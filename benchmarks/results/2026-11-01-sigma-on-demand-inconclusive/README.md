# Inconclusive on-demand sigma-context experiment (B36)

**Invalid baseline; prototype removed. No speedup claim.** One fixed collection
ran against a stale binary from the rejected B35 cursor prototype. Source was
restored with preserved timestamps, so an incremental build reused that output.
Recorded binary identities exposed the mismatch. The comparison is not repeated.

Candidate source is **6d4634f3 + source.patch**; the
[compressed patch](source.patch.gz) preserves its exact bytes. The declared
baseline is ae22392d, but the actual control core SHA256 is
`330bc337320c3bf6fbf2a0c115df728ba7a103fb366709c61c9f44e461210b24`,
byte-identical to the [B35 candidate](../2026-11-01-iteration-cursor-rejected/README.md).
Its missing CheckedEnumerator also contradicts restored source. Candidate SHA256
is `23c09f47afe52a70e5ee0c59e7436f9b8cd7b1eb0d7c0931055fc89dceac9666`.
Original records remain unchanged; this correction supersedes their source label.

## Algorithm assessed

The [B29 CPU capture](../2026-11-01-current-text-cpu/README.md) attributes 10.7%
of text guest self samples to Unicode sigma-property lookup. Existing casing
tracks preceding context at every rune, though only an actual uppercase Sigma
uses it. The prototype deletes four context variables and their per-rune updates
from lower/capitalize/swapcase/title, then scans the original UTF-8 prefix only
at Sigma. Existing forward context checks, property data/lookups and title's
separate word-boundary state remain. This differs from the rejected bulk-ASCII
and property-table replacements.

The proposed algorithm has linear work: Sigma is cased and non-ignorable, so
neighboring context scans stop at it. Each intervening ignorable run is visited
at most once per direction. No index, property cache, conversion, storage owner,
alternate backend or configuration is added. Both directions check cancellation
per visited rune, and the existing output builder/admission/rollback remain.

## Fixed observations, all unusable for a causal comparison

2026-11-01, dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB,
pinned .NET 11 RC3. The existing diagnostic uses four warmups then seven batches
of 128 complete scripts, ordinary GC/tiering. Warm execution includes file work,
full stdout/file goldens and declared cleanup, excluding compilation/startup.
All six processes join in **46.589 seconds**, the owned collection in **47.993
seconds**, within the fixed 30-second/process and 90-second aggregate limits.

| Complete script / metric | Invalid B35 control | Casing candidate | Identical invalid control |
| --- | ---: | ---: | ---: |
| Text CPU ms/job | 6.39550 | 5.82438 | 6.33192 |
| Text allocated B/job | 5,321,993.5 | 5,518,741 | 5,322,031.75 |
| Text wall ms/job | 6.81724 | 6.27347 | 6.79921 |
| CSV CPU ms/job | 7.56266 | 7.79218 | 8.15017 |
| CSV allocated B/job | 9,378,461.188 | 9,542,207.563 | 9,378,254.188 |
| CSV wall ms/job | 8.55940 | 8.55323 | 8.56799 |

The allocation difference first exposed that the control retained B35's different
iteration implementation. Independent CSV CPU control drift also exceeds the
predeclared 5% bound. Quiet-machine gates, complete goldens and cleanup pass;
these cannot rescue a source mismatch or CPU validity failure. No interpretation
of these rows as an algorithm improvement or regression is supported.

[Original criteria](pre-code-plan.md) required at least 5% lower text CPU against
both controls, no more than 2% allocation increase, bounded CSV regressions and
valid identical controls. Reject/restore on any validity failure, without retiming.
Those stopping criteria are honored; no algorithm fragment remains in production.

## Corrected restoration and useful coverage

All three runtime files are restored byte-for-byte. A **nonincremental Release
solution rebuild** passes with zero warnings/errors. [binary-audit.json](binary-audit.json)
checks the stale control identity and restored output structure, then verifies
that built benchmark, public/native test and probe copies match the newly built
core for their target. Restored binaries have CheckedEnumerator and lack the
on-demand sigma helper. The prior B35 incremental "restored" checks used its
candidate binary; this audit and fresh checks supersede those local claims.

The candidate's focused **205 native / 53 public per actual .NET 11 RC3 and
10.0.8 target** pass. Three independent CPython 3.13.2 public goldens remain for
all four contextual methods: cased/ignorable overlap, supplementary runes and long
ignorable intervals between sigmas, with finite shared allowance and retained
result cleanup. Freshly rebuilt original runtime passes those three cases plus
the B35 mutation/exhaustion/generator case (**4 public per target**) and the
existing **4 bounded-cancellation/disposal cases per target**.

AGENTS.md and tests/README.md now require a nonincremental build after restoring
timestamp-preserving snapshots, before no-build tests or freezing workers. Binary
hash recording remains necessary but cannot by itself prove a source label.
The independent fresh-checkout B35 checkpoint **41273799899** is terminal green
on Windows/Ubuntu. Earlier immutable benchmark tables and canonical ae22392d
NuGet artifacts predate this local-output problem and remain unchanged. No
unchanged full platform/package cycle or pack is repeated for discarded code.

[acceptance.json](acceptance.json), [raw-results.tar.gz](raw-results.tar.gz),
the patch, criteria, original records and candidate/rebuild logs preserve the
evidence and correction. Workers/collectors have joined, the service is inactive
and the VM lease available. Stop this experiment; no retiming or performance claim.
