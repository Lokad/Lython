# B1935 Direct ready iterator callback handoff

The trial returns the existing typed row directly for a ready callback and
uses a separate typed continuation for suspension or failure. The reference
is `df3ee74ad`. Both variants keep the unboxed executable slot through
frame completion and cleanup. Only the core DLL differs between the two
verified 32-file images.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 1,238 public
and 334 white-box focused Release checks. Both frozen workers match all
four CPython 3.13.16 output and file goldens. The Windows .NET 11
allocation witness finds identical compiled charges and peak admission
in both APIs. Median managed allocation differs by at most 230 B, and
every command returns the shared allowance to baseline.

## Warm execution

Ubuntu WSL and .NET 11 RC3, November 28, 2026. Each image has two
workers pinned to one logical CPU, with at least five seconds of warmup
and seven rotated batches per worker and workload. Ranges span all
pairings of worker medians. Elapsed includes applicable RAM-backed I/O,
verification and prior-result disposal. Process CPU also includes fixture
reset and framing. Startup and compilation are outside both clocks.
All 112 batches verify, and all workers and fixtures are removed before
verifying the observation archive.

| Workload, `RunAsync` | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Iterable shipment | −0.33% to +1.08% | −2.80% to +1.19% |
| Ordinary helper and generator reduction | −1.64% to +3.81% | 0.00% to +1.72% |
| File-fed map | −5.23% to −0.36% | −2.90% to −1.79% |
| First-line file search, unchanged callback path | −0.94% to +6.30% | +2.99% to +6.29% |

Withdraw the direct ready handoff. The iterable timing spans zero and
does not establish that the handoff caused the B1934 slowdown. The
file-fed map gains CPU while the unrelated control loses CPU. This
prevents attributing the small shifts to the handoff alone. Keep B1934's
unboxed returns and its adverse iterable evidence for the next
whole-script checkpoint.

[Timing](summary.json) · [Allocation](allocation.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Source patch, checks and procedures](procedures.tar.xz).
