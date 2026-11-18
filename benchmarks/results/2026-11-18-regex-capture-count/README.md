# Shared UTF-8 counting for capture projection

R533 replaces two directional rune decoders with Lython's existing vectorized
UTF-8 unit counter. The projection still chooses the nearest known coordinate
on the original subject, then adds or subtracts the scalar count across the
byte range. Validation, capture ordering, byte boundaries and scratch owners
stay in the existing path. The shared counter checks the active token every
4 KiB of input and again before it returns.

This is an isolated core prototype. Initial public regex and Unicode checks
passed on both actual targets, 679 per target. Initial white-box checks passed
on .NET 10 and found one failure on .NET 11. The backward scan finished before
the fixed-delay test could cancel it. The failure is preserved. The test now
uses a 48 MiB subject within the same 64 MiB allowance and keeps its active
cancellation, cursor, cleanup and balance assertions. The refined cancellation
and UTF-8 width checks passed all 16 tests on each target.

Before retention, compare the affected complete Unicode capture workloads and
ASCII controls with both frozen source images. The optional adapter and the
producer's pending cursor and staging changes are separate work.

[Initial and refined checks](checks.tar.xz) · [Isolated source](candidate.patch.gz)
