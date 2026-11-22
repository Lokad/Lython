# Optional adapter range port on the frozen 0.5.0 producer

Private `ef44cbeec` ports the adapter over the immutable PythonRe
0.5.0 development package from `61228b20f`. The tested package and
loaded assembly hashes match the recorded Windows .NET 11 and
Ubuntu .NET 10 assets. The production adapter still pins 0.4.0.
[Development package identity](../2026-11-22-regex-050-frozen-boundary/README.md).

The port passes original subject memory and its byte window to
the producer. It copies capture names directly into UTF-8 storage
and translates match snapshots to the existing absolute capture
ranges. One-shot matches use one-result cursors for last-close
metadata. Working storage and pattern lifetime use the existing
Lython owners, and cursor creation keeps its scoped token bridge.
Temporary snapshot and cursor overhead remains unmeasured.

Each platform passes 622 focused public Release checks. Two new
cases cover short and long UTF-8 group names, lookbehind before
the window, original byte offsets, outer last-close capture and
return of allowance. The existing refusal, alias, cache and
current-token tests also pass.

The original 128-byte nested-repeat fixture completes before the
cancellation controller fires on this producer. Both match and
cursor checks fail to exercise in-flight cancellation with that
fixture. The private replacement uses a consuming match over
1 MiB of repeated alternatives. Compilation, one-shot matching and
advance now observe cancellation in flight, report the active token
and return the allowance on both targets. Cursor creation occurs
before the advance controller starts. The timeout and cleanup
assertions are unchanged. Both initial failures are preserved.

Reversed anchored windows and compiled-program equality still need
producer support. A direct last-close result would avoid the
single-match cursor overhead. Final adoption remains subject to
managed producer compliance, exact released-package semantics,
original-token interruption and a focused performance comparison.

[Loaded package identity](tested-producer.json) ·
[Private source patch and checks](procedures.tar.xz).
