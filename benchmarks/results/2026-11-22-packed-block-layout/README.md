# B1333 Pack emitted block instructions into one buffer

Private `9a956c91c` replaces the per-block instruction lists with one
temporary buffer. Early protected exits eliminate 192 block reopens
on the dense validator. The shared buffer grows geometrically, funds
the old and new arrays during copying and refunds scratch on disposal.
Final tapes and protected ranges match for all 79 inspected code
objects. Windows .NET 11 and Ubuntu .NET 10 each pass 358 white-box
and 1,400 public focused Release checks, including interleaving,
truncation, admission refusal and cancellation during growth.

Compared `52eeb328e` and `9a956c91c` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Each revision
uses two processes, seven rotated batches per process and at least
twelve seconds of warmup per process. Ranges cover the four pairings
of process medians. Compilation includes code-owner disposal. Time
to result adds mediated RAM-backed input, execution, verification and
prior-result cleanup. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −3.85 to −2.70% | −5.03 to −2.82% | 0.53 / 0.66% |
| Postponed factory | Compile | +0.19 to +2.48% | +0.71 to +2.67% | 2.16 / 0.13% |
| Inherited dataclass | Compile | −3.76 to −0.71% | −5.16 to −0.84% | 2.62 / 0.53% |
| Dead-heavy control | Compile | −1.77 to +3.92% | −2.34 to +3.69% | 2.54 / 3.18% |
| Sparse generated validator | Time to result | +1.71 to +3.24% | +2.58 to +3.62% | 0.16 / 1.34% |

Local paired allocation diagnostics use the dense validator and separate
accounted peak from CLR allocation. The dense validator allocates 4.01%
less CLR memory and needs 393,568 more admitted peak bytes
(2,291,744 → 2,685,312). The factory allocates 1.18% more and adds
6,656 admitted peak bytes. The dead-heavy control allocates 0.03%
less and adds 196,672 admitted peak bytes. All owners refund.

Keep the candidate private. Compilation improves for two scripts,
while the complete validator takes longer and the packed layout needs
more admitted peak storage. This comparison does not isolate the cause
of the time-to-result regression. Reconsider a replacement only if it
can remove a pass or backing and control peak overlap without adding
another execution representation. Keep the reordered emission evidence
available for that design.

Both 32-file images match four CPython goldens. All 140 timed batches
verify. Workers join, the persistent archive verifies under the quiet
lease and guest fixtures are removed. The procedures preserve the initial
missing sparse worktree dependencies and representative CSV fixtures,
and the reservation refusal exposed during development. The measured
revision corrects those conditions.

[Timing](summary.json) · [Allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
