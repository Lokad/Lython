# B1362 CPU attribution after signed arithmetic

Profiled production `eb227dcac` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Each workload warms
independently before Linux perf samples all process threads at 199 Hz.
Warm execution includes output verification and prior-result disposal.
Neither script uses guest files. The 32-file image matches B1361.

| Sample count | Range loop in a function | Fully supplied helper called from module |
| --- | ---: | ---: |
| All samples | 1,163 | 1,148 |
| Unmapped leaf | 50 | 83 |
| Closest runtime method is native loop core | 901 | 77 |
| Closest runtime method is executable instruction prefix | 0 | 290 |
| Closest runtime method is builtin arithmetic | 0 | 233 |
| Closest runtime method is native call operation | 0 | 77 |
| Closest runtime method is inline function entry | 0 | 51 |
| Closest runtime method is native call entry | 0 | 31 |

The supplied-helper script resolves 523 of 1,148 samples to the
instruction prefix and builtin arithmetic, which includes useful
arithmetic and result publication. The general binder and scope
promotion do not appear in the sampled stacks. No sample resolves to a
reservation as its closest runtime method. Inclusive call-frame
ancestry spans execution of the body, so it cannot estimate setup or
teardown cost in isolation.

The scripts differ in arithmetic as well as loop placement. Compare
the exact supplied call and operand sequence at module and function
scope before choosing between frame-loop and call transport work.
This profile gives neither an elapsed comparison nor a speedup
estimate.

Both CPython goldens pass against the exact worker image. Both phases
complete with quiet gates before and after, joined workers and raw
traces verified in persistent storage before releasing the fixture
mount.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Production CPython comparison](../2026-11-22-machine-arithmetic-cpython/README.md) ·
[Previous attribution](../2026-11-22-post-capture-first-use-profile/README.md).
