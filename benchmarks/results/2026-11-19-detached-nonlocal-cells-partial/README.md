# B1060 partial mutable capture comparison

The instrumented comparison used retained `d4b2088d4` and private
`c43d4929c` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3.
Both full 32-file worker images and all seven stdout goldens verified locally.
A new stdout-only case inherited a file-output checksum from the forwarding
catalog entry. The comparison supervisor refused its first verification
request after two preceding mutable-counter lanes had timed successfully.

| Completed mutable-counter lane | Guest elapsed change | Process CPU change |
| --- | ---: | ---: |
| Warm | −3.83…−0.23% | −5.27…−0.69% |
| Compile-to-result | −3.49…+1.91% | −3.09…+1.92% |

The four ranges combine duplicate candidate and reference process medians.
The failure stopped the final quiet gate and the control cases. These are
partial observations. The corrected comparison reuses the identical
images and measures all requested lanes with the repaired catalog.

An idle recovery lease verified the original images and the 40 timed
batches, archived the persisted observations and then unmounted the empty
fixture directory. The original status, catalog and runner remain in
the evidence.

[Summary](summary.json) · [Partial observations](partial-observations.tgz) ·
[Original terminal state](original-terminal-status.json) ·
[Recovery](partial-recovery.json) · [Procedures](procedures.tar.xz).
