# Ordinary starred lists stay executable

Ordinary `[*left, value, *right]` displays now use the same prepared display
lowering and runtime operations already used by yielding generators. The compiler
evaluates each expression and immediately appends/iterates it before evaluating
the next expression. This removes the ordinary starred-list expression fallback
and local mirroring needed only by it, including propagation to eligible parents.

The production change is **two compiler files, five inserted/six deleted lines**:
reuse/rename the existing display lowering helper. No opcode, runtime handler,
collection owner, iterator, dependency or configuration is added. Lowered display
evaluation remains necessary for genuinely lowered code; other display families
and comprehensions remain open. This does not revive the rejected comprehension
producers. Existing per-item checkpoints apply, so explicit fuel counts can
differ from the earlier AST route; Python evaluation order is unchanged.

| Warm ordinary helper/list-merge job | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 2,048 calls; six-element merged lists | 2.664 ms | 2.179 ms | 0.3541 ms |

One [short diagnostic](warm.json) observed an **18.2% lower Lython median**;
IQR/median is1.3%/0.7%/0.3%, respectively. No qualified multiplier, confidence
interval, overall speedup or complete file-job claim. No retry or retuning.
The owned command took7.949seconds including cleanup. The remaining CPython
gap is substantial.

The [shared case](catalog-micro.json) defines a helper, merges two fixed tuples
with local values, and accumulates indexed values/counts. Both workers create
fresh execution state and reuse compiled code; timing includes initialization,
execution/projection and previous-result disposal. Compilation, process startup,
transport and full response hashing are excluded. Actual guest host-I/O suspension
is tested separately, not converted into an injected-delay CPython ratio.

Measured2026-10-15 on AzureStandard_F4as_v7, Ubuntu24.04.4 LTS, four AMD
EPYC9V45vCPUs/16GiB RAM; SDK10.0.401/runtime10.0.12, CPython3.13.16 with
ordinary GC/GIL and `-I -S`. Protocol UTF-8 is explicit. Normal Lython public
policies, cooperative allowance, GC/tiering and managed stack protection remain;
CPython has no equivalent containment. Baseline8f290b57675f9bbff5cb704bfc96d332f1baaa1f
adds the identical helper fixture to9f249639; the candidate changes only the two
frozen compiler sources in [the declaration](comparison-declaration.json).

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent output witnesses at all three existing core sizes.
[Source/cleanup proof](cleanup-source-proof.json) verifies both frozen compiler
files, identical timing/catalog/Python sources, seven exited workers, inactive
services and an available VM lease. No file fixtures are used in this case.

[Local checks](local-validation.json) pass2,310ordinary white-box,4focusedwhite,
12newpublic,339semantic and284governed/representative/file-worker cases. Groups
overlap; each finishes below30seconds including cleanup. Structural checks prove
no expression fallback or forced mirroring for an eligible helper and its module.
Fuel-cutoff pairs preserve sync/async spans, assignment publication and unwinding.
Denied growth preserves the original target and output-file bytes; result disposal
returns allowance to baseline. Real yielding/cancelled iteration and escaping
magnitude aliases preserve funding. Eight [independent CPython3.13.2 goldens](cpython-goldens.json)
cover interleaving, errors, aliases, closures/locals, yield/send and iterator-finally.
Original structural/setup failures remain private; budgets/timeouts were not
relaxed. Package/platform qualification is recorded at delivery.
