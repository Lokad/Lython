# D55 bounded cancellation in native whitespace scans, before code

Current D53 collectors (ordinary forward/reverse and unbounded cased) invoke
PyStructuralGuard.NoteWork once per decoded scalar. That resolves execution state
and increments its shared WorkTick merely to check cancellation every64elements.
These loops traverse UTF8 scalars, not recursively structured guest values; no
cycle tracking or nesting semantics depend on that interval update. Split core
already resolves the active explicit/ambient context once. D52 forward collector
27/723self samples is evidence of real scan work, not a removable CPU fraction;
the later cased collector retains the same per-scalar NoteWork mechanism.

Select the same three scalar scans as a single bulk-text cancellation contract:
check context.CheckExecution before each64-byte input chunk, then decode complete
UTF8 scalars until its end. A last scalar can cross the boundary by at most3bytes;
at most64scalars execute between checks, preserving or strengthening the existing
worst-case scalar interval. Reverse uses the corresponding start boundary. End/
output/growth checks and every admission preflight stay. No timing threshold,
timeout relaxation, new work counter/fuel, policy, config, native path or state.
Remove only these per-scalar shared WorkTick updates; NoteWork remains for its
actual structural callers. Keep all29 whitespace values, maxsplit/rsplit, case
capacity/expansions/final sigma, independently owned words and existing writers.

Accept current cancellation/refusal/preflight/lifetime cases plus real in-flight
long whitespace/word scans where a gap exists. Do not add exact checkpoint-count
tests or claim cancellation latency without actually measuring it. Source proof
establishes the <=64scalar interval; full Python outputs and cleanup remain
independent checks. Cancellation before chunk entry/after completion and active
first refusal retain original spans. Context-free calls have no host token.

One short complete standard/Unicode/fused/unfused text comparison with current
source-pair/control/CPython and existing gates. Profile/allocation/causal CPU are
separate. Retain/refine/withdraw on evidence, preserving all original conditions;
no automatic percent veto. No reintroduction of B151 ASCII scanners or B149 shared
fragments, no generic scanner framework. Batch full qualification at a coherent
retained boundary. Larger call/frame/performance objective remains open.
