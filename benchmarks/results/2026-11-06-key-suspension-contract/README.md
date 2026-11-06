# Asynchronous key protocol boundary — 2026-11-06

Q02 qualification gate complete on committed bf741ec2, actual.NET11RC3/.NET10.0.8.
This confirms a documented limitation; it does not add or qualify asynchronous
hash support. [Public probe](Program.cs), [build](build.log), [identity](identity.json),
[.NET11 observations](net11.0.jsonl), [.NET10 observations](net10.0.jsonl).

One host-supplied timing capability actually suspends on an owned gate. Ordinary
RunAsync time.sleep starts the host, leaves the invocation pending and funded,
then completes successfully after release. Both public hash(Key()) and an empty
dictionary's compound-key get reject the same callback before host work begins:
`time.sleep cannot run synchronously; use RunAsync because the host capability is asynchronous.`
The caller is already using RunAsync. Calls/completions are actual host effects,
not execution checkpoints or quotas. No background work starts or is abandoned.

Probe assertions dispose every result, require shared funding to return exactly
to the compilation baseline, then dispose the program and require zero. Logged
`retained` is observed before result disposal, so includes its correctly funded
output/failure; it is not a leak. Both runtimes pass. No performance claim or
production tests permanently enforcing this limitation.

## Required coherent migration

The cause spans existing paths: the hash builtin has only a synchronous handler;
GetInstanceHash uses synchronous descriptor binding/invocation; async dictionary
get deliberately routes protocol queries through its synchronous path. Adding
an async wrapper at the public API would leave the same detour underneath.

Build on Q01's semantic walk with an explicit completed-prefix/pending result,
using existing ValueTask and caller ownership. Use existing BindForInstanceAsync
and callable.InvokeAsync when descriptors or hash callbacks can suspend. Tuple/
slice cycle guards must carry the explicit execution-owned StructuralGuardState,
and unwind that same state after completion/cancellation on another thread; no
thread-static ambient scope may span suspension. Await actual users before ending
scratch/argument/retained-source borrows. Preserve current tokens, first admission
refusal and callback/error order. Ordinary completed/native keys remain cheap.

Completion must state the supported boundary explicitly and remove displaced
synchronous detours there. Qualify public hash, real compound dictionary queries,
native/default paths, descriptor binding and nested/cyclic values before claiming
general support. Mutation, construction, generated dataclass hashes and cache
protocols need explicit decisions; a partial public wrapper must not imply these
are covered. No universal async dispatch framework, duplicate storage authority,
new cancellation policy, background execution or memory subsystem.

Focused gated semantics/cancellation/refusal/retained-alias/cleanup controls precede
any production migration. Compare native completed-key scripts separately from
actual pending-host behavior. This is a correctness/capability milestone, not an
explanation of the synchronous CPython performance gap. Continue independent
whole-script performance work; current lazy function namespaces and direct
operand-to-bound-local transport already exist and must not be rediscovered as
new optimizations. Publication remains postponed.
