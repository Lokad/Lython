# Lython Architecture

This document is a maintainer map of Lython. The user-facing compatibility
contract remains in [SPEC.md](SPEC.md); this file explains where that contract is
implemented and which boundaries a change must preserve.

## Design Constraints

Lython is a Python-shaped runtime, not a CLR scripting escape hatch. Supported
behavior follows Python semantics, while unsupported behavior is rejected
explicitly. Scripts receive no ambient filesystem, process, standard-stream,
clock, entropy, or import authority. Those effects exist only when the embedder
supplies the corresponding public host capability.

Three invariants dominate the implementation:

1. Compilation is effect-free. Host-dependent checks may inspect capability
   availability immediately before execution, but they do not invoke the host.
2. Every runtime effect crosses an `ILythonHost`-derived boundary, is charged to
   the execution budget, and translates host failures to stable Python-shaped
   failures.
3. Static analysis rejects only provable errors. Unknown or data-dependent
   behavior remains a runtime concern.

## Compilation Pipeline

`LythonEngine.Compile` drives these stages:

```text
source
  -> normalization and lexical analysis
  -> Parser / syntax tree
  -> annotation and static-analysis passes
  -> LoweredScript
  -> ExecutableScript when executable lowering supports the program
```

The parser and syntax model live under `Frontend/`. `LythonFrontend` normalizes
line endings, lexes, parses, and combines diagnostics. `StaticAnalyzer` runs
separate passes for scope directives, abstract values, regex contracts, and name
binding. Host-requirement analysis runs before execution because only then is a
concrete host available.

Frontend passes that need syntax recursion should use
`ExpressionSyntaxTraversal` and `StatementSyntaxTraversal`. Comprehension
iterables and eager result expressions are traversed in the current execution
scope; lambda and generator bodies are deferred. A new syntax node must update
the shared traversal and its coverage tests before pass-specific behavior is
added.

`AbstractValue` is a closed analysis value: its factories establish the payload
required by each `AbstractValueKind`, and `RequirePayload<T>` is the checked
access path. `AbstractValueTraitFacts` is the exhaustive source of negative
callability, iteration, sizing, and subscription facts. `AbstractValue.Join`
delegates conservative control-flow merges to the focused lattice-join
component. Protected `try` bodies suppress abstract runtime diagnostics when an
`except` body can catch them, but their successful-path facts still flow into
`else` and the post-`try` merge.

`ScopeDirectiveFactsCollector` owns function-local name discovery as well as
`global` and `nonlocal` facts. Name-binding diagnostics, static analysis, and
execution must reuse those facts so exception aliases, pattern captures,
parameters, and assignment targets cannot acquire path-specific scope rules.

## Lowering And Execution

`LoweredScript` is the complete, general intermediate representation. Both sync
and async execution support it. `ExecutableScript` is an optional, block-based
representation for the synchronous fast path. Unsupported executable lowering
falls back to `LoweredScript`; it is not a compilation error.

Lowered assignments are closed variants for names, chains, annotations,
unpacking, subscripts, slices, members, and augmented targets. Consumers should
dispatch on those variants rather than reconstructing target shape from
nullable fields or from the original syntax node.

The executable path has two layers:

- `LythonRuntime.Executable.cs` creates locals and closure cells, establishes the
  interpreter frame, and guarantees frame teardown.
- `ExecutableFrameInterpreter` owns the operand stack, inline caches, block
  entry depths, and pending abrupt-control state for one invocation. Its opcode
  handlers are grouped by definitions, stack transfers, structures, value
  operations, and control flow.

Every executable control-flow edge must enter a block at one consistent stack
depth. Exceptions and return/break/continue signals remain explicit while a
`finally` block runs, then propagate when that block completes. Changes to
lowered semantics should be tested against both the general and executable paths
whenever the opcode path can represent the construct.

`ExecutionContext` owns per-run services and Python-visible state: globals,
locals, closure resolution, exception state, module cache, decimal context,
host handles, and limits. Values entering from public options are normalized to
Lython's governed runtime values before script code observes them.

Ownership across the execution layers:

- Run-shared services live in one `ExecutionServices` per run, shared by every
  context: `ExecutionState` (host, limits, governor, budget guards, random
  state, decimal context, option snapshots, module registries, standard-stream
  builders and handles, member caches), value-observation guards, and the active
  exception state, which is saved and restored around nested handling.
- Lexical scope is the linked `ExecutionFrame.Parent` chain, each frame owning
  its `Variables` dictionary; the root frame holds module variables.
- Closure scope anchors at `FunctionClosureContext` (each function or module
  anchors itself; class bodies inherit their parent anchor), with per-context
  `NonlocalTargets` resolved once from scope facts.
- The fast-path interpreter frame (`CurrentExecutableFrame`) is per-context
  invocation state, not shared.
- `ParentContext` is the context ancestry chain; `Parent` is the same reference
  under a shorter name. `State`, `Host`, `Limits`, `MemoryGovernor`, and
  `Variables` are pure forwarders to the shared services, state, or frame and
  own nothing themselves.

## Runtime Values And Protocols

Python-shaped values live under `Runtime/`, with text and numeric primitives in
their corresponding subdirectories. Cross-cutting behavior is expressed through
small protocols such as callability, truthiness, indexing, rendering, and member
access.

`PyString` is the sole Python text representation after values enter the runtime.
Public host/global inputs normalize CLR strings at the boundary, and public
projection converts governed values back for consumers. Runtime switches must
not accept raw CLR strings as a second, partially compatible Python value kind.

Protocol-aware unary, binary, comparison, and augmented-assignment dispatch is
centralized so syntax-tree, lowered, executable, and `operator`-module paths do
not drift. Python sorting uses a stable merge operation with one key evaluation
per item and ordered sync/async comparisons; proportional scratch storage must
be accounted for before allocation.

Dynamic attributes deliberately distinguish reading from mutation:

- `IPyDynamicAttributes` provides lookup.
- `IPyMutableDynamicAttributes` additionally provides assignment.

A value must implement the mutable protocol only when assignment can actually
succeed. Unsupported mutation should raise a Python-shaped error at the member
boundary, not advertise a setter that always returns `false`.

Async iteration uses `PyIterationResult`, whose factories make yielded and ended
states explicit. Consumers must test `HasValue` before reading `Value`; the
default struct value is the ended state.

## Host Boundary And Containment

`ILythonHost` is the authority root. Binary read and write live as
default methods on `ILythonHost` that throw `LythonHostCapabilityUnavailableException`
unless the host overrides them; binary append instead defaults to a
non-atomic stat/read/write composition that hosts with native append
should override. Separate optional interfaces add subprocesses,
timing, and standard streams. Module implementations must not use
ambient filesystem APIs, process APIs, environment variables, clocks, delays,
or entropy as fallbacks.

Default methods for unavailable optional host operations throw
`LythonHostCapabilityUnavailableException`. Host-boundary translation uses that
type, rather than matching exception messages, to produce the corresponding
Python-shaped unavailable-capability failure.

Before a host operation, runtime code registers the call with the execution
budget. Synchronous entry points reject operations that require unavailable
synchronous behavior before preceding side effects can occur. Sync and async
host-await helpers share exception translation: Lython exceptions and
`OutOfMemoryException` retain their identity, cancellation and output-limit
failures keep their dedicated categories, binary-I/O failures retain their
contract, and ordinary host exceptions become stable `RuntimeError` failures.

Runtime allocations that can scale with script input must use the execution
`MemoryGovernor`. Prefer single-pass builders and sized buffers; do not create an
ungoverned proportional intermediate merely to populate a governed value.

Path behavior is split between lexical providers and cohesive host-backed
providers. Lexical operations never call the host. Status, mutation, unsupported
host-boundary members, and content/enumeration members are independently
dispatched so authority-bearing behavior remains reviewable.

Archive formats are parsed and serialized by narrow bounded codecs that reuse
governed buffers, raw DEFLATE mechanics, and the shared CRC-32 operation.
Writers stage complete validated payloads in governed memory and publish through
a single host replacement on close, so no partial archive is ever visible.
Retained payloads stay charged to the owning run, member handles hold owned
copies, appended entries are carried as validated slices without recompression,
and extraction plans every destination beneath its root before writing.

## Module Organization

`LythonRuntime` is a partial runtime facade. Its files are responsibility slices,
not independent services: builtin/module registration, individual standard
library modules, execution, calls, member access, and host adapters. Add behavior
to the narrowest existing slice. When a switch or state machine grows beyond one
reviewable responsibility, extract a named component or provider rather than a
generic utility class.

Avoid duplicating Python semantics across paths. Shared rules belong in focused
operations or traversal components; sync and async loops may remain distinct
when their control flow makes awaiting and side-effect order clearer. Private
logic used at only one call site should normally be a local function.

## Verification Boundaries

`tests/Lokad.Lython.Tests` is the white-box subsystem suite. It compiles production
sources into the test assembly so internals remain internal without a friend
assembly. `tests/Lokad.Lython.PublicApi.Tests` references the built library and
guards the real consumer boundary. `tools/LythonProbe` provides public-API
compatibility probes and optional comparison with trusted local CPython.

Before committing a behavior change:

1. Add focused tests for success, Python-shaped failure, and sync/async parity
   where relevant.
2. Test the public boundary when public contracts, projection, or host behavior
   changes.
3. Run the complete solution tests with warnings treated as errors.
4. Keep package/version changes in a separate, explicit release commit.
