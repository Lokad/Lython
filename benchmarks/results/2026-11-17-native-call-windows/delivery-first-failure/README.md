# First delivery failure, preserved

CI [44067803913](https://github.com/Lokad/Lython/actions/runs/44067803913) at
34d2e393 fails ordinary .NET11 tests on Windows and Ubuntu. No full qualification
is claimed; subsequent .NET10/package/stress gates did not complete.

A real regression: caught native argument-binding errors leave partial parameter
values in caller-owned storage. A later valid call can incorrectly raise TypeError.
Existing FunctionBindingLifetimeTests expose both positional and keyword-default
cases. The correction initializes the disjoint parameter prefix before binding
and clears it on failure. No compatibility wrapper, fallback or alternate mode.

Other failures depend on earlier fixed working-storage sizes. Refusal fixtures
now admit the caller window before denying conversion/write storage; publication,
argument effects, input aliases and cleanup assertions remain. Retained-data
fixtures separate the first payload/working set from a larger refused second
payload, still requiring completed gate I/O, terminal refusal and funded runs.
The JSON preflight check now asserts a digit-scale refused request and the actual
supplied allowance instead of an unrelated 4096-byte setup threshold.

Focused correction checks on actual .NET11/10 each pass 124 public and 88 internal
cases; two independent CPython3.13.2 default-binding programs agree. Original CI
logs, inputs and the initial correction's fixture failures remain preserved.
Corrected-source measurement and full delivery are separate, still pending.

[Terminal](terminal.json), [committed inputs](inputs.tar.xz), [logs](run-logs.zip).
