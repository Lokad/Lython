# Typed integer creation — B196 withdrawn

2026-11-04. Both prototypes are withdrawn. Production retains its original boxed
BigInteger creation and ownership paths; no small-integer table, typed creation
policy, truncating-index lookup or new allowance remains. The independent public
alias/value/ownership regression remains. Whole-script performance work continues.

Baseline production 2d46dc8e, fully qualified through test repair 5d8c7239, frozen
`ba48576af397ef62ce62a493b7ab4eb2ff845874fe44c512e6e5ceadf8e63783`.
Initial `a6b2bfb45d47c5d74560add06ebcc1b816de494415f1d804b4c472f63fe60447`
and refined `71406c3f783cc0c5793540fc9c0f5931ad88e1cec8ae83b56b4a2ff845005b1d`
are built from 3e863f40 plus their respective exact patches. Actual binary/source
identities remain distinct from later evidence-only commits.

## Design and source evidence

Move fresh integer creation ahead of object transport, share a fixed immutable
−5..256 constant table in existing PyNumberOps, and remove OwnHeapInteger forwarding.
Arithmetic, int/text/JSON parsing and range/enumerate production use the boundary;
existing-object aliases never canonicalize. Boxed BigInteger, exact promotion,
major-magnitude preflights and owner lifetimes stay intact. The fixed table would
add about 11 KB of immutable runtime/control headroom, not a growing guest cache.

An untimed CPython AST/JSON-number probe verifies all five original fixture/output
goldens: CSV/JSON each have 4,096 small parsed values but only 414/6,144 small
arithmetic results; text has 1,280/12,288 small arithmetic results. This is a value
distribution, not a Lython allocation or CPU profile. It justified miss-heavy
whole-script and above-range counter controls before implementation.

## Initial complete observations

Five complete scripts plus small/above-range controls, both APIs, two independently
warmed unchanged baseline roles, 3-second warmups and nine rotating ~200 ms batches.
All 14 jobs, original quiet gates, hashes/goldens, balances, normal closes, exit zero
and joined users pass: 273.583 seconds capture / 274.196 seconds owned service.

| Initial result | Versus baseline roles | Baseline-role spread |
| --- | ---: | ---: |
| Text warm elapsed | −4.27% / −3.02% | −1.29% |
| Text async warm elapsed | +1.09% / +5.36% | −4.06% |
| Above-range counter warm elapsed | +4.47% / +3.44% | +0.99% |
| Above-range counter async warm elapsed | +5.97% / +4.14% | +1.76% |

Allocation falls 2.78%/2.60% for CSV sync/async, 4.05% for JSON, 1.64% for text,
0.69% for report and about 50% for the small-value loop. Casing-fixture allocation
falls 13–15% through its range values; its script is unchanged but it is **not an
unaffected execution control**. Above-range allocation falls only 1.51% from the
small prefix of range values. Other complete-script elapsed points are mixed or
control-limited; all remain in the initial summary/raw archive.

## One bounded refinement

Untimed local Windows JIT inspection finds a 351-byte Tier1 BigInteger boxing
helper with generalized comparisons/conversion and call-frame work. This is not
VM CPU attribution. Two initial diagnostic protocol/layout errors are preserved;
correction uses the existing verified-worker protocol and required temp parent.

The refinement uses managed int.CreateTruncating only to propose an index, checks
exact BigInteger equality on possible hits, and permits inlining. Large positive
and negative low-bit collisions remain exact. Four images include the original
prototype, refined source and two unchanged B194 roles. CSV/JSON sync, text both
APIs and above-range controls both APIs use 5-second warmups, thirteen rotating
~200 ms batches and the original quiet gates after owned cleanup settles.

| Refined elapsed | Versus baseline roles | Baseline-role spread |
| --- | ---: | ---: |
| CSV warm | −1.20% / −1.07% | −0.13% |
| JSON warm | +0.14% / +0.41% | −0.27% |
| Text warm | +0.40% / −1.99% | +2.44% |
| Text async warm | −0.86% / −0.29% | −0.57% |
| Above-range counter warm | +5.66% / +5.75% | −0.08% |
| Above-range counter async warm | +3.39% / +3.49% | −0.10% |

All six four-image jobs, separate eight-case allocation checks, unchanged gates,
goldens/balances/normal closes/joins pass: 248.644 seconds capture / 249.252 seconds
owned service, above the approximate 230-second estimate and below the 300-second
bound. Allocation direction repeats. Counter IQRs are 1.61%/1.52% in the refined
image versus 0.7–1.1% in baseline roles. Original prototypes also remain adverse
in this same comparison. External process CPU includes reset, transport, JIT and
GC, with clock quantization; it is not guest-only CPU proof.

**Decision:** withdraw both designs. Modest representative allocation savings and
mixed script timing do not justify a creation lookup on every miss-heavy counter
operation. The refinement does not remove that demonstrated cost. This is a
concrete tradeoff, not a rigid percentage veto or a permanent ban on cheaper scalar
representations. No additional unchanged retiming is planned. A future redesign
must remove transport/boxing work without inheriting this per-result policy cost.

## Validation and boundaries

Initial source passes 281 internal / 1,058 public focused Release checks on each
actual .NET 11 and .NET 10 runtime. Refined source passes 154 affected public cases
per target, including large low-bit collisions, cancellation, terminal refusal and
shared-budget cleanup. The retained independent public regression passes both cases per actual target on
restored production after a nonincremental rebuild; unchanged runtime qualification is not repeated. No prototype
is claimed fully platform/stress/package qualified. Publication remains postponed.

Existing protocol 2 clocks include invocation, mediated RAM-file I/O, full checks
and previous-result disposal; reset/staging/startup/response hashing/final cached
teardown are excluded. All users and fixture directories are joined/removed. Same
quiet F4as_v7 / Ubuntu 24.04.4, .NET 11 RC3, ordinary GC/tiering. No new CPython
ratios, equal-heap/RSS or embedding-latency claim. Disk cleanup removes only a
verified redundant uploaded tar and inactive generated MSBuild obj storage in an
old checkout; all source, frozen images and measurement evidence remain.

Exact plans, distribution, identities, summaries and raw archives are adjacent.
Initial archive SHA256:
`f83ecbd64bf58463bf8e593668d896b207471f8d93cef596a853c176ba3eef71`.
Refined archive SHA256:
`24c7099d7823b2f07ef720b516d9dd5875642cd597e64fd90fad8f8a234611f7`.
Focused logs, TRX files, local JIT evidence and collector preparation are in
focused-checks.tgz. Next binding source gate is separate from these runtime timings.
