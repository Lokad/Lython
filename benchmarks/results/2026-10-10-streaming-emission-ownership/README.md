# Unpublished text payloads on registration denial

A focused ownership probe demonstrates an accounting defect in streaming text
reads. The reader constructs a governed string, advances its position, and
registers the result for reclamation. If that registration is denied, the result
has not escaped to guest code, but its construction charge remains committed.
A four-byte line strands **132 bytes**; a two-byte bounded slice strands
**130 bytes**. Caught failures therefore consume headroom that should have been
returned.

The fix registers these fresh results through the existing `TrackFreshString`
path. It refunds the construction charge when registration fails, while normal
registration continues to track retained values. The reader's progress on failure
stays as before. Production window sizes, limits, host mediation and the
256-emission sweep cadence stay unchanged.

White-box probes use four-byte windows and reserve pressure under an unchanged
**65,536-byte** cap. One headroom setting denies the 128-byte entry fee by one
byte; another funds that fee and denies the initial 32-byte tier growth by one
byte. Line, bounded-slice and accumulated exact-window reads cover synchronous
and asynchronous paths. They assert exact committed/reserved balances after
failure, then release pressure and verify that the next read succeeds.
Retained-line controls check that closing the reader and collecting does not
release a live result's charge.

| Check | Result |
| --- | --- |
| Final baseline Debug probes | 8 rollback failures, 2 retained-value controls pass |
| Baseline Release rollback probes | The same 8 payload-accounting failures |
| Corrected baseline Release retained controls | 2 pass |
| Fixed focused Debug / Release | 22 / 22 pass |
| Fixed public file/stream Release | 43 pass |
| Fixed full Debug | 9,134 pass: 1,383 white-box and 7,751 public |

The initial Release probe also had two control-authoring failures: `IsTracked`
checks global registration identity rather than ownership by the queried pool.
The controls now select the sole nonempty pool. That original receipt remains
preserved separately from the eight demonstrated production failures.
The Debug probe was expanded as coverage was added; all initial receipts remain.

Candidate `a88949c5efd9e34ec246faa2985f751698a96bba` has production tree
`4174256cc6da53c0f2f5cd7d163c2bb47fb0fcf3`, tests tree
`a45507af1ed878492a6907b12bfe04cd678d036a` and unchanged comparison-harness tree
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. The baseline production tree is
`b4c953ccb98d78b0ca56be1510dae243ec38f76c`. The full Debug run builds the matching
probe explicitly after freezing the candidate. Earlier focused builds precede
the commit but use these same production/test contents. Tests use local SDK
10.0.300-preview.0.26177.108 and runtime 10.0.12, without tuning overrides.

This is a demonstrated rollback defect, **not an explanation of the original
C06 streaming failure**. That first-denial cause remains pending, alongside the
original C05 CSV failure. The namespace optimization stays isolated and unmerged.
No timing micro, profile, VM collection or full lane is run here, and no speed
improvement is claimed.

[Evidence](evidence.json) records every test receipt's hash, counts and failures.
Raw logs/TRX remain under ignored
`.git/agent-notes/streaming-emission-ownership-20261010/`. The new source receives
one full Debug run; the earlier failed namespace suite is not recollected.
