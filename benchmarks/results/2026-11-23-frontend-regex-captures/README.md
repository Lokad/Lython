# B1481 Share inline capture storage with constant regex validation

`520ecf8dc` gives frontend constant evaluation the same managed inline
capture buffer as the runtime's single-match path. Larger captures still
use a preflighted heap array and a temporary reservation. The provider
writes caller-owned spans and receives the current token in either case.

Windows .NET 11 and Ubuntu .NET 10 each pass 97 white-box and 622 public
focused Release checks. The initial run's cancellation test expected a
growth callback for a single slot. The wide fixture still exercises that
callback, and the effective-flags test now covers both storage paths.
The tests verify subject and pattern retirement and shared-budget reuse.
Three independent CPython goldens verify on both 32-file images using the
current optional PythonRe 0.4.0 adapter.

Local compilation of the 64 named rules allocates **6,144 fewer bytes**
with and without CLR tiering, about 0.08% of the complete compile. The
template control is flat. The positional control allocates 288 bytes more
in the initial ordering and matches with tiering disabled. Every compile
restores the allowance. The initial diagnostic executable was incompatible
with the current comparison host and failed before observations. The
compile-only procedure uses current frozen assemblies and keeps execution
goldens in the existing comparison tool.

This is a small capture ownership simplification. The measurements cover
managed allocation and provide no timing or backend qualification.

[Allocation and identities](summary.json) ·
[Observations and procedures](observations-and-procedures.tar.xz).
