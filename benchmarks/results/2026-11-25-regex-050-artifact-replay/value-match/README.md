# Optional adapter whole-match development replay

This private follow-up compares the funded cursor bridge with a direct
caller-slot match for patterns with no capture groups. Both images use
Lython `c1984f96b` and the same immutable development producer
`0.5.0-dev.lython.6edc01772`. Only the adapter DLL changes. The bridge keeps
the original subject and explicit byte window, passes the active token and
borrows the working allowance. Grouped matches keep their funded cursor and
range snapshot, which supplies last-close metadata.

The first focused runs pass 514 of 515 checks each. The one-shot literal had
no working allocation to refuse, so the existing test's expected refusal
did not apply. The revised fixtures exercise real working admission in the
grouped, cursor and no-capture backtracking cases and verify subsequent
pattern reuse. The final 516 checks pass on Windows .NET 11 and Ubuntu
.NET 10, including in-flight cancellation of the direct match. Test and
cancellation worker assemblies match the pinned package for each target.
Core has no backend dependency.

Both images match four complete CPython 3.13.2 goldens. Local Windows
.NET 11 warm CLR allocation falls 48.37% for the whole-match compiled
log search and 45.54% for the module form. The grouped log search and
invoice controls differ by 56 B each on more than 3 MB per invocation.
Admitted peaks match and all budgets return to baseline. CPU and elapsed
remain unmeasured. The production adapter and published-package
qualification retain their prior status.

[Exact package replay](../README.md) · [Allocation](allocation-summary.json) ·
[Image identities](identity.json) · [Assembly verification](verification.json) ·
[Procedures, tests and raw results](procedures.tar.xz).
