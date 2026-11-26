# B1789 Collect deferred-definition sites during linearization

The [recorded patch](candidate.patch) on `c9de89e97` replaces a second
whole-tape scan with site placement after linearization. The original
scope proof still observes ordinary local reads before operand forwarding.
Linearization records only definition/store pairs that survive forwarding.
The compiler places those sites after the other code-shape proofs, in
the same order.

The ordinary factory's `build` tape contains 933 instructions. The old
placement pass examines 932 adjacent positions. The new placement pass
visits 93 selected sites. The scope-observer factory contains two
selected sites. Across the four fixtures, both binaries produce identical
opcode, operand and scratch-capacity snapshots and equal retained compiler
admission. Both compiled owners restore their allowance. Both full
32-file images match all four independently verified CPython goldens.
Windows .NET 11 and Ubuntu .NET 10 each pass 497 focused white-box and
1,573 focused public Release checks.

## Local timing

This WSL diagnostic uses two independent processes per source and seven
rotated timed batches per process. Percentages span all four pairings of
process medians. Negative values favor the candidate. Compile-only includes
compiled-program disposal. Through-result adds execution, result verification
and cleanup. Process CPU also includes request handling.

| Script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Ordinary definitions, compile only | −23.25% to +66.10% | −18.89% to +67.20% |
| Protected file search, compile only | +1.18% to +26.06% | −1.76% to +21.52% |
| Ordinary definitions, through result | −9.63% to −0.94% | −3.23% to +2.88% |
| Escaping rules, through result | −0.39% to +2.77% | +1.01% to +11.43% |

Ordinary compile-only elapsed spread reaches 67.09% within the candidate.
The unused file-search control also moves. All 112 batches, joins and
fixture cleanup complete and the raw archive verifies. The timing remains
diagnostic. The same frozen source pair and four phases were tested on the
quiet VM. [Quiet confirmation and delivery decision](quiet/README.md).

[Tape summary](tape-summary.json) · [Image identity](identity.json) ·
[Local timing](summary.json) · [Local observations](observations.tar.xz) ·
[Checks and procedures](procedures.tar.xz)
