# B1800 Prepare native segments during tape emission

The candidate on `aef1fd39a` resolves protected instruction offsets
after assigning block positions. Closure arithmetic, dictionary update
and native histogram loops are recognized at their final store or
backedge during emission. This removes a separate tape scan and keeps
the original operand and exception-boundary proofs. It creates no
candidate list. The ordinary-definitions fixture loses a pass over 967
instruction positions.

Nine serialized tape snapshots match byte for byte, including raw
instruction fields, native dictionary and closure markers, call-site
flags, operand diagnostic spans, protected ranges and storage capacity.
Both full images match five independent CPython goldens. Windows .NET 11
and Ubuntu .NET 10 each pass 2,807 public and 1,113 white-box focused
Release checks. The first build attempted to write through the read-only
call-site property. Emission now takes the existing mutable builder
array, while runtime readers keep the read-only view. Both initial build
logs and the successful checks are in the procedures archive.

Windows compilation and warm execution allocation match on all five
cases, as do retained compilation admission and peak execution
admission. Every allowance returns to baseline.

## Local diagnostic

The local Ubuntu WSL block covers all five lanes and 140 timed batches.
Ranges span all pairings of the two worker medians for each image.
Negative values mean less time.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile only | −12.88% to +20.47% | −12.05% to +17.70% |
| Escaping rules | Compile only | −12.59% to +27.22% | −1.37% to +6.74% |
| Unicode text loop control | Warm sync | −6.68% to +3.35% | −5.98% to +6.72% |
| Ordinary definitions | Through result | −4.16% to −1.31% | −5.94% to −4.90% |
| Protected file-search control | Warm sync | −3.12% to +3.57% | −6.69% to +10.38% |

## Quiet confirmation

The same images run four reordered lanes on Azure F4as_v7, Ubuntu
24.04.4 and .NET 11 RC3, November 26, 2026 UTC. Workers warm for five
seconds and then complete seven rotated batches targeting 440 ms.
Compile-only covers compilation and compiled-program disposal.
Through result also includes execution and result disposal. Warm
file-search elapsed includes mediated RAM-backed I/O, output checks
and result disposal. Process CPU also includes request framing and
fixture reset where applicable. Startup is outside these phases.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Protected file-search control | Warm sync | +0.27% to +2.17% | −0.94% to +1.07% |
| Ordinary definitions | Through result | −0.46% to +0.44% | −1.92% to +0.18% |
| Escaping rules | Compile only | −0.40% to +1.57% | −0.62% to +0.42% |
| Ordinary definitions | Compile only | −2.29% to −1.13% | −2.94% to −0.54% |

Retain emission-time preparation. Ordinary-definitions compile elapsed
falls 1.13–2.29% and process CPU falls 0.54–2.94% on the quiet VM.
Ordinary through-result and escaping compile CPU span zero. The local
through-result improvement does not repeat. Protected file-search
elapsed rises 0.27–2.17% on the same emitted tape, while its CPU spans
zero. The local Unicode text control is mixed. Keep those observations
with the removed scan and matching allocations.

All 112 quiet batches and both quiet checks pass. Workers join, guest
fixtures retire, and both archives verify. Staging released one
duplicate B1799 DLL after verifying the full local image, a separate
compressed DLL backup and the remote archive against its pushed copy.

[Tape comparison](tape-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Warm allocation and admission](allocation-summary.json).

[Local timing](local/summary.json) · [Local observations](local/observations.tar.xz) ·
[Local image](local/identity.json) · [Local receipt](local/archive-receipt.json).

[Quiet timing](quiet/summary.json) · [Quiet observations](quiet/observations.tar.xz) ·
[Quiet image](quiet/identity.json) · [Quiet terminal status](quiet/terminal-status.json).

[Verification, procedures and exact patch](procedures.tar.xz).
