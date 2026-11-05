# Selected D37/D38 construction replacements — before code

D36 completed32s/two cases/all gates/zero event loss. Allocation-tick attribution
is sampled, not exact/live/CPU evidence. Ordinary interpreter288B/context168B are
16.59/9.67% sampled bytes; actual defining contexts escape through closures, so no
transient-context fusion selected. Arguments/locals already share96B/9-slot backing.
Text strings/bytes54.16%, temporary reservations11.01%, lists10.39%, BigInteger18.09%.

D37: counted whitespace/default/forward/reverse/cased splitting already knows all
output ranges and capacities. One admission covers final list capacity plus payloads;
fill one final array and adopt via D34's existing PyList transfer primitive. Remove
separate list constructor reservation and per-fragment Add/EnsureCapacity. Preserve
Unicode/sigma/expansion, maxsplit/reverse, independent payload owners, capacity and
replacement overlap, cancellation before allocation and terminal first refusal.
Unowned helper use retains its unowned contract; no hidden allowance. Combined
preflight can refuse earlier. No new container/backend/scope/dispatch framework.
The executable fixed-count list-literal producer also copies through a temporary
array into a second list backing. Fill/adopt its single final array under the same
existing primitive; preserve input retirement/cleanup and first-refusal semantics.

D38: direct builtin call receivers bypass member lookup, yet every invocation
allocates/precharges its MemberCacheCount table. Allocate/admit that table only at
the first generic lookup, BEFORE descriptor/protocol effects, under the existing
frame-storage owner; publication uses that admitted table. No cacheability guess
or post-callback denial is needed. Keep uncacheable generic targets' current path;
never skip caching after refused admission. Remove constructor allocation/preflight.
Missed ordinary name lookup must use existing nonmaterializing frame reads, while
explicit namespace/mutation consumers keep the stable actual dictionary. Remove
gratuitous empty namespace construction from read paths; no new lookup cache.
Preserve class/annotation/nonlocal/uninitialized semantics and method exposure,
shared async dispatch, actual lookup/argument order, aliases and awaited cleanup.
Lazy cache admission can occur later in a body; it still precedes cache allocation
and later call/argument effects, fails terminally and never retries. Existing
published values/receivers retain owners; no per-object accounting or tracing.

Focused Release per actual11/10: string/whitespace/native returns and full CSV;
scope/closure/class/nonlocal/annotation/locals/import/member/generator/protocol and
real suspension/cancellation/refusal/lifetime families. Add narrow nonmaterializing
lookup and actual cache admission/cleanup checks only for uncovered invariants.

One bounded final source pair versus qualified402, <=180s owned: text warm/fresh/
async-completed-I/O, ordinaryCSV warm/fresh and report control (six jobs~165s).
Two unchanged baseline roles, all normal/quiet/identity/fullgolden/balance/join
gates. Reuse existing driver, no CPython rerun immediately. Keep adverse signs;
explicit retain/refine/withdraw and remaining debt decision. Full platform/Stress/
installed checks batch both retained changes. No predicted CPU gain from samples.
