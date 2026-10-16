# Changelog

## Unreleased

- Compile ordinary and yielding subscript augmented assignments using captured receiver/key frame slots and the shared value-last store. Remove their statement fallback and prepared read/store branches; route yielding ordinary subscript stores through the same helper. Preserve key identity, native re-conversion after the RHS, in-place aliases, awaited callbacks, admission and cleanup.

- Call user descriptor getter slots raw with descriptor, instance and owner, matching Python's staticmethod/callable behavior and rejecting property/classmethod slots before side effects. Reuse ternary invocation and preserve valid setter/deleter binding, async instance callbacks, cancellation and retained results. Recognize property objects in builtin type reporting.

- Resolve implicit user __getitem__ through type-level slots, preserving ordinary explicit attribute access and callable/non-callable error distinctions. Share synchronous/asynchronous binding and existing unary invocation, await valid descriptors under RunAsync, and preserve raw keys, aliases, cancellation and diagnostics.

- Convert native mutation indices through type-level __index__, awaiting target callbacks under RunAsync and re-coercing retained augmented keys after the RHS. Preserve raw mapping/custom keys and Python's read-only rejection order. Delete the duplicate synchronous item-deletion dispatcher while retaining ambient structural guards, admission and cleanup.

- Compile member augmented assignments with existing duplicate/member-load/in-place instructions and the shared member store. Preserve captured receivers, current setters, in-place aliases, yielding targets and async callbacks; remove their statement fallback and obsolete prepared member-read arm without per-store dictionaries or a new opcode/owner/frame.

- Resolve native slice bounds and slice.indices() lengths through type-level __index__ hooks, awaiting descriptor binding, callbacks and replacement iteration under RunAsync. Share colon/explicit-key and sync/async mutation helpers; replace the duplicated slice drain and final array copy with the existing owned lease while preserving overlap admission, cancellation and retained aliases.

- Compile ordinary slice-store receivers and bounds into the current frame. Hold five operands through the existing mutation helpers and awaited user setters; remove the final general target opcode, operand kind, binding table, builder plumbing and VM handlers. Preserve raw bounds, aliases, cancellation, diagnostics and native storage preflights.

- Validate native slice bounds in Python's step/start/stop order, including reads, list assignment/deletion and slice.indices(). Reject invalid or zero steps before later callbacks and replacement iteration; preserve raw user/mapping and worksheet keys. Remove the empty legacy slice-result tracking hook.

- Compile eligible ordinary for-loop and eager-comprehension targets through the shared unpack/member/subscript/name operations. Keep comprehension names in their existing binding scope, preserve directives and closures, and remove their general target-dispatch path without a new opcode or runtime owner.

- Compile ordinary and chained unpacking groups with the existing executable unpack operations and child stores. Delete the separate opcode, operand kind, target binding table, builder plumbing and interpreter handlers. Preserve Python effects, cells, diagnostics, suspension and retained funding; starred-rest admission now precedes prefix stores.

- Stop nonstarred unpacking after the first excess value, preserving iterator position, later errors and earlier nested stores. Apply the same bound to ordinary, loop, comprehension, header and yielding targets while retaining leased allocation preflights, cancellation and alias funding.

- Compile ordinary member-store receivers in the caller frame and remove their general target-read evaluation. Keep RHS and receiver in two existing operand slots through awaited mutation, then clear them in finally; preserve chained effects, target spans and retained aliases.

- Await supported user-descriptor reads and setter/deleter binding under RunAsync. Valid descriptor callbacks can suspend without replay; synchronous execution and absent/noncallable errors retain their own behavior. Getter slots use the raw-call rule described above.

- Forget pre-try dictionary key facts before computed exception headers and handlers. Accept valid reads after partial protected-body mutations, including aliases and nested mappings; retain definite diagnostics for fresh missing keys and normal runtime key checks.

- Compile ordinary subscript-store receiver/key expressions in the caller frame, preserving RHS-first and chained-assignment order. Use a fixed three-operand store with existing mutation helpers, cleanup and target diagnostics; avoid general target evaluation and per-store read packaging.

- Resolve implicit item mutations through type-level slots, ignoring instance-only overrides and getattr fallback. Await setter/deleter descriptor binding under RunAsync while preserving mutation arguments, alias funding and existing missing/noncallable errors.

- Share the synchronous observed-value handler for exact integer reads on native list, tuple, bytes, text and range values under RunAsync; custom containers/indices, mappings and slices retain asynchronous dispatch.

- Use type-level __index__ conversion for native sequence reads in both execution APIs, awaiting descriptor binding and callbacks under RunAsync. Preserve arbitrary-size range indices and raw dictionary keys; share XML's existing converter and remove the duplicate lowered synchronous read body.

- Route exact integer unary instructions under RunAsync through the existing synchronous arithmetic and value-transfer handler. Other operands retain asynchronous unary/truth protocols, including real host suspension and cancellation.

- Resolve mutable function callable attributes again at reused call sites, so replacement and deletion take effect while retained callable aliases remain valid.

- Support custom-attribute deletion on named, lambda and generator functions through their shared metadata table. Preserve missing-key errors, protected names, default binding and Python-shaped reserved-field resets; retain capacity and escaped-value funding. Remove an unused metadata-byte counter and its rollback branch.

- Dispose native iterator temporaries when executable loops or comprehensions fail or exit early. Retained Python iterator and generator values remain usable; discarding an internal temporary uses the existing reverse cleanup path.

- Compile eager list/set/dictionary comprehension loops and helper calls in the caller's executable frame using the existing binding scope. Restore nested scope temporaries in reverse order and preserve caller locals, closures, walrus, super, async protocols, cancellation and retained funding; no new invocation frame or owner.

- Consolidate yielding and ordinary expression lowering, deleting the duplicate generator-family dispatcher and obsolete eager-comprehension adapter branches. Lazy generator expressions and lambdas created inside comprehension scopes retain their required dynamic routes.

- Share lambda binding, metadata, invocation and generator handling with the existing function classes, deleting the separate lambda callable. Prepare parameters/scope facts once; preserve the shared immutable lambda-name label, while class lambdas keep their qualified name and support the shared function attribute writes.

- Compile lambda bodies/defaults through the shared function compiler and closure cells, removing ordinary lambda fallbacks and prepared captured-expression/default clones. Preserve lowered body fallbacks where needed, actual suspension, alias funding and managed admission. Admit closure-array storage before allocating it.

- Compile ordinary expanded calls with the existing generator call operations, removing their expression fallback, unnecessary local mirroring and boolean fallback selection. Preserve argument order, mapping/iteration suspension, cancellation, admission and retained aliases without a new binder or owner.

- Evaluate deferred arguments before rejecting a non-callable target in syntax and lowered sync/async execution. Argument effects, exceptions and cancellation now precede the target error; callable-target frame admission is unchanged.

- Compile ordinary f-strings through the existing executable field, format-specifier and join operations, removing their expression fallback and unnecessary local mirroring. Preserve formatting protocol suspension, evaluation order, cancellation and conservative intermediate/result admission.

- Compile expanded set/dictionary displays through the existing generator display operations, removing their ordinary expression fallbacks and unnecessary local mirroring. Reuse one dictionary lowering helper; preserve mapping suspension, key/value order, cancellation and retained funding without new opcodes or storage machinery.

- Evaluate a dictionary entry's value before validating its key in syntax and lowered execution. Preserve value-side host effects, cancellation and exception precedence when the key is unhashable.

- Compile ordinary starred tuples through the existing display/immutable-finish operations, removing their expression fallback and unnecessary local mirroring. Conservative scratch/result overlap can reject earlier; explicit fuel uses existing per-item checkpoints.

- Reuse the existing immutable empty tuple for empty literals and completed tuple expansions across executable and lowered execution. Preserve identity with `tuple()` without a new cache or identity ledger.

- Compile ordinary starred-list displays with the existing generator display operations, removing their expression fallback and unnecessary local mirroring. Preserve interleaved evaluation, async iteration, cancellation, admission and alias funding. Explicit fuel now uses those existing per-item checkpoints.

- Await reflected augmented methods for builtin Boolean, integer, float, complex and decimal values, including host integer representations. Preserve cancellation and complex/matrix augmented error tokens when reflected methods decline.

- Add a complete JSON configuration read/edit/write workload with independent files, replay and cancelled-publication checks. Both comparison workers allow exact input/output path overwrites while retaining path, collision and fixture bounds.

- Share the synchronous binary/augmented operator handler for exact integer operands during `RunAsync`, avoiding generic async operation dispatch while retaining preflights, errors, fuel/cancellation and result ownership. Mixed/user operands keep their existing protocol routes.

- Await user-instance augmented numeric methods and exact-integer reflected fallbacks under `RunAsync`, preserving in-place/ordinary/reflected order, augmented error tokens and cancellation. Share the existing operator mappings instead of restricting asynchronous dispatch to addition.

- Sum exact immutable bytes with a machine-integer subtotal and one preflighted exact-integer result, removing per-byte boxes/iteration adapters. Preserve empty/start aliases, integer promotion, terminal fuel checks, native ownership and dynamic async fallbacks.

- Forward validated JSON value reads directly to the existing parser ValueTask, removing a redundant async state machine while preserving callbacks, errors and owned-input cleanup.

- Write ordinary uppercase scalars directly to governed UTF-8 storage. Share the existing expansion table with legacy casing helpers and preserve admission, ownership and cancellation. Add a complete uppercase-report comparison workload with independent output checks.

- Write ordinary lowercase scalars directly into governed UTF-8 storage, removing per-character CLR strings. Preserve dotted-I expansion, contextual final sigma, capacity preflights, cancellation and scratch cleanup.

- Remove the unused function-binding name table and retained parameter-list field. Preserve the canonical slot map, live defaults and conservative layout admission; retain evidence of a rejected compiled-layout sharing prototype.

- Keep thread-local structural comparison scopes within synchronous work after awaited expression evaluation and iterable pulls. Prevent stale execution/cancellation contexts from escaping async dictionary/set construction and comprehensions.

- Reuse protected execution workers for asynchronous startup without blocking the caller through a CPU prefix or retaining a worker through suspended I/O. Share bounded idle retirement and isolated caller contexts with synchronous runs.

- Diagnose unsupported surrogate string literals before executable lowering, including f-strings and unpaired UTF-16 input. Reject out-of-range eight-digit Unicode escapes without signed wraparound.

- Dispatch default builtin `str.strip()` calls without method/binding packaging; preserve dynamic fallback and owned output. Return unchanged trim inputs directly instead of copying, and reject keywords to builtin `str.strip`.

- Dispatch ordinary builtin `dict.get` calls without bound-method or argument packaging. Share lookup with exposed methods, await stored-key equality for string lookups, and forward async unbound methods correctly. Reject builtin `dict.get` keyword arguments while preserving receiver/default aliases through suspension.

- Reject keyword arguments to builtin `list.append` before mutation, including exposed, descriptor-bound, unbound and expanded calls. Preserve keyword binding for user-defined methods.

- Invoke ordinary one-argument builtin `list.append` calls with separate receiver/implementation operands, avoiding bound method construction and argument arrays across distinct lists. Preserve lookup order, dynamic fallback, call guards and suspension lifetimes.

- Reuse invocation-owned bound storage for executable functions whose parameter and local layouts match, removing a second local array and copy. Preserve separate captured cells and retain storage through asynchronous completion and cancellation.

- Remove the redundant executable call cache, its frame arrays and compiler operands/counters. Preserve dynamic call resolution and update generator admission for the removed storage.

- Pass eligible compiled positional calls from existing operand storage without packaged call-argument arrays or async argument-factory closures. Share frame entry/body cleanup with ordinary invocation, preserving live defaults, cancellation, admission and owned arguments across suspension.

- Make def/lambda positional and keyword-default metadata live for argument binding, including mutation, deletion, type checks and supported container subclasses. Await custom-key default lookup, preserve its owner through suspension, and admit parameter-layout/default storage before allocation. Return temporary definition-map allowance after construction instead of retaining its old per-default fee.

- Preserve fresh exposed builtin method objects in synchronous execution, including comprehensions and class bodies. Keep immediate method-call caching separate from attribute reads, preserving lookup-before-argument order and receiver lifetime.

- Share the synchronous integer-addition handler during RunAsync while preserving admission and cancellation. Await user-defined in-place addition and declined/reflected fallback without replaying host effects.

- Preserve eligible executable function bodies, argument slots and captured cells in RunAsync. Await yielding defaults, decorators and body callbacks without replay, and remove blanket async module mirroring. Import stores update their own slots without erasing unrelated locals.

- Compile ordinary break/continue in protected with/try regions instead of abandoning executable lowering. Preserve abrupt branch destinations through normalization so terminal loop exits run cleanup once and discard the correct iterator.

- Align packaged release notes and property/gzip contracts with explicit ownership lifetimes; preserve older collector changes as labeled history.

- Bind builtin json.load/loads options directly into the existing parser, avoiding temporary decoder attributes and bound-method allocation while preserving custom classes, explicit decoders, hooks and error behavior.

- Add Lython-owned shared memory budgets, empty ownership scopes and resizable reservations with immediate cooperative admission and deterministic refunds.
- Give compiled programs and execution results explicit disposal lifetimes; forward the borrowed budget through nested execution and host command requests. Retained aliases require their owners to remain alive.
- Replace weak reclamation registrations, identity tables, paging/sweeps, forced collections and dormant guest-heap traversal with conservative batch ownership. Long streams use bounded host-driven executions; arbitrary reference withdrawal no longer promises an immediate refund.
- Preserve allocation preflights, rollback, cancellation cleanup and Python identity. Correct enumerate alignment after a consumed item encounters result admission failure, and keep refused compilation associated with its supplied budget.
- Migrate collector-only fixtures to shared-budget batches and independent retained-data/admission controls; keep longer stress totals separately. Document approximation and host headroom in MEMORY.md, included in the Release package.

## 0.10.0 - earlier release candidate

This candidate predates the governed-memory migration. Collector-specific entries
below are historical and superseded by Unreleased and [MEMORY.md](MEMORY.md).

- Add immutable per-invocation built-in module selection, enforced across imports, attributes, registries and discovery without granting missing host capabilities.
- Report typed terminal failure origins and guard quantities, preserving provenance through catches/rethrows and bounded diagnostic projection.
- Own exception storage once across constructors, bindings and aliases; reclaim dropped records while retaining funded args, attributes and chains, and preserve native cleanup under exhaustion.
- Expose existing runtime consumption counters without enabling implicit fuel: measured checkpoints, registered host-call attempts and retained UTF-8 capture bytes; add a compile-once policy/limits example and package-consumer controls.
- Return ordinary Python lists from regex findall, including normal type identity, mutation, JSON encoding and CLR projection; reclaim discarded lazy-dot-star results.
- Remove the implicit execution-step ceiling; retain enforced opt-in fuel for existing embedders, independent temporary-reclamation cadence, cancellation and default memory/stack/host-call protections.
- Check cancellation and explicitly requested fuel during every lowered for-loop iteration, including pass-only range loops.
- Observe cancellation and opt-in fuel during shared built-in iteration, including sum, any/all and min/max over primitive iterators; retain list self-extension snapshots and dispose canceled cursors.
- Observe cancellation through stable-sort comparisons/copy tails and native sorting in statistics, JSON, difflib, directory traversal and protocol snapshots; preserve Python failures and refund sorting scratch.
- Check shared execution guards through whole-buffer Latin-1 decoding, non-strict UTF-8 decoding and newline rewriting, including mediated stdin and file windows.
- Refund collected young-tier values before funding live-entry promotion, so reclamation cannot reject a sweep using charges it has already freed.
- Enumerate reclamation pools without promotion during exhaustion relief, so one pool cannot reject a request before collected charges in another pool are released.
- Fund regex cache slots only while its bounded capacity grows; eviction reuses existing funding and purge refunds it.
- Keep returned CSV/text values reclaimable after their source is collected; release abandoned scratch before sweep funding and preserve registry ownership through reentrant exhaustion relief.
- Add sequential gzip.GzipFile wrappers over contained filenames and supplied binary objects, lazy metadata/trailer checks, persistent governed compression, sync flush, awaited callbacks, and correct supplied-object ownership.
- Retain native ownership charges through finalization, expose EOFError/FutureWarning hierarchy, and emit inferred gzip write-mode notices through mediated stderr.
- Add governed zlib-wrapped byte compression/decompression, exact levels, validated headers/truncation/Adler-32, first-stream trailing behavior and awaited option conversion.
- Emit a complete final DEFLATE block for empty gzip payloads and filename-backed writers.
- Measure suite indentation from logical lines, including compound headers whose closing delimiter shares a continuation line.
- Add contained ElementTree parsing, independently governed node/text/map aliases, simple namespace-aware child selectors, Python byte-declaration behavior, awaited index conversions and catchable ParseError identity.
- Expose stable, governed direct-base tuples for supported exception type objects.
- Add governed portable struct packing/unpacking, standard byte orders, IEEE half/single/double fields, Python conversion slots, lazy record cursors and distinct struct.error identity.

- Preserve Python's positive NaN factory constant and explicit negative NaN signs in float, float.fromhex, math.nan and JSON decoding.
- Add governed UTF-16 native/LE/BE codecs, stateful file BOM publication, split-unit/surrogate/newline reader carry, funded awaited outputs and shared byte/path/gzip/URL composition.
- Correct quote_plus source-space selection for encoded UTF-16 units and raise LookupError for unknown runtime codec names.
- Add governed Windows-1252 codecs with a fixed mapping, faithful undefined-byte and scalar errors, shared handlers, binary-mediated text files and owned decoded windows.
- Resolve implicit builtin class identities from the canonical per-run table, preserving type checks, exception roots and singledispatch when guest names shadow object/type or primitive types.
- Add governed sequential binary open/Path.open handles, ranged reads, byte line iteration, staged writes/append, flush/close, shared execution-end publication, faithful option/size protocols and io.UnsupportedOperation.
- Add governed Path.read_bytes/write_bytes through optional host binary methods, with owned read copies, staged publication, byte counts, limits and asynchronous cancellation.
- Apply Python's conditional Greek final-sigma lowercasing with Unicode 15.1 cased/ignorable context, bounded scans and governed scratch.
- Release lower/upper casing scratch when conversion is denied, and scan UTF-8 directly with execution budget checks.
- Add governed structured URL parsing and reassembly, text/byte tuple results, authority properties, lazy port validation, field replacement, codec conversion and a bounded per-execution split cache with guest hash/equality protocols.
- Add governed urllib.parse quoting, percent decoding and ordered query encoding/parsing, including opaque byte fields, callback/option order and asynchronous guest protocols.
- Support Python sequence iteration through type-level __getitem__ when __iter__ is absent, with permanent exhaustion, retryable failures, async descriptor/item dispatch and governed cursor storage.
- Fund reader carry buffers before allocation and retain ownership only after the charge commits, preserving the original MemoryError when an open is denied.
- Add shared ASCII codecs with Python aliases, strict/ignore/replace/backslashreplace behavior, byte-mediated paths and streaming text handles, delayed acquisition/cancellation and governed decoded-window scratch.
- Release raw and decoded reader storage when priming or final handle allocation fails, including asynchronous construction under memory denial.
- Add bounded html.unescape with the complete embedded HTML5 entity table, faithful named/numeric reference rules, containment/truth protocols, governed output and retained upstream license notices.
- Resolve truth testing through type __bool__/__len__ slots, await their descriptors, preserve invalid __bool__ priority and reject overflowing length results.
- Resolve implicit membership through the type's __contains__ slot, bypass instance overrides and attribute interception, await slot descriptors, and reject disabled/noncallable slots explicitly.
- Add governed html.escape with Python ampersand/quote rules, Unicode scalar limits, ordered guest replace/truth protocols and delayed-host cancellation.
- Reject positional collisions with keywords expanded before star arguments, preserving each argument's original parameter slot instead of silently skipping occupied slots.
- Add bounded textwrap.dedent/indent with Python indentation and line rules, ordered guest predicates, delayed protocol/truth effects, cancellation and governed output; keep wrapping APIs explicitly unsupported.
- Match Python 3.13 Unicode printability and repr escaping, including nonprinting separators and astral scalars; render directly from UTF-8 and release scratch on memory denial.
- Correct string splitlines for Python's U+001C–U+001E and NEL boundaries, preserving original endings, Unicode byte positions and byte splitlines rules.
- Correct Python string whitespace classification for isspace, strip variants and whitespace split/rsplit, including U+001C–U+001F and bounded maxsplit; keep byte whitespace separate.
- Add governed io.BytesIO with opaque byte storage, byte positions, shared-cursor reads/iteration, immutable snapshots, checked seeks and lifecycle APIs; await guest index/writelines effects and preserve cursor/content on resource denial.
- Apply string-result limits before consuming StringIO input, and keep its private storage outside guest collection item limits.
- Add governed io.StringIO with Unicode character positions, Python newline policies, shared-cursor iteration, immutable snapshots and lifecycle APIs; compose with CSV, JSON and print, and await guest index/writelines effects.
- Resolve callable instances through the type's __call__ slot, await call descriptors under RunAsync, and bind lambdas stored on classes as function descriptors.
- Support CSV write protocols for text files, mediated standard streams and guest objects; capture write at construction, preserve row/header callback results, await row acquisition and writes, and govern callback-visible row strings.
- Support JSON text-stream protocols for mediated standard streams and guest read/write objects; stream dump through the selected encoder's iterencode, awaiting guest lookup and callbacks and preserving written prefixes on later failure.
- Preserve optional XML-reader attributes when loading XLSX declarations, and make CSV readers their own iterators across ordinary iteration and direct next calls.
- Resolve dataclass helper diagnostics through callable bindings; honor dictionary membership guards and mutable aliases; accept slice assignment when sequence facts are unknown, including direct os.walk pruning.
- Encode Counter/defaultdict through their governed mappings, support regex match subscripts and Python index selectors, and keep oversized group indices catchable as IndexError.
- Document the embedder-supplied complete argv contract and extend the exact-package consumer with delayed host acquisition, file publication and final-byte checks.
- Reject keyword collisions introduced by `**` during argument expansion, including dictionary and Counter constructors; reserve expansion scratch before allocation.
- Add `json.JSONDecoder`/`JSONEncoder` instances, `raw_decode`, incremental `iterencode`, guest subclass overrides and the `cls` customization path.
- Sort original JSON dictionary keys before applying `skipkeys`, sharing the order between eager and incremental encoders.
- Add a public syntax corpus and explicit grammar inventory; correct assignment support documentation and diagnose deferred Python syntax as unsupported before effects.
- Isolate CPython differential imports from shared temporary directories and ambient modules, restoring the chunked-read validation gate.
- Await JSON hooks and selected-class construction/dispatch through `RunAsync`, including incremental encoding, cancellation and delayed host effects.
- Make JSON encoder option writes authoritative for subsequent encoding and expose writable decoder attributes with CPython's constructor-captured scanner behavior, including subclasses.
- Accept f-string fields that reuse their enclosing quote character, including adjacent literals and quoted expressions in format fields.
- Support repeated comprehension filters and starred arguments after keywords, preserving Python's short-circuit and argument evaluation order.
- Reject duplicate parameters/keywords, invalid comprehension assignment expressions, inconsistent pattern captures and out-of-scope loop control before execution can perform effects.
- Decode JSON prefixes without scanning or reserving unused suffixes; fund lenient control rewriting per string before allocation and reject strict strings before dependent hooks.
- Dispatch guest `__getitem__` consistently in lowered execution and await its effects under `RunAsync`.
- Evaluate decorator expressions before function defaults and class bases/bodies, then apply decorators in reverse order, including through asynchronous hosts.
- Corrected logical `not` precedence relative to comparisons and arithmetic, while rejecting invalid arithmetic placement of `not`.
- Fixed nested `def` functions failing to capture transitively free variables once the definers returned (`NameError`); closures now bind through intermediate scopes with nearest-rebinding semantics.
- Per-call `*args`/`**kwargs` materializations now release when dropped instead of accumulating across calls, while retained results stay charged.

## 0.9.0 - 2026-09-13

This release delivers the contained ZIP subset, standard-library hardening and bounded CSV streaming developed since 0.8.0, with memory-governor containment extended across readers, writers and conversions.

- Added a contained `zipfile` subset across `r`, `w`, and `a` modes, with ordered duplicate-preserving listings, STORED/DEFLATED member reads, CRC validation, staged archive creation, single-writer member handles, append support, and destination-contained extraction through host capabilities.
- Isolated mutable builtin module exports between executions while preserving their identity within each run.
- Added work and memory checks to `difflib.SequenceMatcher`, bounded XLSX entry expansion with length/CRC validation and XML depth checks, and reused loaded workbook parts and parsed style XML.
- Bounded additional parser recursion and expression-chain shapes, rejected byte limits before narrowing, and widened execution counters.
- Indexed ZIP member lookup by name (last wins, renames invisible) instead of rescanning the directory per call. Gave difflib matcher views stable identity with invalidation and governed row-scratch reuse. Finished user-iterator dispatch: `iter()` returns the validated `__iter__` result (preserving identity), `next()` advances instances directly, and `__iter__`/`__next__` callbacks await suspension on delayed hosts.
- Fixed large gzip read sizes, closed-handle context entry, and finalization of valid writes when a context body raises an unrelated exception. Prevented a failing context exit from being invoked twice. Routed ZIP protocol exit through CloseAsync and added an awaiting write() implementation so RunAsync works through hosts whose stat/read operations actually suspend. Rejected ZIP data mutations after close, made fresh appends publish a valid empty archive while unmodified existing appends still publish nothing, and kept failed closes retriable. Validated ZIP record field widths before staging/narrowing, fixed ZIP64 end counts to merged totals, preserved unrelated extra fields across ZIP64 promotion, resolved local ZIP64 sizes on read, and made integer magnitude bit-length allocation-free.
- Restricted static path text-I/O checks to known path receivers, consumed `sorted()` inputs before invalid-key rejection, delayed invalid sort/min/max key errors until the key would actually be called (empty input with a bad key now succeeds), accepted `json.dumps(indent=True/False)` like 1/0, preserved `ZipInfo` `date_time` booleans, and aligned more integer argument positions with Python's boolean-as-integer behavior.
- Fixed `dir()` to advertise resolvable members, removing unimplemented entries and rejecting unsupported objects explicitly; added module export, exception ancestry, alias, and return-shape parity coverage.
- Shared UTF-8 line scanning and codec facts, supplied explicit host-derived XLSX timestamps, tightened path metadata and callable-layout contracts, and centralized copying of run options when linking cancellation.
- Moved public behavior coverage into the suite that references the production assembly, retaining source-linked tests for internal invariants. Made `csv.reader`/`csv.DictReader` single-pass iterators (indexing, slicing and `len(...)` now fail explicitly, `bool(...)` is always true, rendering never pulls input); dropped field charges are reclaimed as the scan advances while retained aliases stay charged. Added archive, workbook, gzip-flush, SequenceMatcher, and async-materialization benchmarks with recorded allocation and host-traffic baselines.
- Stream `open()` file reads in fixed windows through new `ILythonHost` ranged reads instead of loading whole files, so full scans no longer scale with discarded content; ranged reads compose whole-file transfers by default and hosts with native ranged I/O should override them. Advance `csv.reader`/`csv.DictReader` through the async host boundary in async runs, including header inference. Run `except` handler suites in the enclosing scope in every execution, matching CPython (only the `as` variable is deleted). Share string and bytes literals as compile-time constants in tree-walked runs instead of recharging them per evaluation; render `int()` failure literals lazily like `float()`; commit durable ownership for escaping `getvalue()` results so retained results stay charged. Opening a file larger than the execution budget no longer fails up front when streaming fits; per-commit charges and host-read caps govern instead, and strict decoding failures past the first window surface lazily like CPython.

## 0.8.0 - 2026-08-11

This release closes the host-embedder compatibility findings gathered from coding-agent workloads while preserving Lython's explicit, host-mediated execution model.

### Python Language Compatibility

- Added unparenthesized tuple expression lists, collection-display unpacking, dotted imports with Python package binding, and implicit continuation throughout compound-statement headers.
- Added old-style string interpolation and the ordinary non-mutating and mutating set method families.
- Improved static flow analysis for short-circuit `None` guards and branch-local sequence length bounds, and made `Counter` report its number of stored keys through `len(...)`.
- Made `pathlib` construction, joining, and `absolute()` preserve lexical parent segments while `resolve()` and host-mediated operations continue to canonicalize them.

### Standard Library Surface

- Added context-correct `builtins`, contained `importlib` discovery, host-mediated `filecmp.cmp`, managed incremental `hashlib`, bounded in-memory and host-backed `gzip`, functional and iterable `shlex`, and host-clock-backed `time` support.
- Added Latin-1 codecs across the supported text APIs while continuing to route non-UTF-8 file bytes through the host's bounded binary capability.
- Added a lazy, bounded `subprocess.Popen` facade and in-memory pipeline composition without exposing live processes, PIDs, unmanaged pipes, signals, or ambient shell authority.

### Security And Containment

- Closed the initial-globals boundary over copied Lython values, rejecting arbitrary CLR objects, callbacks, reference cycles, and excessively nested object graphs before execution.
- Redacted CLR runtime object names from guest-visible rendering and made public return projection fail closed on unsupported internal objects.
- Canonicalized every filesystem path at the host gateway, rejected Windows-ambiguous contained paths, and normalized subprocess working directories inside the host namespace.
- Redacted host exception messages from guest-visible failures, supplied contained cwd/environment defaults for subprocess requests, and made shell invocation an explicit runner opt-in.
- Charged standard streams, local-import discovery, package discovery, append preflight, and clock reads against the host-call budget.
- Bounded source length and syntax nesting before frontend allocation, and enforced subprocess output limits across combined captured stdout and stderr.
- Replaced OS-native extension parsing on OpenPyXL media paths with contained lexical suffix handling.

### Reliability And Embedding

- Made every unsuccessful execution carry a non-zero process-compatible exit status and aligned synchronous/asynchronous execution behavior with differential coverage.
- Reject asynchronous host effects before synchronous execution can perform preceding side effects.
- Hardened captured subprocess output, gzip buffers, incremental hash state, governed byte accumulation, pipe lifecycle, and exception-handler stack restoration.
- Made executable context managers invoke `__exit__` exactly once when an exception is not suppressed, and made every execution path pass Python-shaped exception classes and instances to `__exit__`.
- Made zero-valued execution limits enforce real zero budgets, reject negative limits explicitly, and retain omitted limits as the only default/unlimited case.
- Removed avoidable buffered-output copies and quadratic work from text handles, sized Unicode reads, `functools` caches, gzip CRC processing, traceback collection, and large-integer base formatting.

### Engineering

- Tightened public state contracts, nullability invariants, host implementor documentation, and optional-parameter usage to follow the repository's C# guidelines.
- Removed friend-assembly access and unnecessary unsafe compilation, enabled warnings as errors, made benchmarks part of the solution, and normalized source formatting.
- Centralized invariants shared by synchronous and asynchronous execution while keeping their effect-awaiting loops explicit, removed the duplicate memory estimator, and cached stable builtin member bindings per execution.
- Replaced nullable/string-tagged call arguments and generic executable instruction payloads with explicit domain types, and unified static user-call binding across functions and methods.
- Canonicalized builtin module inventories and datetime operator contracts, and split oversized runtime, static-analysis, and lowering units by responsibility.
- Typed callable categories, dataclass options, and CSV argument layouts; shared lowered statement dispatch and `try` control flow across synchronous and asynchronous execution.
- Split the remaining responsibility-mixed parser, evaluator, OS, functools, difflib, itertools, statistics, random, JSON, and shlex implementation units without changing the pending package version.
- Introduced a black-box public-assembly smoke suite alongside the source-linked white-box suite, establishing the boundary that later compatibility scenarios can exercise without source linking.
- Removed unused runtime-specialization scaffolding and consolidated static value descriptions and text-boundary proofs so diagnostics cannot drift between analysis passes.
- Replaced loosely nullable abrupt-execution and subprocess policies with closed domain types, made worksheet range rewrites linear, and removed proportional allocations from common text slices and formatting.
- Shared lowered expression dispatch and formatted-string assembly, then partitioned the remaining module, member, collection, static-analysis, and OpenPyXL outlier files by responsibility.
- Preserved successful-path abstract facts through caught `try` bodies, centralized structural syntax traversal, and documented eager versus deferred expression boundaries.
- Replaced ambiguous iteration, abstract-value, cached-resolution, and workbook-save states with closed domain contracts; separated readable from mutable dynamic attributes.
- Closed execution outcomes and abstract-value payloads over explicit state types, compacted named-call presence tracking, and documented the omitted-versus-explicit-`None` binding invariant.
- Centralized host-exception translation, localized incidental single-caller helpers, and split path-host dispatch and executable-frame interpretation into cohesive components.
- Added a maintainer architecture guide covering compilation, execution paths, containment invariants, runtime protocols, and verification boundaries.
- Centralized protocol-aware operator dispatch across execution paths and made Python sorting stable and subquadratic with governed scratch storage and one key evaluation per item.
- Replaced weak lowered-assignment, text-flush, codec-option, and missing-capability states with closed variants or typed failures; normalized host text at the boundary and rejected raw CLR strings from runtime switches so `PyString` remains the sole runtime text representation.
- Consolidated tuple-like recognition, OpenPyXL style inventories, abstract-value capabilities, lattice joins, and function-local binding facts; removed the superseded internal APIs those sources of truth replaced.
- Removed redundant collection materialization, shared print binding, sequence-unpacking and spreadsheet-column rules, indexed workbook names and styles, and partitioned worksheet validation, formatting, and media models.
- Replaced nullable callable layouts, executable local slots, and string-tagged OpenPyXL style payloads with explicit domain states; removed the obsolete path-stat compatibility constructor.
- Reused structural syntax traversal, materialized OpenPyXL package update plans once, and separated percent-format parsing, brace-display parsing, regex contracts, callable catalogs, and workbook responsibilities into cohesive units.

## 0.7.0 - 2026-07-02

This release improves Lython's behavior as a non-surprising Python substitute for fresh coding agents within Lython's contained scope.

### Language And Parser Compatibility

- Added the scoped bytes value model needed by contained OpenXML workflows without adding binary file handles or unmanaged byte I/O.
- Added set comprehension support, including nested clauses, condition filters, async execution coverage, and static flow handling.
- Closed parser gaps around grouped multiline layouts, adjacent string literal concatenation, and common repairable syntax failures.

### Callable Compatibility

- Aligned high-value builtin keyword binding with Python spellings and common aliases.
- Tightened stdlib and method call shapes across commonly generated agent code, including CSV writer method signatures.

### Text IO Compatibility

- Normalized builtin `open(...)` and `Path.open(...)` text-option contracts for `mode`, `encoding`, `errors`, and `newline`.
- Made text handles behave like linear text streams: `read`, `readline`, `readlines`, and iteration now share one cursor.
- Made text handle `tell()` report deterministic UTF-8 byte positions and made `flush()` publish writable buffers without duplicating append writes.

## 0.6.0 - 2026-06-15

This release tightens Lython's Python-shaped scripting surface for fresh coding agents while preserving host-mediated containment.

### Python-Shaped Script Surface

- Removed Lython-specific filesystem globals from default script globals; scripts should use `open`, `pathlib`, `os`, and `shutil` instead.
- Added a contained `shutil` subset for common file-copy and move workflows, including `copyfile`, `copy`, `move`, `copyfileobj`, `SameFileError`, and `Error`.
- Kept `__file__` as conditional source-backed script/module state rather than a regular builtin.

### Builtin Compatibility

- Added numeric and text builtins including `abs`, `pow`, `round`, `bin`, `oct`, `hex`, `chr`, `ord`, `ascii`, `format`, `callable`, and `hash`.
- Added iterator and sequence helpers including `iter`, `reversed`, `map`, `filter`, and explicit `slice` objects.
- Added object inspection helpers `getattr`, `hasattr`, `setattr`, `delattr`, `dir`, and `vars` for supported Lython values.
- Expanded builtin exception names and catch behavior with Python-shaped categories such as `BaseException`, `ArithmeticError`, `LookupError`, `ModuleNotFoundError`, `RecursionError`, and `MemoryError`.

### Static Analysis And Documentation

- Synchronized runtime builtin registration, static builtin-name inventories, `sys` discovery, `SPEC.md`, and tests.
- Clarified agent guidance around Lython as a strict, extensive Python subset and disabled the .NET terminal logger by default through repository configuration.
- Made NuGet packaging explicit and Release-only so Debug build/test outputs cannot be mistaken for release artifacts.

## 0.5.0 - 2026-06-10

This release expands Python compatibility for coding-agent scratch scripts and workbook automation while preserving Lython's host-mediated containment model.

### OpenPyXL Compatibility

- Added built-in `openpyxl` compatibility for common workbook, worksheet, cell, style, comment, table, chart, drawing, validation, protection, and package-metadata workflows.
- Added fixture coverage for Excel-authored workbooks and openpyxl round trips, including formulas, shared strings, styles, relationships, worksheet XML, comments, tables, validations, conditional formatting, protection, VBA preservation, drawings, images, named styles, and structural metadata.
- Added static contracts and sealed member surfaces for the supported `openpyxl` import paths and objects.

### Python Language And Runtime Compatibility

- Expanded list compatibility with `index`, `count`, `insert`, `remove`, indexed `pop`, `reverse`, stable `sort`, repetition, augmented repetition, lexicographic sequence ordering, slice assignment, and slice deletion.
- Generalized assignment and augmented-assignment targets to support subscript, attribute, nested, slice, and openpyxl-style cell-value writes while preserving single-evaluation target behavior.
- Added `global` and `nonlocal` parsing, diagnostics, runtime binding, lowered execution, executable execution, closure-cell writes, and directive-aware static analysis.
- Locked down baseline helper behavior for generated scripts, including `sum(...)`, `subprocess.check_output(...)`, and `Path.read_text(...)` option handling.

### Standard Library Surface

- Expanded `csv` with `DictReader`, `DictWriter`, file-backed reader/writer support, multiline quoted records, common dialect options, quoting constants, and `csv.Error`.
- Expanded `difflib` with junk predicates, `Differ`, `HtmlDiff`, `diff_bytes`, richer `SequenceMatcher`, grouped opcodes, close-match scoring, and documented eager materialization where Lython deliberately differs from lazy generators.
- Expanded module and introspection helpers across `pkgutil`, `sys`, `argparse`, `dataclasses`, and `typing` for agent-generated scripts and static validation.
- Expanded contained filesystem and path helpers across `pathlib`, `os`, `os.path`, and `glob`, including path-like handling, host-mediated traversal, environment maps, cwd/stat/scandir-style values, hidden-file globbing, `root_dir`, `include_hidden`, and `glob.translate`.
- Expanded numeric, date/time, and data helpers across `decimal`, `math`, `datetime`, `statistics`, and `random`, including contexts, rounding modes, combinatorics, distributions, fixed-offset timezone behavior, grouped medians, quantiles, regression, deterministic `Random`, state round trips, and distribution helpers.
- Expanded `collections`, `itertools`, and `copy` with common containers, lazy data-wrangling helpers, explicit memo deep copies, `copy.replace`, hooks, cycle preservation, and compatibility aliases.
- Expanded `operator` with direct-function equivalents for supported unary, binary, comparison, item, sequence, in-place, and callable operations.
- Expanded `functools` with wrapper metadata helpers, caching decorators, `partialmethod`, `cached_property`, simple dispatch helpers, and recursive repr support.
- Expanded `re` with catchable regex errors, more flags, compiled-pattern and match metadata, group helpers, lazy `finditer`, range arguments, and richer replacement templates.
- Expanded `fnmatch` with `fnmatchcase`, `translate`, bracket character classes, negated classes, ranges, and deterministic case-sensitive matching.
- Expanded `json` with text-file `load`/`dump`, hooks, parse callbacks, formatting controls, `JSONDecodeError`, circular-reference checks, and explicit custom encoder/decoder diagnostics.
- Expanded `subprocess` with `CompletedProcess`, `CalledProcessError`, `SubprocessError`, `list2cmdline`, and checked-failure behavior shaped like CPython while keeping execution host-mediated.

### Host Surface And Containment

- Added `LythonRunOptions.Environment` so hosts can supply the contained environment used by `os.environ`, `os.getenv`, `os.putenv`, `os.unsetenv`, `os.get_exec_path`, and subprocess `env` handling without reading the ambient process environment.
- Added optional `ILythonHost.ReadBytesAsync(...)` and `ILythonHost.WriteBytesAsync(...)` hooks for host-mediated binary file workflows; the default interface implementations keep binary I/O unavailable unless a host opts in.

### Static Analysis And Documentation

- Expanded static contracts, return-shape flow, host-requirement checks, callable/member surfaces, and diagnostics across the new language and module surfaces.
- Updated README and SPEC coverage for the supported Python-shaped subsets and the explicit containment boundaries for filesystem, environment, and subprocess access.

## 0.4.1 - 2026-06-05

- Added `LythonSubprocessCompletion.CompleteBufferedAsync(...)`, a public host-side helper for applying subprocess stream modes to buffered stdout and stderr.
- Covered `PIPE`, inherited streams, `DEVNULL`, `STDOUT` redirection, and output-bound handling for host subprocess runners.

## 0.4.0 - 2026-06-04

This release broadens Lython's Python compatibility for coding-agent scratch scripts and text-editing workflows while preserving the host-mediated containment model.

### Python Language Compatibility

- Added multiline list, tuple, set, dictionary, call, and subscript syntax support for common Python formatting styles.
- Expanded f-string support, including format specifiers, conversions, debug expressions, raw/triple-quoted variants, and nested expressions.
- Added one-line compound suites for `if`, loops, `try`, `with`, `match`, class, and function definitions.
- Filled in common builtins used by scratch scripts, including practical `iter`, `next`, `sum`, `min`, `max`, `abs`, `round`, `enumerate`, `zip`, `reversed`, `all`, and `any` behavior.

### Text Editing And File Workflows

- Made file handles first-class for supported text I/O, including `close()`, closed-state checks, iteration, and context-manager behavior.
- Added richer path and filesystem compatibility across `pathlib` and `os`, including path-like arguments, path properties, globbing, stat/scandir-style objects, rename/replace/remove helpers, and directory creation variants.
- Added `sys.exit(...)` support with structured exit results.

### Standard Library Surface

- Added `difflib` support for unified/context diffs, ndiff/restore, close matches, and `SequenceMatcher`.
- Added `pkgutil` module discovery helpers for the supported import model.
- Expanded `datetime` compatibility for date/time constructors, parsing, formatting, arithmetic, comparisons, and timedeltas.
- Improved `re.findall(...)` list compatibility.

### Host-Mediated Subprocesses

- Broadened the optional `subprocess` module beyond `run(...)` with `call(...)`, `check_call(...)`, and `check_output(...)`.
- Added `subprocess.PIPE`, `STDOUT`, and `DEVNULL`, plus host request fields for stdin/stdout/stderr modes, `shell`, text mode, encoding, errors, environment, timeout, and output bounds.
- Added `CompletedProcess.args` and `CompletedProcess.check_returncode()`.
- Kept process execution explicit and host-mediated; embedders still decide whether subprocesses exist and how requests are authorized.

### Static Analysis And Packaging

- Expanded static contracts for the new Python, filesystem, datetime, module, and subprocess surfaces.
- Added package release notes metadata and packaged this changelog with the NuGet artifact.
