# Development validation with Utf8Regex 0.5.0-dev.109

Lython `7ddb8be02` and the existing development adapter matched CPython 3.13
on ten pattern and subject cases, in both execution modes on Windows .NET 10
and .NET 11. Cases cover original-subject windows, named UTF-8 captures,
lookbehind before the window, adjacent empty and nonempty matches, split,
callback replacement and matches retained after cache purge. Each run used a
borrowed 4 MiB allowance.

The Core and PythonRe package and assembly hashes match the immutable
Release candidate at producer revision `18c1e0e20`. The producer's separate
Lython replay, based on `98b9f87f7`, passed 405 checks for each Windows and
Linux target. Its records include in-flight compile, match and cursor
cancellation. The replay and CPython probes exercise different integration
boundaries.

The direct package probe passed UTF-8 name copying and an ordinary anchored
match. Both targets rejected the original-subject anchored window at byte
start 3 and byte end 1 with an out-of-range start. CPython 3.13 returns
an empty match with a participating empty named capture at scalar position 2.
The development adapter reports an explicit capability diagnostic for this
window. Its inherited wording names the stable 0.4 pin. The package still
uses `Unsafe.SizeOf` in its Core storage estimates. The stable pin stays at
0.4.0. These semantic checks leave final adapter qualification and backend
timing open.

[Inputs and checks](inputs-and-checks.json) ·
[Probes, receipts and test records](checks-and-procedures.tar.xz).
