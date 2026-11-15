# Final-slot integer tuples — retained refinement

2026-11-15. Frozen **22e5b72c** against qualified b58e2dc4/f304b3db.
[Full Windows/Ubuntu11/10, original stress and installed-consumer qualification passes](../delivery/README.md); publication remains postponed.

The original [1b88 source and adverse results](../README.md) stay preserved.
That source grew the common slot accessor from 42 to 89 IL bytes. The refinement
keeps tuple observation in small publication helpers, clears completed local
publication state and guards borrow handling before type classification. The
accessor is again 42 IL bytes. This is an emitted-code fact, not proof of actual
JIT inlining or a complete causal explanation. No hint, option or execution route
was added. Integer tuples still reuse the shared managed slot layout and preserve
one canonical object identity on observation.

| Complete script | Allocation change | Quiet-VM elapsed | Wider process CPU |
| --- | ---: | ---: | ---: |
| Invoice | −8.56% | −10.10..−8.55% | −10.81..−6.42% |
| Fulfillment | −5.32% | −5.01..−3.85% | −4.62..−2.67% |
| Captured text | 0% | −9.45..−0.99% | −9.63..−0.75% |
| JSON summary | effectively flat | +0.47..+1.16% | −0.58..+0.70% |

B782: four jobs/170.146 s, quiet F4as_v7 Ubuntu 24.04.4/.NET 11 RC3, ordinary
GC/tiering, same catalog/goldens/host/API. Two processes/image, five rotated 0.5 s
batches; full-request warmup is 12 s for text and 5 s otherwise. Ranges retain all
four process-median pairings. Text reference spread **8.90%** makes its magnitude
uncertain. B783 reuses the same images for one justified text/JSON confirmation
(101.411 s): text elapsed **−7.94..+0.88%**, CPU −9.23..−1.18%; JSON elapsed
**−2.39..+0.32%**, CPU −3.34..+2.00%. Text reference/candidate spread is 5.40/3.97%.

Retain targeted CSV benefit and allocation reduction. Accept remaining small
text/JSON control uncertainty for 0.11; no portable text gain, proven neutrality
or claim that the original regressions were causally resolved. No more unchanged
tuple timing campaign is selected. New defects or materially different evidence
remain eligible. This milestone does not complete the whole performance objective.

Elapsed includes synchronous `Run`, mediated RAM-file I/O, complete goldens and
prior-result cleanup. Reset/staging, protocol, compilation, startup and final cache
teardown are excluded; wider process CPU includes reset/protocol. Allocation is
measured separately on Windows using the unchanged complete-job driver, including
reset/I/O/goldens/result disposal. No forced GC, heap/RSS or pending-I/O/tail claim.

Focused Release passes on each actual .NET 11/10: **418 public / 173 internal**,
plus unchanged-reference/candidate walrus, closure, wider-tuple and helper-call
alias controls under both APIs. Current tokens, eager refusal, conservative
capacity/eventual-box admission, retained owners and awaited I/O cleanup remain.
A private scalar borrow may retain its source tuple until observation/retirement.
Full Windows/Ubuntu, original stress and installed-package delivery passes as linked above.

All VM identity, golden, quiet, balance, Closed/exit/join and archive gates pass.
No users/RAM fixtures remain. The original VM prototype image is retired only
after exact committed-archive/hash/all-alias/UID/path/idle-lease proof; reference
images, evidence, toolchains and disk floors stay. Initial malformed metadata
and original timeout/capture conditions remain in linked earlier evidence.

[Decision](decision.json), [B782](quiet/summary.json),
[B783](confirmation/summary.json), [allocation](allocation-summary.json),
[frozen identity](candidate-identity.json),
[sources, binary, checks and procedures](sources-checks-and-procedures.tar.xz).
