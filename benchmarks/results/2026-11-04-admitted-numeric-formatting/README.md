# Admitted numeric UTF-8 output — B188

2026-11-04, retained source; full delivery pending. Core b09b259e plus the source
patch in observations.tgz; frozen corrected SHA256
`cfdedd954ad1c040c3c4b1f17e40fb07816247fad5f045259466b26334b4d31e`.
Qualified B186 baseline SHA256
`4a64c429c3590bc29bb08a42c829fdee354604a8d50c7673bb0843d78b2c8371`.
Later SourceLink metadata can change rebuilt hashes without changing runtime source.

## Change and correctness

Delete FormatIntegerValue and ToUnsignedBaseString: no CLR digit/sign/group strings,
per-digit BigInteger.DivRem or radix StringBuilder. Integer formatting, percent
integers and bin/oct/hex share admitted digit construction and numeric output.
Finite f/F/e/E uses byte-span formatting with a stack precision descriptor. Complex
components retain their real text bridge and share digit/exponent rules; default,
general, percentage and Decimal algorithms remain. No new formatter/cache/pool/owner.
The existing builder/padding and ownership mechanisms remain in use.

Before changing source, allocation-sensitive probes on the committed baseline
showed denied decimal output allocating162,280/169,640B and hexadecimal output
17,060,344/17,067,704B (sync/async). The new checks refuse before proportional
construction, preserve input aliases/first attribution, and return allowance on
cleanup. Host cancellation at scratch admission stops digit construction. This
is not evidence of interruption inside an opaque BCL conversion. Managed radix,
grouping/padding loops cooperate; BCL decimal/float conversion stays synchronous.
Conservative working estimates can reject earlier; arbitrary tiny-budget parity
is not a contract. No hidden cap or default allowance.

Actual runtime probe: .NET11 generic constrained BigInteger UTF-8 formatting has
zero allocation for4,096 warmed small values; .NET10's inherited interface would
allocate131,072B. Production uses a stack/admitted char bridge on.NET10 instead.
Both actual targets pass130 focused internal and1,031 public tests. Existing
protocol/suspension/refusal/alias/cleanup assertions are retained. Ten independent
CPython3.13.2 cases add grouping of sign-aware padding zeros through three format
entry points, both invocation APIs, with ownership cleanup/budget reuse.

## Preserved failed condition

The initial independent numeric golden failed on the unchanged baseline: zero
padding was not grouped (`000001,000` versus Python's `00,001,000`). Eight of ten
focused parity cases also failed before correction. Fix the shared direct numeric
output; time comparable controls without that previously incorrect construct.
The initial five completed script timings, old source and failing catalog remain
in failed-golden-observations.tgz, SHA256
`9a6e6d27333cbe163013dde4153d2339a0e7207995f73447dd16d5733cb28689`.
That incomplete42.322s run is not pooled with the corrected run. Initial prototype
core was dbf0c78d61eb1c0d8ccd42e333f7ccb8e93f4ae58c9f32c73cab78e4cd94f22f.

## Complete-script pair and controls

Existing protocol2 on quiet Azure F4as_v7/Ubuntu24.04.4, .NET11 RC3, normal GC/tiering.
Five complete CSV/text/JSON/report/config scripts plus three numeric controls,
both invocation APIs; CSV/report compilation separate. Persistent workers/evidence,
tmpfs guest files. All goldens, image hashes, quiet gates, ownership balances,
fixture removals and worker joins pass. No abandoned work. Owned181.501s,
collection180.866s (see raw record for exact value). Positive means slower.

Three workers/job old/new/old,1.5s warmup, seven rotated~150ms batches,.5s settle.
Warm clocks include actual mediated I/O, complete output/file verification and
previous-result cleanup. Fixture reset/staging, startup and final cached teardown
are excluded; final cleanup is joined. Compilation has its own boundary. No
suspended-I/O, CPython, RSS or embedding-latency claim. Systemd peak is not RSS.

| Lane | Script/control | New/old1 | New/old2 | Old control |
| --- | --- | ---: | ---: | ---: |
| warm | csv-summary-helpers | -2.98% | +2.53% | -5.37% |
| warm | text-summary-helpers | -5.83% | -3.26% | -2.66% |
| warm | json-summary-helpers | +1.09% | -2.25% | +3.42% |
| warm | report-render | -8.34% | -8.49% | +0.16% |
| warm | config-edit | -15.18% | -11.82% | -3.80% |
| warm | numeric-integer | -5.70% | -1.52% | -4.24% |
| warm | numeric-floating | -16.57% | -12.50% | -4.65% |
| warm | numeric-base-complex | -87.80% | -87.77% | -0.30% |
| warm-async | csv-summary-helpers | -1.03% | -4.60% | +3.75% |
| warm-async | text-summary-helpers | +2.96% | -3.44% | +6.64% |
| warm-async | json-summary-helpers | -1.30% | +4.04% | -5.13% |
| warm-async | report-render | -2.38% | +2.54% | -4.80% |
| warm-async | config-edit | -7.80% | -9.09% | +1.42% |
| warm-async | numeric-integer | -7.24% | -15.02% | +9.15% |
| warm-async | numeric-floating | +25.77% | +31.00% | -3.99% |
| warm-async | numeric-base-complex | -86.02% | -84.53% | -9.60% |
| compile | csv-summary-helpers | -5.37% | +3.15% | -8.26% |
| compile | report-render | -2.72% | +0.40% | -3.11% |

Separate allocation/peak diagnostic: three isolated Lython images share process GC,
CoreLib/hashing;32 warmups and five16-job batches, both APIs, exact full goldens.
Report allocation−6.13/−6.11%; integer control−18.42/−18.41%, float−27.75/−27.73%,
base/complex−86.68/−86.67%. Other complete jobs near flat. These short shared-process
elapsed values are diagnostic only. Admitted peaks are preserved in summary.json.

Initial numeric-floating async median+25.77/+31.00% is adverse and retained.
All three workers fall sharply through samples (old1.345→.628ms,
new.956→.562ms, otherold1.351→.589ms); IQR34–61%. This justified one bounded
same-binary confirmation with explicitly longer warmup, not a failed-design verdict.
Report/config/compilation control movement also limits broad elapsed claims.

## Same-bit confirmation and decision

Two NEW controls versus OLD candidate, roles swapped. Four targeted jobs: report
and numeric-floating, both APIs. Five-second warmup, nine rotated~250ms batches,
one-second settle. Owned98.760s; all goldens/gates/balances/cleanup/joins pass.
Keep separate from initial sampling. CPU is external process ticks10ms resolution.

| Lane | Script/control | New1/old | New2/old | New control |
| --- | --- | ---: | ---: | ---: |
| warm | report-render | +1.26% | -1.77% | -2.99% |
| warm-async | report-render | -2.95% | -3.42% | -0.48% |
| warm | numeric-floating | -6.99% | -6.23% | +0.82% |
| warm-async | numeric-floating | -7.43% | -7.87% | -0.47% |

Float controls show−6.2–7.9% elapsed, similarly lower process CPU, with control
spread below1% and IQR approximately0.4–1.8%. No persistent async float regression
is demonstrated; the original adverse profile is not erased or causally explained
beyond its observed settling. Large-base control improves approximately85–88%
against both old roles in initial sampling, matching the removed per-digit work.

Complete report: allocation−6.1% is established. Sync timing straddles a3% control
spread, so no demonstrated sync CPU gain. Async elapsed−3.0–3.4% with~0.5% control,
2.5–3.7% IQR is modest; process CPU is near flat. Config initial IQR19–29% precludes
its apparent general speedup. Other complete jobs/compilation are near moving
controls. Retain for admission correctness, simpler representation and allocation/
numeric gains; do not claim a broad CPU improvement or close fresh-job debt.
CSV+1–2% and fresh JSON+1.8–1.9% remain accepted/open until a direct coupled check.
The CPython checkpoint remains B184, not this source pair. Whole-script goal OPEN.

observations.tgz SHA256:
`0597f6c77f0f19c00789265d19d03fc02d770b2aba9a6f11e7f0212ded285e0e`.
stabilization-observations.tgz SHA256:
`0692bd54a577c2a43fd7217bd083e98c4dc6d6994b79b99043fd75ced02f3244`.
Raw catalogs/source/worker identities/samples/leases/cleanup/gates are archived;
source remains tied to its actual frozen precommit image. Publication postponed.

## Remaining known formatter limitation

A follow-up independent probe confirms legacy general/percentage float and Decimal
paths still group before adding sign-aware padding zeros. These algorithms were
not migrated by B188; the direct integer/fixed/scientific correction does not cover
them. See remaining-grouping-probe.json. The next implementation milestone should
consolidate their layout with the corrected native path, preserving real rounding
and complex/custom consumers, admission, cancellation and cleanup. No broad claim
that all grouped numeric formatting is corrected.
