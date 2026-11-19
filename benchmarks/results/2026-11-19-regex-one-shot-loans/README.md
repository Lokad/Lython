# Development regex one-shot loans with dev.107

The optional adapter prototype uses producer-funded retained ranges for
both one-shot matching and cursor pulls. It scopes one-shot working
storage to the call and releases its range receipt after copying captures
into the Lython buffer. Both paths give the producer the original UTF-8
subject and the explicit end byte. This removes the estimated one-shot
subject-size reservation and shares capture copying and error mapping.
The prior cursor-loan experiment provides the immutable PythonRe and Core
0.5.0-dev.107 packages from `7be42c66`. [Cursor-loan evidence](../2026-11-19-regex-cursor-loans/README.md).

On .NET 10 and 11, match, search and fullmatch agree with CPython 3.13 for
a bounded Unicode window whose lookbehind captures before the start. The
provider's last-closed capture agrees. The one-shot admission probe observes
working growth, rejects a 96-byte request once, returns the allowance to
baseline and reuses it. Both one-shot and cursor cancellation interrupt the
recalibrated in-flight operation with the active token and a zero final balance.

The immutable producer's one-shot and cursor entry points both throw
`ArgumentOutOfRangeException(startOffsetInBytes)` for start byte 5 and
end byte 4 on the original subject. CPython's empty `match` succeeds
at scalar 2, byte 5. The prototype continues to surface this gap as an
explicit capability error. The shipped adapter remains pinned to 0.4.0.
Qualification here covers these development probes. Elapsed performance and
the final released package are pending.

[Results and image hashes](checks.json) · [Prototype and probe sources](development-checks.tar.xz).
