# B1048 prepared name-binding diagnostics

The retained `c8e749318` checks a nested declaration header once in its
defining scope and reuses prepared local-name membership. The previous
compiler emitted the unbound-read diagnostic twice for a lambda default
inside a nested declaration. The regression reproduced on the previous
runtime. Windows .NET 11 and WSL .NET 10 each passed 375 public and 31
white-box checks for names, scopes, cancellation and class cells.

Both complete 32-file images passed the four-workload catalog with CPython
goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-19 UTC. It compared `bed567750` with `d6adfc554`, whose source
and tests match `c8e749318`. Ranges cover four pairs of process medians.
Negative changes favor the candidate.

| Script and boundary | Guest elapsed change | Process CPU change |
| --- | ---: | ---: |
| Configuration manifest, compilation | −3.35…−0.25% | −2.88…+1.40% |
| Configuration manifest, compile-to-result | −1.18…+1.35% | −1.31…+0.56% |
| Generated validator, compilation | −3.49…+1.75% | −0.59…+1.96% |
| Generated validator, compile-to-result | −0.78…+2.05% | +0.55…+3.39% |
| Configuration edit, compilation | −2.03…+0.94% | −1.71…+1.40% |
| Configuration edit, compile-to-result | −2.10…−0.58% | −3.53…−1.92% |
| Shipment selector report, warm control | −2.90…−0.69% | −2.04…+0.83% |

The generated validator's compile-to-result elapsed IQR reaches 4.51%, and
the unaffected warm control moves favorably. The data leave a possible CPU
cost for the generated validator. The diagnostic correction and removal of
repeated work justify retention. Recheck the validator at the next frontend
milestone.

All seven timing jobs and both quiet gates finished in 322.69 seconds.
Workers joined and RAM fixtures were removed. An obsolete 252 MiB free-space
check failed after the persistent archive was already written. A separate
idle lease verified the original archive against the persisted data, checked
both complete worker images, and recovered all 140 measured batches. The
original failure and verification are retained.

Compile-to-result includes compilation, mediated I/O, golden checks and
owner cleanup. Compilation alone includes compiled-owner disposal. Warm
includes mediated I/O, checks and preceding-result cleanup. Startup and
final cache teardown fall outside guest elapsed. CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Original terminal state](original-terminal-status.json) ·
[Archive verification](archive-recovery.json) · [Measured core](measured-core.dll.gz) ·
[Sources, checks and procedures](checks-and-procedures.tar.xz).
