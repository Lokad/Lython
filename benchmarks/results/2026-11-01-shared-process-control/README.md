# Same-process identical-image control passes (B55)

2026-11-01. **0.81% median elapsed difference**, within the predeclared5% control
bound; relative IQR2.33%/1.92% passes10%. This validates one private internal control,
not a speedup, a cause for the earlier cross-process difference, general repeatability
or an external-baseline ratio. No runtime/source/package/framework change.

## Frozen method and checks

[Criteria before code](pre-code-plan.md): adapt the existing complete-job reflection
driver to two managed AssemblyLoadContexts in one owned process. Frozen identical
Lython/host bits, core6e51564b, informational20e208c0/original production95b5955c.
Distinct Lython assembly/type identities share System.Private.CoreLib. Untimed public
hash witnesses match for the actual CSV headers/categories:kind/amount/note/blue/red.
No private seed writes, runtime options, unsafe/native techniques or GC forcing.

Each image owns its own engine/compiled script/budget/host. Every complete warm
CSV-helper job resets real fixtures, runs the public API, checks full stdout/files,
disposes its result and verifies the budget balance. Witness script/results end
before main compilation; both compiled owners return balances to0 on disposal.
Private array/metadata lifetimes are not a guest-heap tracing mechanism.

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3,
ordinary GC/tiering verified from actual process settings.128 interleaved warmups
per image,2s settle, seven alternating paired32-job batches. All elapsed batches
exceed100ms. One **9.998s owned service**, below30s including setup and cleanup.

| Identical image | Median elapsed ms/job | Relative IQR |
| --- | ---: | ---: |
| First | 8.05295 | 2.33% |
| Second | 8.11810 | 1.92% |

Every sample remains, including the first image's9.897ms outlier. No favorable
selection or retiming. All before/after quiet, image/hash, full-golden, balance,
fixture-removal and process-exit gates pass. Service inactive/dead/success, MainPID0,
empty control group and free VM lease verified. No claim that idle startup workers
unload with their contexts: the external service owns and joins the process.

## What this permits and what it does not prove

The [independent-worker control](../2026-11-01-identical-worker-control/README.md)
remains failed, with identical bits differing9.78%. Shared CoreLib hashing removes
one possible process-specific variable; JIT/PGO, scheduling, caches, file paths and
coexisting images still affect this diagnostic. Matching hash values do NOT prove
hashing caused the earlier difference. No runtime hash/GC/tiering policy changes.

One subsequent coherent runtime trial may use this private internal method with
its own identical controls, material elapsed criteria, semantic/ownership checks
and hard bound recorded before code. It cannot replace CPython as the maintained
external baseline or rescue/rerun closed candidates. Keep startup/compilation/I/O/
cleanup boundaries explicit; phase medians cannot be subtracted. Stop this control.

[All observations and gates](summary.json), [driver/source identities](source-identity.json),
[readable private driver](Program.cs), collectors and [raw results](raw-results.tar.gz),
SHA256`538b144e71e754a3ffc4a26139e37386211412ac789e85505f057b4597317d12`,
preserve source, images, input catalog, hashes, every paired sample and machine gates.
