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
2. Every runtime effect crosses an `ILythonHost`-derived boundary, observes
   cancellation and applicable resource admission, and translates host failures
   to stable Python-shaped failures.
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
  -> ExecutableScript for both public execution APIs
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

`LoweredScript` is the general intermediate representation used to build
`ExecutableScript`. Both public execution APIs run executable code. Frontend
validation rejects invalid loop control and operators before lowering; compiler
invariant failures surface directly rather than silently selecting another
engine. The unused whole-module lowered representation is released after
compilation.

Local imports prepare and run the same executable code as public entry scripts.
Executable import tables retain the original immutable import syntax metadata;
they do not copy it into a second binding record or reconstruct nodes at runtime.
Synchronous imports still bind slots directly; asynchronous imports await host
resolution through the existing import implementation.
Their module context mirrors stores into its namespace for exports and escaping
functions, even when no opcode fallback requests mirroring. Loaded script modules
use that same context for attributes and globals(); builtin definition factories
retain stable member caching, while mutable script namespaces read current values.
Code admission is committed before guest callbacks can expose prepared code.
The builtin member dictionary is initialized on its first ordinary cached lookup;
script and name-only access do not allocate that table. The name value remains
eager and concurrent publication uses the existing dictionary.

Module final blocks complete naturally; an executed top-level return remains
separately detectable and forbidden during import. Function returns, including
implicit None, keep their existing lowering and optimized calling paths.

Ordinary executable definitions use one definition operation for functions and
generators, including annotations and type parameters. It creates the existing
callable representation, evaluates metadata and decorators in Python order, and
publishes its bound slot directly. Annotated generators do not detour through
lowered definition dispatch or resynchronize the whole namespace. Generator
bodies retain their existing executable code and lexical namespace lifetime.
Executable bindings retain declaration metadata and leading docstring text rather
than another lowered body tree. Definition execution still admits and creates
the guest docstring before publishing the function. Coarse compilation funding
and the original syntax needed for diagnostics remain unchanged.
Definitions prepared across a guest yield retain their evaluated inputs, then
reuse the same compiled binding and definition factory. Their ordinary bodies
are executable; publication updates the bound slot directly. Captured metadata
and default-map scratch stay admitted through awaited decorators and annotations.
Definitions in lowered class bodies retain their existing evaluation path.

Lowered statement and expression evaluation remains necessary for class bodies,
annotations and individual opcode fallbacks. Executable fallback tables retain
these lowered nodes directly, without separate statement/expression carrier
records. The existing fallback opcodes and local mirroring remain. Synchronous
and async
lowered `match` evaluate their already-lowered guards and case bodies; there is
no raw-syntax statement interpreter. Pattern and expression helpers still use
syntax where needed. Imported errors retain their source paths and propagate
after leaving active catch handlers, preserving nested-error stack safety.

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

Prepared comprehension boundaries, display creation, tuple completion, call
accumulator creation and formatted-part joining complete through one nonasync
entry. Operations that can suspend retain one async handler. Each operation has
one implementation; scope, stack and storage cleanup remain with that operation.

Produced values go directly to the existing operand stack. Suspending producers
finish their await before accessing that mutable stack; no result-observation
forwarder or unused source span remains. Operand replacement still keeps inputs
funded through callbacks and clears them on failure.

Every executable control-flow edge must enter a block at one consistent stack
depth. Exceptions and return/break/continue signals remain explicit while a
`finally` block runs, then propagate when that block completes. Changes to
shared semantics should be tested through executable modules/functions and real
lowered consumers such as class bodies and annotations, with both execution APIs.

Executable call sites store the frontend's immutable `CallArgumentForm` values
directly. The existing argument-conversion helper serves both packaged calls and
direct operand binding; there is no per-argument metadata wrapper.

Async `sum()` shares the existing checked synchronous loop for an exact range
and an exact integer start. This shape cannot invoke guest protocols. Custom
starts and other iterables retain async iteration/addition and awaited cleanup;
allocation preflights and cancellation remain in the shared numeric/range helpers.

The existing sum loops share one private scalar state for integer, compensated
float and general-addition phases. Float compensation is finalized before a
guest operation; generic addition then remains in control. Fixed signed-64-bit
phase bounds keep behavior portable rather than copying native C-long widths;
SPEC.md records the mixed wide-int/float rounding tradeoff. Iteration, arithmetic
preflights, guest dispatch and async cleanup stay in their existing paths.

Min/max candidate selection shares the existing Python operator/truthiness paths
after the numeric kernel. Async selection awaits iterator acquisition, rich slots
and truth conversion, retaining the original reduction loops and first-tie order.
Wrapped comparators expose one async invocation/sign-validation path shared by
equality, inequality, membership/element equality, ordering and sorting; the
synchronous path remains separate. The sorting
algorithm, argument binder and ownership mechanisms remain in place.

`operator.index` shares `CoerceTypeIndexAsync` with native indexing and JSON
raw-prefix conversion. Its synchronous native path stays direct; async callbacks
and descriptor binding are awaited. The helper retains exact integer objects,
preserving exposed identity and existing storage owners.

Public projection reserves through its existing result owner, without a separate
memory cap. Failure frame arrays are admitted before allocation. Diagnostic
storage refusal uses the existing minimal failure result; it does not retry
admission with a shortened message.

`ExecutionContext` owns per-run services and Python-visible state: globals,
locals, closure resolution, exception state, module cache, decimal context,
host handles, and limits. Values entering from public options are normalized to
Lython's governed runtime values before script code observes them.

Ownership across the execution layers:

- Run-shared services live in one `ExecutionServices` per run, shared by every
  context: `ExecutionState` (host, limits, governor, random state, decimal context,
  option snapshots, module registries, standard-stream builders and handles,
  member caches), cancellation/admission and managed-stack guards, and the active
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

The JSON reader passes its original governed `PyString` through token and error
helpers, deriving byte views from that immutable document. It carries no separate
document/view record. Lenient token rewrites preserve original error coordinates
and document identity; suspended callbacks retain the existing execution owners.
Raw-prefix index coercion shares the existing type-index protocol under both
execution APIs, awaiting descriptors/callbacks only when asynchronous hosting is
active. Native indices go directly to one integer/size validator. Binding,
coercion, text validation and negative-index checks retain Python's error order.

JSON value emission uses one synchronous `ValueTask` entry for native scalars and
forwards container completion directly. Default callbacks keep an awaited
replacement tail. Shared cancellation, growth preflights, cycle/depth checks and
the container/callback ownership lifetimes remain on their existing paths.

Protocol-aware unary, binary, comparison, and augmented-assignment dispatch is
centralized so expression helpers, lowered, executable, and `operator`-module paths do
not drift. Python sorting uses a stable merge operation with one key evaluation
per item and ordered sync/async comparisons; proportional scratch storage must
be accounted for before allocation.

Async set ordering uses the existing frozen protocol-key metadata and awaits
element equality/truth helpers. A read-only side-index bucket resolves source
identity without another table. Mutated generated dataclass hashes require a
cancellable identity scan only when that bucket misses. Source-entry and candidate
copies have temporary reservations through suspension; clearing either set cannot
refund their working storage. No thread-local ambient context spans an await.
Builtin-only comparisons retain the HashSet path. General guest hashing and
nested structural equality retain their synchronous restrictions.

Generated dataclass hashes enter the existing managed single-value structural
guard and check cancellation while visiting fields. Cycles and excessive depth
unwind through that guard before CLR stack exhaustion; successful hashes retain
their existing field combination. The generated callable supplies context/span
through the existing synchronous ambient scope, honoring per-execution recursion
and cancellation limits. This native synchronous walk never spans an
await and introduces no separate protection policy.

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

Ranged reads (`ReadTextUtf8RangeAsync`, `ReadBytesRangeAsync`) bound
acquisition: offsets and lengths are byte-based, negative arguments fail
explicitly, and offsets at or past the end yield empty payloads. The default
implementations compose whole-file reads and are therefore correct but
unbounded; hosts with native ranged I/O should override them. Engine file
opens stream fixed windows through these ranges, with shared storage admission
and per-pull cancellation, so a native override
is what makes large-file scans bounded end to end; without it every window
re-reads the whole file host-side.

Host operations use the active cancellation token and mediated capabilities.
Synchronous entry points reject operations that require unavailable
synchronous behavior before preceding side effects can occur. Sync and async
host-await helpers share exception translation: Lython exceptions and
`OutOfMemoryException` retain their identity, cancellation keeps its dedicated
category, binary-I/O failures retain their
contract, and ordinary host exceptions become stable `RuntimeError` failures.

Runtime allocations that can scale with script input must use the execution
`MemoryGovernor`. Prefer single-pass builders and sized buffers; do not create an
ungoverned proportional intermediate merely to populate a governed value.

List backing admits reference and possible numeric-box headroom by capacity,
without classifying contents or maintaining a numeric-slot balance. In-capacity
mutation/removal retains funding. Growth admits full replacement while old
backing stays charged, then relinquishes old backing; unpublished construction
failure returns only its private storage. Attaching ungoverned internal backing
conservatively admits its capacity and preserves any existing producer funding.
Large numeric payloads and escaping values retain their independent owners.

Native dictionary `setdefault` shares the existing protocol insertion kernel
with ordinary writes, retaining the current value on a hit. Guest query hashes
are computed once, including identity hits. Existing insertion sequence metadata
allows a callback to add replacement keys without repeating equality against
already examined entries. Indexed protocol keys are excluded from the native-key
scan. Probing reads matched values live after callbacks and holds its admitted
snapshots until those users finish; insertion retains ordinary storage preflights.
Ordinary one/two-positional calls on exact native dictionaries use that same
operation from the existing receiver/argument operands. Exposed, expanded,
keyword, invalid-arity and custom-member forms keep general binding. The direct
call entry retains the ordinary managed frame and exception boundary.

Native concatenation admits one final-capacity backing through the active
execution owner, including when inputs are prepaid views without governors.
Indexed copies preserve source funding and aliases, check cancellation while
working and discard only unpublished destination storage on failure.

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
3. Use focused Release regressions during development. Batch complete platform,
   Stress and explicit Release package checks at coherent delivery boundaries,
   following [TESTING.md](TESTING.md).
4. Keep package/version changes in a separate, explicit release commit.

## Regex provider boundary

`LythonEngine` borrows one `ILythonRegexProvider` across frontend analysis,
execution and imports. The core defines the UTF-8 pattern/subject/cursor/capture
contract; it references no backend package or types. Missing capabilities fail
explicitly. The optional `Lokad.Lython.Utf8Regex` assembly owns backend adaptation
and its separately documented limitations. Native wildcard matching stays native.

Patterns expose immutable effective flags and metadata. Provider operations use
the current cancellation token and existing shared owners; caches retain no
token. Active regex iterators join deterministic execution cleanup. Callback
replacements use the existing synchronous/asynchronous callable routes and keep
subject storage funded while host I/O is pending. No tracing, new memory layer,
regex time/work policy, dialect fallback or abandoned matching task is involved.
