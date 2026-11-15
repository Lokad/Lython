# Initial delivery: obsolete fixture casts

CI 43661215938 at `cf754dc7` fails on Windows and Ubuntu: the same 43 internal
cases in five lifetime fixture classes still cast executable backing to object[].
Both platforms pass all 17,849 public tests on actual .NET 11. Subsequent .NET 10,
package and stress steps are skipped, so this run does not qualify delivery.

Preserve the terminal result, complete logs and exact source proof. The correction
uses direct typed access to the actual retained arrays, preserving identity,
side-effect, suspension, cancellation and cleanup assertions. It removes stale
reflection and an unnecessary nonparallel annotation; no runtime source, timeout,
budget guarantee or measured image changes. Full corrected qualification follows
separately. Never requery this terminal run.
