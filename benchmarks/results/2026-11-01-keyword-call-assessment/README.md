# Ordinary keyword-call assessment (B49)

2026-11-01, frozen runtime **082dd130 + [B48 source.patch](../2026-11-01-expression-route/source.patch.gz)**,
identical core SHA256 `ecf3ee0fa0318e0143d01483ad8f27343e5d5e4bfe36c5f616b2a50b4fa8bde6`
in all six processes. This is a source-spelling diagnostic, not a runtime candidate
or a current CPython performance comparison. **No runtime change is selected.**

## Source finding

Ordinary named calls bypass the executable caller/callee chain. They already read
operand storage directly through BindFunctionArguments/BindInto, using the shared
parameter-slot map and admitted full-local array. There is no CallArgumentValue[]
or name-dictionary allocation for these fixed signatures. Both routes retain
simple-return handling; these helpers call builtins and are not shortcut eligible.
The measured distinction is general binding and nested compiled-runner transport,
not an interpreted function body. Adding a second keyword binder would duplicate
working machinery without an established material complete-script cost.

## Frozen comparison and outcome

The [prospective criteria](pre-code-plan.md) require a >=10% named-call complete
process-CPU gap against both positional controls to select a redesign. Only call
spelling changes: `parse_amount(value=row['amount'])` or
`words_from_line(line=line)`. Sources, fixtures and complete stdout/file goldens are
preserved in the raw archive. Existing supervised comparison tooling confirms
both named scripts against isolated CPython 3.13.2: zero failed cases. That is
semantic validation, not a qualified external timing ratio.

Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3,
ordinary GC/tiering. One **47.336-second collection / 47.363-second service**,
ordered positional/named/identical-positional processes for each complete script.
Four warmups, seven batches of128 jobs; no profiling, retiming or favorable selection.

| Complete warm job | Positional CPU ms | Named CPU ms | Identical positional CPU ms | Named change against both |
| --- | ---: | ---: | ---: | --- |
| CSV helpers | 7.76144 | 7.39932 | 7.84761 | -4.67% / -5.71% |
| Text helpers | 6.23012 | 6.38795 | 6.12556 | +2.53% / +4.28% |

CSV elapsed8.376/8.446/8.538ms; text6.648/6.978/6.720ms. Text's possible elapsed
cost (+4.96%/+3.84%) remains visible. CPU control drift +1.11%CSV/-1.68%text and
allocation drift below0.008% pass the frozen5%/0.2% bounds. All CPU batches exceed
100ms. Allocation is essentially unchanged at9.542MB CSV/5.519MB text. No case
meets the material-gap criterion. These short observations do not prove named
calls are faster or free; they do not justify a new argument/frame redesign.

Warm Run includes fixture reset, efficient mediated file I/O, full validation and
result disposal. Compilation/startup excluded. The shared unbounded allowance
exercises normal ownership; finite refusal is qualified separately. Both quiet
machine gates, all goldens and balance checks pass. All children join; service is
inactive/dead/success and VM lease available. No new benchmark framework, runtime
option, public API, dependency, cancellation or memory-policy change.

[Complete summary](summary.json), [loaded identities](worker-identity.json),
[semantic receipt](named-validation.json), collector and [raw results](raw-results.tar.gz)
preserve every sample and control. Raw SHA256
`76a97461a74a3fd540b77ad3a2ad47ffe5f2fcd252de92a5caed9aeda403416f`.
Stop B49. No prototype, retiming, extra qualification or speedup claim.
