# B1898 Repeat bound shipment with reversed worker placement

The repeat uses the exact B1897 images at `4f0f9e44f` and `5cb61871a`.
All 64 image files match their original hashes, and both images verify
four independent CPython 3.13.16 output and file goldens. It reverses the
initial placement of the bound lane's workers and adds a wide-list lane
over the same 27,350-byte shipment input.

On November 27, 2026 UTC the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET
11 RC3. Each image has two workers with at least five seconds of warmup
and seven rotated timed batches per worker. Ranges span all pairings of
worker medians. Both lanes use `RunAsync`. Warm elapsed includes
mediated RAM-backed I/O, golden checks and previous-result disposal.
Process CPU also includes fixture reset and framing.

| Warm script | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Bound `starmap` shipment | −1.63% to +4.21% | −1.08% to +3.63% |
| Wide-list `starmap` shipment | −1.48% to +3.63% | −1.40% to +4.17% |

Both clocks now span zero for the bound script. Candidate CPU medians
differ by 3.63% within bound and 4.00% within wide. The original quiet
bound increase of 2.15–3.92% retains its original conditions. These
observations leave its cause and size uncertain. Keep the B1897 change
and close further unchanged-image timing for this comparison.
[Original comparison](../2026-11-27-generator-definition-cache/README.md).

The VM passes both quiet gates and all 56 batches, joins all workers,
removes fixtures and verifies the persistent archive under the lease.
Staging streams about 195 KB and reuses both complete images.

## Local stack attribution

Separate six-second EventPipe samples on Ubuntu WSL, pinned to CPU 27,
verify 88 batches across the two bound images and the current wide
lane. Lookup backing access resolves in about 0.1% of eligible bound
sample time in either image and 0.2% of wide. Direct operand returns
and both map and zip advancement appear on the stacks. Inclusive
regions overlap and JIT inlining can hide calls from attribution.
These samples do not explain the earlier timing difference.

The bound policy callback already selects between existing operands
for exact Boolean flags. `starmap` consumes the short zip row without
a tuple, and the inner map uses the same positional callback entry.
The source still resolves the definition before accessing an existing
frame lookup table. That is a separate simplification candidate, with
the original array owner and live entry semantics preserved.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Profile attribution](profile-attribution.json) · [Profile and procedures](procedures-and-profile.tar.xz).
