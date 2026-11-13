# Builtin slot definition-owner correction — R298

Source review during the bounded config gate found sixteen native object slots
whose BindOwner overwrote their defining class on every alias assignment. The
public probe over qualified31d65e01/e34cacaf printsTrue/False/False for the original
object.__init__.__objclass__, while isolated CPython printsTrue/True/True.

The ordinary public regression reproduces two failures (Run/RunAsync) after its
new assembly is built. An earlier test attempt ran while the build was pending,
matched no tests and is **invalid**, preserved separately; it is not a pass or a
runtime failure. The private check now rejects no-match output.

Preserve each wrapper's first owner, matching existing function binding, and state
that invariant on IClassOwnedMember. No new wrapper, registry, metadata cache,
policy, dependency or public API is introduced. Guest aliases now retain Python's
original __objclass__, including direct class-body/later assignments and forwarding
through staticmethod/classmethod/property. Actual receiver binding stays ordinary.

The full fifteen-public-slot matrix/golden is independently checked under isolated
CPython. **627 public semantic, descriptor, constructor, cancellation and ownership
checks pass per actual.NET11/10**. Repeated shared-budget cleanup returns to baseline
through both APIs; normal aliased initialization/rendering still works. This is a
correctness change, with no elapsed/CPU claim. The sixteenth slot is the existing
classmethod-wrapped init_subclass implementation; broader subclassing checks pass.

The changed contract is a Python compatibility correction: aliases stop rewriting
the original native descriptor's metadata. Current performance measurements remain
source-labelled31d65e01 and do not time this later correction. Full qualification
belongs to the next coherent delivery checkpoint. Publication/tagging remain held;
the larger performance/regex roadmap continues.

[Exact source, original failures, invalid attempt and focused checks](source-checks.tar.xz),
[validation/source identity](validation.json), [CPU source gate](../2026-11-13-config-cpu-source-gate/README.md).

Finald413392c full qualification is GREEN; [cached delivery](delivery/README.md). Earlier pending statements describe their recording dates.
