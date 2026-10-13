# Linux embedding impact: bound, exposed and retained errors

Binding caught errors changes the cost dramatically: **128 Gen2 collections per
completed job**, compared with **one** for unbound catches. Bound/exposed/retained
models complete only 12–14 jobs per four-second window; unbound catches complete
1,250–1,267. These scripts deliberately differ in their exception work; the counts
show this model's cost, not a CPython ratio or a general throughput comparison.

All **24 original samples / 12 matched pairs** meet the unchanged finite targets:
added request p99 <=5 ms and largest added heartbeat <=25 ms. The fixed matrix took
**178.125s**; the longest owned child took **7.409s**.
No sample was excluded or replaced. Latency acceptance leaves repeated process-wide
full collections and GC-independent reclamation as architectural problems.

| Catch behavior | Added request p99 (ms) | Largest added heartbeat (ms) | Jobs per 4s window | Gen2 per job |
| --- | ---: | ---: | ---: | ---: |
| unbound | 2.492–2.602 | 3.272–3.461 | 1,250–1,267 | 1 |
| bound | 1.766–2.374 | 4.021–6.119 | 13–14 | 128 |
| exposed | 1.692–1.844 | 3.725–4.078 | 13 | 128 |
| retained | 2.036–2.402 | 3.958–4.776 | 12–13 | 128 |

Measured source `7f5fbf70222de9ff96e9f241f2eff227fa911055`, production tree
`dcec7539f370c3622b55057a4fd91d0b5236e80c`, on 2026-10-13. Quiet Azure Linux VM,
4 logical processors, Linux 6.17.0-1022-azure, .NET SDK 10.0.401/runtime 10.0.12.
[prepared.json](reproduce/prepared.json) freezes the actual diagnostic Release DLL
and toolchain hashes. Compilation, preparation and startup are outside the window.
Later runtime revisions require separate qualification.

## Declared work and boundary

Each fresh synchronous `Run` keeps a 32,000-byte string at a 48,000-byte execution
cap, attempts 128 further 32,000-byte strings, and catches every denial. The bound
case uses `except MemoryError as error`; the exposed case also reads `args` and
`str(error)`; the retained case additionally keeps the first eight real exceptions.
Every completed job must return exactly its declared catch/payload/retention/
exposure counts, with empty stdout/stderr and peaks within the unchanged cap.
Compiled code and the fixed host are reused; execution state is fresh for every job.
[The four scripts](reproduce/) and [probe](reproduce/Program.cs) retain these checks.

The existing observer measures a managed JSON request and heartbeat with a 1-ms
sleep while the caller completes jobs. Every active/control child receives the
same retained 128-MiB host graph (131,072 arrays), two declared noncompacting full
preparation collections, two real warm jobs, and a two-second pause. All arrays
must be Gen2 before measurement. Controls then measure host requests alone.
Allocations, CPU and collection counts describe the whole process. The model has
no production request queue and establishes neither Windows behavior nor a general
application SLO. No forced-GC setting, GC mode or tiering override was used.

The supervisor owns an exclusive VM lease, 28-second child deadline plus one second
of cleanup, and a 240-second campaign limit. Three independent sessions include a
reversed middle order. Source/fixture/DLL identities, idle CPU <=3% median/<=5% max,
no paging/steal, >=1,000 observations, exact outcomes and all-old host graphs gate
eligibility. Both services exited successfully, their cgroups are gone and the
lease is free: [terminal proof](reproduce/terminal-proof.json).

## Interpretation and architectural lead

Guest exception exposure funds actual storage between denial attempts. The current
allocation-progress flag therefore authorizes relief again, and each attempt runs
through full GC although this fixed retained payload leaves insufficient room for
the request. The source explains the mechanism; the exact 1-versus-128 collection
counts above are measurements. Suppressing relief simply because the retained
payload looks unchanged is not a demonstrated safe replacement.

A separate local correctness probe at this same production tree catches a denial,
withdraws the payload by `del held`, rebinding it to `None`, or list-element
deletion, then attempts a fitting replacement. All three fail with MemoryError.
A control that allocates a two-character string after deletion succeeds: actual
allocation authorizes relief, while removing the reference alone does not.
[The reproducer and sanitized receipt](recovery/) preserve that finding. It is not
part of the latency matrix. Whole ownership/root/resource coverage must address
both avoidable collections and recovery after references are withdrawn.

A stack-only candidate also stops before implementation: keeping instruction
operands published does not expose prepared mapping-key snapshots or evolving
native helper state. [The source assessment](ARCHITECTURE.md) records the required
larger representation. No pending-root slot, additional registry or partial
collector is shipped.

[Raw distributions/receipts](raw/), [recomputed validation](validation.json) and
[independent verifier](reproduce/verify-published-evidence.py) retain all originals.
Reproduce the declared Git archive paths and probe, then the one fixed eight-case /
three-session supervisor. Record new rebuild hashes separately. The earlier
[unbound pressure report](../2026-10-13-embedding-sync-pressure/README.md) remains
historical at its own source/cap; it is not silently replaced by this model.
