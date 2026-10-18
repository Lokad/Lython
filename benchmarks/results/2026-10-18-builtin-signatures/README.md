# Reuse int/float signature inputs, 2026-10-18 UTC

Fresh execution contexts now reuse two immutable `int`/`float` signature
references. Their mutable builtin wrappers and connected type graphs remain
per execution. The existing signature cache already reused layouts and parameter
maps; this change removes two fixed parameter-name array constructions and
repeated cache lookup, locking and shape comparison. It adds no registry,
configuration, binding route or ownership mechanism.

Baseline is immutable **4e55ff0df2d3044da115f9bc7075cf1fc5a33876**; the candidate
is the [one-file patch](candidate.patch). Source verification preserves the
factory names, parameter names, defaults/counts, positional-only and variadic
options exactly. Both per-context `BuiltinCallable` constructions remain.

| Native allocation diagnostic | Before | After | Difference |
| --- | ---: | ---: | ---: |
| One warm fresh context, 103 builtin entries | 19,312 B | 19,176 B | 136 B less |
| Child context reusing parent state | 264 B | 264 B | Unchanged |

One baseline and one candidate capture use two warmups and 32 retained samples
per route. The [fixed gate](comparison.json) requires all fresh samples below
baseline and identical child-control samples. The [baseline](baseline-allocation.trx),
[candidate](candidate-allocation.trx) and [diagnostic source](diagnostic.cs) retain
the evidence. Fresh contexts keep distinct int/float/object wrappers; child
contexts retain parent identities, exception aliases remain correct and the
shared allowance returns to zero.

This measures native warm context construction, excluding options/scope setup,
guest compilation/execution, public projection and process startup. The local
Windows x64 build 26200 machine has an i7-14700KF and 28 logical processors; .NET
host 10.0.13 / SDK 10.0.300-preview.0.26177.108. [Environment](environment.json)
records the details. No elapsed, complete-job or CPython speedup is claimed.

All **846 distinct focused Release cases** pass: 67 signature/binding/protocol/
stack, 628 public builtin/conversion/isolation, 89 ownership/admission and 62
complete-job/cancellation cases, including 58 complete CSV/text/JSON/file jobs.
[Validation](validation.json) records source and process hashes. The matching
37 [CPython source goldens](../2026-10-18-native-abs/python-goldens.json) from the preceding qualified delivery remain valid
evidence; no Python fixture or semantic implementation changed here.

The temporary diagnostic is removed. Existing signature-cache, binder, numeric,
allocation-preflight, cancellation and ownership implementations are unchanged.
This closes the two-input scope; no wider factory inventory or cache redesign.
Core dependencies and the planning-only regex migration are unchanged.
