# R565 keyword forwarding prototype

CPython preserves the original key object when a dynamic mapping supplies a
keyword through `**`. The reference Lython build preserved the value but
reconstructed the key, in both sync and async execution. The prototype carries
the original key alongside its CLR spelling and fills the final `**kwargs`
dictionary directly. It removes the private CLR spill map. Early call-site
collision checks and the CLR spelling used by native consumers are retained.

Reference `25d3005f8` (`087eebd6`) and isolated source patch (`40b2116e`).
Measured on 2026-11-18 under Windows 11 on an i7-14700KF with .NET
11.0.0-rc.3.26522.148 and CPython 3.13.2. Both frozen images verified the
1,024-record forwarding report and unchanged positional control against
expected stdout and file output. Complete warm runs allocate 4,364,278 →
3,971,062 B (−9.01%) on the report. The accounted execution peak drops from
3,997,360 to 3,589,808 B (−10.20%). The positional control's median allocation
and accounted peak are unchanged.

| Workload and lane | Reference → prototype, first pair | Reference → prototype, reversed roles |
| --- | ---: | ---: |
| Keyword report warm | 18.252 → 21.908 ms | 21.582 → 26.650 ms |
| Keyword report compile to result | 18.442 → 17.277 ms | 27.844 → 19.054 ms |
| Keyword report compilation | 77.51 → 91.75 µs | 101.23 → 64.13 µs |
| Positional control warm | 2.756 → 3.206 ms | 2.054 → 2.000 ms |

Warm includes mediated file I/O, golden checks and prior-result cleanup.
Worker startup, fixture reset and final cache teardown are outside elapsed.
Compile to result includes the compiled owner's disposal. The warm slowdown
appears in both local pairs. The first prototype report warm IQR is 110% of
its median, the unchanged control moves, and compilation and compile-to-result
show different behavior. These results justify investigating the warm path.
Process CPU was unavailable in this local harness. The VM had about 266 MB
free, below the existing staging floor before any new source images.

The alias regression fails on the reference and passes in the prototype.
Windows Release passed 33 white-box and 200 public checks per target across
the selected runs. WSL passed 33 white-box checks per target. Checks include
duplicate and missing-argument errors, isolated storage refunds, terminal
admission refusal, cancellation, and original aliases through an actually
suspending default lookup. The prototype remains outside the delivered core
pending a controlled timing pair or a justified correction.

[Machine-readable summary](summary.json) · [Source patch, local timings and checks](evidence.tar.xz)
