# B1363 Matched helper loop placement

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The exact production image from B1361 is
`eb227dcac`, compared with stock CPython 3.13.16. Each loop processes
the same 2,048 integers. The helper cases evaluate the same
arguments and call the same module-defined function. The inline
controls evaluate the modular operands into two bindings before the
same add and multiply. All cases print `8421311` and use no guest files.

| Loop placement and body | Lython warm | CPython warm | Lython / CPython | Worker spread L/C |
| --- | ---: | ---: | ---: | ---: |
| Module · helper call | 0.3646–0.3685 ms | 0.2019–0.2150 ms | 1.696–1.825× | 1.07 / 6.46% |
| Function · helper call | 0.3152–0.3215 ms | 0.1280–0.1326 ms | 2.377–2.512× | 2.00 / 3.61% |
| Function · inline arithmetic | 0.1679–0.1681 ms | 0.0977–0.1074 ms | 1.564–1.721× | 0.14 / 9.94% |
| Module · inline arithmetic | 0.2365–0.2440 ms | 0.2042–0.2093 ms | 1.130–1.195× | 3.20 / 2.47% |

The elapsed boundary is a warm execution including output verification
and prior-result disposal. Process startup and compilation are excluded.
Two separate workers per engine supply seven rotating timed batches
each after at least eight seconds of warmup. Ratios span all pairings
of process medians. The worker spread shows the difference between
the two medians relative to the lower one.

Wrapping the calling loop cuts Lython elapsed 11.81–14.46% and
CPython elapsed 34.34–40.47%. Wrapping the inline control cuts elapsed
28.90–31.19% and 47.42–53.32%, respectively. These source variants
change name instructions and introduce one outer function call.
With placement fixed, the calling body takes 49.39–55.82% longer in
the Lython module and 87.46–91.48% longer in the Lython function.
The CPython module interval spans zero and its function interval is
19.17–35.75% longer with a 9.94% inline worker spread. These differences
cover operand staging and arithmetic dispatch as well as calls.

The Windows allocation diagnostic measures 142,680 and 142,728 bytes
per invocation for the original module and function call scripts.
The inline cases allocate 65,536 fewer bytes in either scope.
Keeping all helper calls while assigning their results before reading
the accumulator saves 65,504 bytes in each scope. This matches 2,047
32-byte boxes, since the initial accumulator is an existing constant.
The actual tapes show the original accumulator read through
`LoadLocal` into a temporary before the call. The reordered scripts
feed the accumulator directly into augmented arithmetic. The
reordered diagnostic is safe for these pure helpers. A general runtime
change must preserve the original read, early unbound-name errors and
a binding that can change during the right-hand side. These allocation
numbers supply no timing estimate for such a change.

All 112 timed batches and both quiet gates verified in 210 seconds.
The full 32-file worker image and all CPython identities verified.
The four timed scripts and the two extra local allocation probes
match CPython goldens. Allocation probes return the borrowed allowance
to baseline. Workers joined and raw observations were archived and
verified before the VM lease ended.

[Timings and allocation](summary.json) · [Executable shape](shape-summary.json) ·
[Raw observations](observations.tgz) · [Verification and procedures](procedures.tar.xz) ·
[CPU attribution](../2026-11-22-post-arithmetic-invocation-profile/README.md).
