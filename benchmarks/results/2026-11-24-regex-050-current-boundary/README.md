# Frozen 0.5.0 adapter bridge on the current cursor contract

The private range adapter from `ef44cbeec` builds on Lython `9d50a1b1f`
with the current matching flags and original-subject cursor changes.
The isolated feed contains the immutable 0.5.0 Core and PythonRe packages
from producer `61228b20f`. Package and test-output assembly hashes match
the earlier producer manifest on both targets.

Windows .NET 11 and Ubuntu .NET 10 each pass 157 focused Release checks.
These cover clipped Unicode windows, matches retained across cursor close,
UTF-8 capture names, effective flags, current-token cancellation, terminal
refusal, and allowance recovery. Adapter tests exercise the actual package.
Provider contract tests use cooperating providers to examine interruption
and cleanup order. Both sets are included in the count.

The current production adapter still uses 0.4.0. The local result qualifies
the rebased development bridge for these checks. Released-package adoption
still needs public staged parsing and compiled equality, reversed anchored
window semantics, managed dependency validation, and focused backend timing.
The earlier qualification records the exact unresolved producer behavior.

[Pinned bytes and focused counts](verification.json) ·
[Adapter patch and test logs](procedures.tar.xz) ·
[Earlier boundary probes](../2026-11-22-regex-050-frozen-boundary/README.md).
