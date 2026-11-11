# R229 — complete patch-generation workload

The existing representative catalog now includes `patch-generation.py`: read two
UTF-8 Python files, generate `difflib.unified_diff`, publish `change.patch` and
report line counts. Empty,16-line and1,024-line inputs use one central replacement
between unique lines. Exact patch goldens are independently constructed in CLR
from that edit, including three context lines, headers, UTF-8 and LF bytes.

**Correctness qualified; performance unmeasured.** On2026-11-11, Release solution
build20.26s/0warnings/errors;83file-workerchecks pass per actual.NET11/.NET10.
Existing replay/validation tests include all three new cases; added lane controls
cover compile, compile-to-result and async execution. Shutdown requires the shared
budget to reach zero. Core source remains byte-equivalent to fully qualified4aa;
normal0.4 adapter unchanged. [Identity](identity.json),[checks](focused-checks.json).

Independent supervised stockCPython3.13.2 and Lython workers pass all3sizes against
the same source/fixtures/exact files and stdout, with before/after verification,
compile/warm/compile-run smoke, Closed and joined shutdown. Two-invocation clocks
are deliberately unqualified; no speed ratio, CPU or pending-I/O latency follows.
[Semantic receipt](semantic-cpython.json),[frozen catalog](catalog.json).

The existing comparison protocol/host/runner is reused; no new framework or runtime
shortcut. Current difflib already reuses native match-length dictionaries, while
private patch generation uses Python-facing matcher/index/match/opcode machinery.
Their source existence does not establish dominance. Next one bounded controlled
compile/warm/compile-to-result comparison will decide whether a substantial phase
needs attribution/redesign. Preserve mutable public matcher tables, cache aliases,
Python tie/junk/popular rules, callbacks, cancellation and admitted ownership.

This artifact adds workload coverage, not a new runtime delivery or a reason to
repeat unchanged platform/package qualification. Exact sources/commands/logs/TRXs
and original smoke clocks survive in [preparation](preparation.tar.xz) with its
[archive identity](archive-identity.json). Publication remains postponed.
