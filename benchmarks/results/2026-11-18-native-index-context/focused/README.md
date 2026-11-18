# Explicit native indexing context: focused prototype

R523,2026-11-18UTC. **Private, unpromoted b6769b32** versus retained3f873227 /
a5038a28. Timing is unmeasured. Native builtin sequence indexing no longer pushes
and restores a thread-local ambient scope on every read. Current context/span
follow string count, rune-cache construction and character-copy allocation;
retained storage/cache owners and preallocation rollback remain unchanged. Real
context-free string callers retain ambient resolution, including reversed()'s
one-argument Index delegate. Dynamic protocols/structural guards are retained.
No new dispatch/cache/owner/budget policy or universal index abstraction.

Two production files and four extra native failure cases in the existing cache
regression. Both actual.NET11/10 pass59internal/144public focused Release checks;
nonincremental solution build, actual32-file13goldens/allocation/reuse and exact
four timed-case goldens pass. Cases trigger cancellation/refusal during cold rune
cache construction and warm-cache character allocation under another cancelled
ambient context. Correct current authority, origin, storage rollback, unchanged
input and allowance reuse remain checked. Existing protocol/identity/Unicode/
index-error tests apply. Initial signature build failure is preserved: optional
extra parameters cannot satisfy reversed()'s Func<int,object>; the genuine original
entry remains. No new architecture-only test or checkpoint counter.

Local allocation differs by at most0.0067%; all accounted peaks unchanged and13
budget baselines restore. This establishes no CPU benefit. One short changed-source
floating warm/fresh with text/call controls must decide retention/refinement/debt/
withdrawal. Source/counts/conditions are frozen; no full qualification inherited
from a503. Broader performance work/publication hold continue.

[Exact source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[identity](identity.json),[checks](checks.json),[allocation](allocation-summary.json),
[procedures/original build failure](checks-and-procedures.tar.xz).
