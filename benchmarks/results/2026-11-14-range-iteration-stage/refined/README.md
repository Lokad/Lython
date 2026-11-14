# Range iteration — retained development source

2026-11-14. Frozen `73a1b233` against fully qualified `3bc513b8` / `ca1e2331`.
Three production inputs, 582 total. General cursor creation and object pulls
match baseline exactly after removing the added primitive-pull method. No new
compiler analysis, unconditional integer arrays, frame system, pool or owner.
One explicit range state replaces generated iteration; native numeric loops
reuse their existing working storage. Observed object values remain stable,
independent enumerators and wider integer admission remain. The iterable adapter
has a small allocation cost. Earlier [initial/adverse results](../README.md) stay.

## Decision and complete-script observations

Retain this bounded stage, pending full delivery qualification. Counter elapsed
and CPU gains are demonstrated; generic neutrality is not. Keep materialization
allocation +1.26% and unexplained captured-text process variation as explicit debt.
Do not turn uncertainty into a design ban or claim universal improvement.

Quiet F4as_v7 / Ubuntu 24.04.4 / four EPYC 9V45 / 16 GiB / no swap / .NET 11 RC3;
normal GC/tiering. Same fixed workers, auxiliary assemblies and adapter 0.4.
Four fresh processes/job, full-request warmups, five rotating batches.
B745: six jobs, 233.123 seconds, 0.5-second batches. All source/output/file/budget/
quiet/joined-user/persistent-archive gates pass. No remaining users/fixtures.

| Complete script | B745 elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add counter, warm | −30.59..−27.94% | −31.21..−26.56% |
| Counter, compile-to-result | −27.88..−27.58% | −29.44..−27.46% |
| 256 materializations of range(64) | −3.29..+1.49% | −3.31..+0.61% |
| Captured text | −2.08..+12.91% | −2.41..+11.78% |
| Short/zero helpers | −1.12..+1.76% | −2.44..+0.48% |
| Helpers exposing intermediate values | −1.69..−1.08% | −2.20..−0.74% |

All process-median pairings, not confidence intervals. Counter warm
1.998–2.001 versus 2.777–2.879 ms. Separate fresh-process allocation:
counter 3,215,354→15,360 B/run (−99.522%); CSV effectively flat;
materialization 977,342→989,678 B/run (+1.262%). No final-source CSV elapsed claim.

B745 text candidate medians 1.460/1.287 ms differ 11.88%. This justified one
unchanged-source B746 confirmation: two fully recorded jobs/eight fresh processes,
12-second full-request warmups, five one-second batches, counterbalanced roles;
150.213 seconds/all gates/no remaining users. B746 elapsed −3.43..−0.07%, wider
CPU −3.99..+0.18%. Across all six independent processes per image from both runs,
median-of-process medians: elapsed −1.33%, wider CPU −1.55%. Every process survives
in [combined data](combined-text.json), including the adverse 1.460 ms observation.
Across all observations, elapsed pairing spans −3.94..+12.91%; aggregation is
descriptive and batch lengths differ. Cause remains unresolved; no neutrality or
backend/PGO/hash causal attribution. No further unchanged replay is automatic.

Warm includes mediated RAM I/O where present, output/file checks and result
cleanup; excludes compilation/startup, fixture reset/protocol and final cache
disposal. Fresh adds compile/disposal. Wider CPU includes reset/protocol.
No CPython or cumulative/cross-collection improvement claim.

## Validation and custody

Initial 536 focused actual .NET 11/10 and 2,298 iterator/aggregate consumer checks,
90 isolated CPython 3.13.2 controls and 18 worker goldens remain preserved.
Final refinement adds 142 focused checks, reruns 90 Python controls/18 goldens,
then 22 extreme-state checks (11 each actual framework). Real I/O, suspension,
aliases, first refusal, cancellation and shared-budget cleanup remain covered.
Full platform/original stress/installed-package delivery is separate/pending.

B745 raw SHA256 `0cb44c1b9b595873be66b9eb5a7a9dc8cc211f6b87af7b75ab516d1dee96d12f`;
B746 `78b2e1e12fe8f77c391d8df5c5eab06e32803939f5c0cd9635d266cd96569ebd`.
Archives are independently verified while the RAM lease remains owned, then all
users/fixtures join/dispose. Current images, catalogs/toolchains and 254/252 MiB
floors remain. The committed SHA-proven historical 61 MB upload is retired only
after exact path/inode/UID/link/hash revalidation; its custody evidence is linked
in the parent. All original preflight failures survive. Publication remains held;
broader representative-script and independent regex work continue.

[Identity](candidate-identity.json), [B745](b745-timing.json), [B746](b746-timing.json),
[allocation](allocation-summary.json), [checks](focused-checks.json),
[source/binary/raw/check evidence](sources-checks-and-observations.tar.xz).

This retained source subsequently [passes full43625900419](../delivery/README.md)
at exactfa40d674/frozen73a1b233. Initial pending metadata remains historical;
all adverse/debt observations above remain. The later R364 prototype is excluded.
