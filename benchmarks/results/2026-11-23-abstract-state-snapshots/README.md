# B1442 Share read-only compiler branch facts

`30d956735` retains the measured source from `2e5e316c1`, based on
`e8fd8b73f`. Each abstract binding map stays shared until a mutation
requires an independent copy. The value and sequence-length maps detach
independently. Joins reuse completed storage and skip an unchanged
shared length map. Expression resolutions and container invalidation
remain local to each branch.

Only the core DLL differs across the verified 32-file images. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated validator with read-only branches | Compile | −71.29 to −70.34% | −71.25 to −70.84% | 0.49 / 2.78% |
| Inherited dataclass | Compile | −2.58 to −1.61% | −1.58 to −0.40% | 0.12 / 0.88% |
| Sparse generated validator | Compile | −2.07 to +0.87% | −1.52 to +1.33% | 0.60 / 2.39% |
| Postponed nested factories | Compile | −2.64 to −0.63% | −4.14 to +0.18% | 1.47 / 0.59% |
| Generated validator with read-only branches | Compile to result | −69.22 to −68.11% | −69.76 to −67.99% | 2.86 / 0.75% |
| Inherited dataclass | Compile to result | −2.68 to +0.22% | −4.49 to −0.17% | 1.58 / 1.37% |
| Sparse generated validator | Compile to result | −0.42 to +5.78% | −1.03 to +5.34% | 5.94 / 0.27% |
| Small configuration edit | Compile to result | −0.91 to +0.17% | −0.87 to +0.28% | 0.33 / 0.76% |

Each range spans the pairings of medians from two independent workers
per source. Workers warm for eight seconds and provide seven rotated
batches each. Compilation includes code-owner cleanup. Compile to result
also includes execution, applicable mediated RAM-backed I/O, output
and file verification, and prior-result disposal. Wider process CPU
includes resets and request handling. Startup and final teardown are
outside elapsed.

The dedicated read-only workload avoids copying 207,720 value entries
across 543 forks. Local compile allocation falls from 11.40 MB to
1.05 MB. The inherited dataclass allocates 3.81% less, and postponed
factories allocate 2.66% less. The sparse validator allocates 0.44%
more. Both sides of some sparse forks change values, so it copies 581
value entries against 387 before, while length-map copies drop from
194 to three. The sparse elapsed range includes an adverse comparison,
with 5.94% reference-worker spread. Preserve it as a control.
Representative execution allocation and admitted peaks match.

Windows .NET 11 and Ubuntu .NET 10 each pass 828 focused Release checks.
An initial Windows CLI diagnostic check failed before the matching
probe and SDK path were supplied. The corrected run passed and both
logs are preserved. All eight CPython goldens, 224 quiet timed batches
and both machine gates verified. Owners returned their allowances, all
workers joined, guest fixtures were removed and the archive verified.
The integrated gate passes both Release targets, original-scale
stress and installed-package consumers on Windows and Ubuntu.
[CI](https://github.com/Lokad/Lython/actions/runs/45111635398).

[Source and image hashes](source.json) · [Timings](summary.json) ·
[Binding copy counts](binding-copy-counts.json) ·
[Compile allocation](compile-allocation.json) ·
[Observations](observations.tgz) · [Tests and procedures](procedures.tar.xz).
