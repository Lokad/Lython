# Funded development regex programs and cursors

**Development implementation, not a dependency promotion or released-adapter
qualification.** Source **40aaeb4b** plus [source.patch](source.patch) relative to
its preceding A04 development adapter. [development.patch](development.patch)
contains the full optional-adapter delta from the ordinary 0.4 source. Core code,
public provider APIs, normal package pins/locks and packaging are unchanged.
No adapter package is generated. Both observations use the exact same 0.5.0-dev.1
producer; complete assembly identities are recorded below.

## Replaced mechanisms and ownership contract

- Delete the raw-parenthesis scan and fixed 64-KiB/slot/source-length program
  estimate. Fund producer compilation, retained programs/cache and construction
  overlap through its existing caller-funded Compile API.
- Delete cursor input-times-capture scratch prepayment. Pass its existing child
  scope through the producer's working-allowance API. Hold MoveNextRetained's
  receipt until capture bounds are copied, then dispose it.
- Delete per-advance Lython capture arrays. Admit one current buffer/record before
  allocation (64 + 16*slots), reuse it at the next operation, and release it with
  the drained cursor. The public contract makes results borrowed until the next
  operation/disposal; core copies retained Python matches before advancing.
  Independent cursors have independent current buffers.
- Close producer programs before closing their existing owner. Failed/cancelled
  unpublished construction disposes the producer handle before refunding storage.

One small adapter-local IUtf8MemoryAllowance maps incremental producer loans onto
one existing ILythonMemoryReservation, with serialized aggregate growth/release.
Refusal throws LythonRegexException(MemoryAdmission) immediately with its requested
amount; balance is unchanged. Cleanup releases observe no cancellation. There is
no per-loan identity map, guest tracing, new allowance, policy, quota, retry, pool
or background work. Producer callbacks must honor their documented exact pairing.
The shared host allowance remains authoritative; no physical CLR collection is
needed to return relinquished ownership.

Adapter metadata still needs independent admission. After validated
CaptureGroupCount is known, reserve 256 + 128*slots + 4*pattern bytes before
GetGroupNames and UTF-8 copies, including construction overlap. Keep that
conservative estimate until pattern disposal. Native name enumeration is still
unavailable; this temporary conversion is outside core.

## Remaining one-shot limitation

The inspected MatchDetailedData/SearchDetailedData/FullMatchDetailedData overloads
have no separate working allowance or retained-result receipt. Keep PatternBytes,
MatchBytes and subject Fund for those calls. The funded producer also uses its
program account for their working storage. This may double-charge and reject
earlier, especially with separate compile/execution roots; removing that estimate
would underfund published detailed results. No ambient owner swap, recompilation,
algorithm fallback or extra policy is introduced to hide the missing capability.
The producer's fixed construction backstops remain separate; null MaxBytes on
this funded entry adds no private byte cap. Lython adds no work/time policy.

Current-token construction/matching/advancement, Python reported flag-object
behavior, original-subject windows, captures and explicit reversed-match capability
failure are preserved. GetGroupNames and individual CLR operations remain atomic
cancellation limitations. Final released-package/safety/performance qualification
remains separate; the development producer's internal safety is not certified here.

## Exact inputs and validation

2026-10-31, same frozen local packages as A04:

- PythonRe SHA256 `e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
- Base SHA256 `47216c6c75e1e2a6c980058e690e7f595a53525c37ad8efb35d2094c95bf467b`.
- Producer source `0e24cecebbb9093a18f5cb962dae3b6f6426742c`; no public-release claim.

Private MSBuild imports/feed/output/intermediates/packages/locks remain under
ignored agent notes. The [check archive](focused-checks.tar.gz) includes their exact
configuration and logs/TRX, plus the unchanged 25-second process-bound runner.
Reproduction needs those exact package bytes and adjusted absolute private paths;
there is no new supported build system. No sibling source is edited.

Both targets pass **379 public + 84 native** focused regex/fnmatch/glob cases.
The [three new development public-boundary cases](RegexAdapterFundingTests.cs)
exercise actual producer construction/working refusal with the first requested
amount and no retry, program funding surviving failed cursor cleanup, subsequent
budget/program reuse, and independent current borrows across interleaved cursors.
Existing 4,096-advance coverage keeps its unchanged steady-balance assertions and
passes; no cumulative-allocation substitute is used. Existing retained-match,
cache/purge, callback, real suspension and provider-contract checks remain.

All **six actual isolated** compile/match/cursor cancellation probes (both runtimes)
observe work still running at cancellation, propagate the current original token,
join cleanup and end at zero allowance. [cancellation.json](cancellation.json)
binds the actual adapter/producer hashes. Ordinary fixture-discovery tests are
excluded from isolated builds; these probes explicitly run the isolated fixture.
Entry/exit checks alone do not establish interruption.

The first public run exposes one stale fixture: it compiles the same pattern
twenty times and retrieves a single cache entry. Real funded construction fits
that allowance, so the old expected failure no longer proves retained-data
rejection. The production fixture now retains **distinct patterns**, checks
terminal shared-allowance attribution, disposes both API results, verifies zero
balance and runs a successful later command. Its original failure and the corrected
run remain in the archive. This is also verified with the normal 0.4 adapter:
**all three family cases pass on both targets**. No timeout or semantic assertion
is relaxed. No other existing fixture needs adjustment.

## One fixed affected allocation diagnostic

The existing [driver](driver/Program.cs) runs before/funded/identical-before once,
with four warmups and seven rounds of five complete public warm RunAsync jobs.
It validates full return/stdout and disposes results. Each process has a 30-second
bound and completes normally. Compilation/startup are excluded; common reflection
overhead is included. Windows 10.0.26200, 28 logical processors, .NET 11 RC3,
ordinary GC and equally disabled tiering, no concurrent local build/test. Frozen
sources and independent CPython 3.13.2 [goldens](driver/cases.py) are unchanged.
The file/text control uses the maintained 1,000-line discard job.

| Complete shape | Before B | Funded B | Identical-before B | Change |
| --- | ---: | ---: | ---: | ---: |
| capture split, 300 matches / 1,200 bytes | 384,275.2 | 387,035.2 | 384,281.6 | +0.72% |
| template sub, same captures/input | 219,488 | 222,248 | 219,488 | +1.26% |
| 100 match.expand calls | 203,368 | 203,688 | 203,368 | +0.16% |
| complete text-file control | 252,224 | 252,224 | 252,224 | unchanged |

[Before](before.json), [funded](after.json) and
[identical-before](before-control.json) preserve every observation. The split
control's 6.4 B difference is retained. Funding adds about 2,760 B per split/sub
job and 320 B per expand job despite removing current capture arrays; admission/
receipt overhead is an explicit cost. Local elapsed also increases for affected
rows and is unqualified: no speedup, host-latency or absence-of-regression claim.
This migration is accepted as development ownership plumbing, not a performance
optimization. A04's noisy VM comparison is closed: **no new VM timing/capture or
branch retuning** occurs. The primary CPython table is unchanged.

Stop this bounded migration. The exact development implementation is available
for final released-adapter qualification; independent Lython runtime work proceeds.
