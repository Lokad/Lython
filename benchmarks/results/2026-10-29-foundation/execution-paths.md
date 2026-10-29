# Execution paths at 3386baf8

This is a source map for the next structural prototype, not a performance claim
or a new runtime contract. The foundation method-call case creates an ordinary
class and invokes `Accumulator.add` 16,384 times: about 8 ms in Lython versus
0.875 ms in CPython, with much less spread than the file-script rows.

Module/function definitions already compile through `Builder.InternFunction`
into executable bindings. Both public APIs use those bindings. Direct lambdas
also carry executable code. Do not repeat the obsolete claim that RunAsync
always constructs lowered functions or that every comprehension falls back.

The remaining class route is concrete:

1. Ordinary class statements reach `CompileStatement`'s statement fallback.
2. `ExecuteLoweredClassDefinition` and its async counterpart evaluate the header
   and execute the class suite with `ExecuteStatements`/`ExecuteStatementsAsync`.
3. Function declarations in that suite call `CreateLoweredFunction`, producing
   `PyFunction` for ordinary methods. The executable call loop can recognize that
   callable, but its body still runs the lowered dispatcher.
4. The fallback also marks the enclosing executable code for namespace mirroring.
   Class-created lambdas/generator expressions use lowered expression helpers;
   the documented class-generator close/send/throw boundary follows that route.

Relevant sources are `Frontend/ExecutableScript.Statements.cs`,
`ExecutableScript.Infrastructure.cs`, `ExecutableScript.Builder.cs`, and
`Runtime/LythonRuntime.Lowered.cs`, `LythonRuntime.LoweredAsync.cs`,
`LythonRuntime.Lowered.Shared.cs`, `LythonRuntime.Executable.Interpreter.Async.cs`.
The existing prepared class-header operation already supports suspension; it
still delegates the suite to the lowered class executor.

The next candidate is compiling class suites with the existing executable
builder and passing prepared class-body code through the existing class operation.
Methods/lambdas/generators would then use their existing executable constructors.
Class headers, decorators, metaclass restrictions, dataclass/namedtuple metadata
and class-cell publication should retain one shared implementation. This is a
Python scope distinction in the current engine, not another execution backend.

Class namespaces need deliberate handling: class reads observe the mutable class
dictionary before enclosing/global lookup; unbound names differ from function
locals. Methods/comprehensions skip class locals when capturing outer names.
Nested classes must preserve transitive free variables, global/nonlocal directives
and the hidden `__class__` cell. `locals()` mutation, zero-argument super, defaults,
annotations, postponed/generic scopes and descriptor/decorator order are acceptance
cases. Disabling mirroring globally would be incorrect.

Preserve current managed stack admission/workers, cancellation, exception/finally
cleanup, host suspension, major-capacity admission and retained closure/result
funding. Retire the lowered class-suite route only after its real consumers have
moved. Preserve lowered expression evaluation for actual annotations/defaults and
remaining opcode consumers; do not delete it by assumption.

The prototype stops after this vertical slice: run independent scope/creation-site
goldens and existing class/function/generator/ownership/suspension regressions,
then one short paired method-call diagnostic and relevant complete-script controls.
Promote only with preserved contracts and a structural benefit; preserve/reject
the patch if it demands a new namespace subsystem or performs worse without a
maintenance gain. Do not accumulate alternate dispatchers or micro-tweaks to rescue
it. Explicit guest frames and scalar representation remain later hypotheses.
