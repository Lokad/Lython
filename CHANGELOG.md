# Changelog

## 0.11.0 - Unreleased

- All filesystem host methods now receive LythonPathRequest, including both copy,
  move and replacement operands. Remove the pre-normalizing dispatcher and preserve
  actual path components through directory traversal, recursive creation and imports.
  Share os/pathlib/ZIP directory creation while preserving prefix effects and
  Python-shaped errors. CopyAsync now owns regular-file overwrite; remove shutil's
  delete-before-copy detour and admit its result before mutation. ZIP/shutil path
  protocols await host work, and glob/scandir preserve relative result paths.

- Path predicates distinguish missing/non-directory traversal from other failures;
  missing stat/size/time access raises FileNotFoundError. os.path predicates suppress
  filesystem OSError, while pathlib preserves permission failures as in Python 3.13.
  Admission, cancellation and capability failures remain explicit.

- Bind writable ZIP targets at construction and retain that opened identity through
  publication and cleanup. Append uses the new optional owned-file
  RewindAndTruncateAsync capability after complete output admission; guest seek
  modes remain unsupported. Share open/publication paths, remove late path writes
  and duplicate output charges, and retain serialization ownership through actual
  I/O. Execution cleanup closes open archives without publishing cancelled staging.

- Decide synchronous host completion from one observed state, removing the race
  that could accept pending work as it completed. Join actual file/process users
  and retire unpublished results before reporting the contract failure. Preserve
  completed-error translation and cancellation.

- Retire ZIP archives after failed close and consolidate synchronous/asynchronous
  close behavior. A second close never retries publication; cached listings remain
  available. Clear private input aliases before refunding, while retained staged
  storage stays charged. Explicit close still rejects a live member writer.

- Breaking host API: remove legacy whole-file/range/text read, write and append
  members from ILythonHost. Path helpers, imports and archive input use owned
  byte files with pre-admitted read windows and funded final-copy overlap.
  Whole-file writes borrow admitted data until completion; filecmp compares two
  opened cursors and Path.touch uses create/exclusive intent. Path.write_text
  truncates before encoding errors and exclusive touch raises FileExistsError,
  matching CPython. Private whole-file input returns its allowance after use;
  returned bytes transfer their funding without another copy.

- Text `open()`, `Path.open()` and `argparse.FileType` use the same owned byte-file
  contract as binary handles. Remove stat/open races, path-based refill copies and
  encoding-specific host transports. Keep Python codec/newline/BOM semantics and
  fund encoded output through actual writes; unchanged UTF-8 staging is borrowed.
  All opened readers participate in awaited execution cleanup.
  Text direction errors use `io.UnsupportedOperation`; entering a closed text
  handle raises `ValueError` before executing the body.

- Binary `open()` and filename-backed gzip now require the host's owned
  `ILythonByteFile` capability.
  Preserve opened identity across rename/replacement, read directly into admitted
  buffers and prevent failed partial writes from replaying accepted prefixes.
  Await file disposal on cancellation and failed acquisition before refunding.

- Retire text/binary handles in finally when close reports a flush failure;
  release staged storage and prevent a second close from replaying publication.
  Explicit failed flushes retain their existing retryable buffers when appropriate.

- Scan format fields and nested specifications directly as UTF-8, removing whole
  field decoding, nested-spec re-encoding and private retained result copies.
  Fund metadata and output through the active execution owner, including compiled
  literals; forward cancellation through ordinary str.format and resumed lookups.
  Preserve Unicode field indexes/error ordering and Python's conversion diagnostics.

- Breaking provider API: Prepare receives the original UTF-8 subject and an
  explicit scalar window; captures now use absolute original-subject coordinates.
  Remove copied subject segments and incorrect anchor/lookbehind behavior; use
  the same bounds during constant analysis. Fix omitted keyword defaults while
  preserving explicit None errors, and clamp native-sized indexes before narrowing.
  The optional 0.4 adapter explicitly reports its reversed-match-window limitation.
  Reversed bounds in the nonstandard substitution extension now raise ValueError
  before callbacks, replacing the old overlapping-prefix/suffix result.

- Breaking provider API: ILythonRegexPattern now implements IDisposable. Compile
  transfers a fresh owned handle; frontend analysis and execution close handles
  after their users finish, including unpublished returns and failures. Retained
  provider caches keep separate funding. Regex purge relinquishes table capacity
  before refund; conservative slot funding includes growth/replacement overlap.

- Breaking host API: replace the bare LythonSubprocessResult record with a
  disposable completion and an explicit funded owner. Buffered capture routing
  preflights/copies retained bytes; direct consumers retire the owner after use,
  and Popen keeps it through pipeline cleanup. Remove duplicate raw-output
  charging and the request's internal governor hook. Retire late results from
  falsely synchronous hosts after actual completion.

- Decode valid escaped JSON strings directly into UTF-8 at every token size,
  preserving scratch/result overlap, strictness, offsets and independent ownership.
  Remove stale checkpoint tables and callback-depth expectations from JSON tests.

- Parse match.expand templates directly as UTF-8 into funded output, removing
  its ungoverned StringBuilder and whole-template/capture decoding. Correct octal
  escapes, unknown escapes, group errors and scalar error positions; preserve
  captures through suspension/purge and cancel within native processing.

- Retain funded native regex group-name metadata once per pattern, removing
  repeated name conversions and sorting from match metadata and named lookup.

- Key the execution regex cache by native UTF-8 text and complete compile options,
  removing decoded lookup copies and preventing incorrect reuse when parsing-time
  verbose behavior differs despite identical reported flag bits.

- Add typed LythonHostException failures with safe Python error categories,
  including BrokenPipeError, and preserve terminal host-reported memory refusal
  without requiring an external sticky error. Settle actual users before unwind,
  including an incorrectly advertised synchronous operation. Distinguish direct
  subprocess launch errors from process return codes; keep unknown errors redacted.

- Keep namespace capacity and binding preflights funded through publication,
  without refund/re-admission windows against a shared host allowance. Reject
  lossy CLR dictionary/set projection, including distinct NaN collisions, and
  honor cancellation during successful result projection.

- Make list.sort callbacks observe an empty receiver, reject callback mutations,
  and preserve original storage through errors, cancellation and asynchronous
  work. Reuse the original backing instead of allocating a replacement list.

- Dispose benchmark results after consumption and replace obsolete uninterrupted
  governor scans with explicit bounded host batches. Remove orphaned static
  validation/runtime helpers and duplicate success-projection code.

- Reuse prepared function/class/lambda scope facts across frontend analysis,
  lowering and class creation, removing repeated scope collection and separate
  class-cell flags. Preserve lexical scoping and existing execution routes.

- Support user-class `__class_getitem__` through existing descriptor/call machinery,
  including asynchronous host effects, ordinary implicit classmethod binding and
  raw slice keys. Keep lazy class annotations live while separating retained body
  namespaces and preserving comprehension scope boundaries. Recognize function/native runtime type objects and method wrappers
  in type checks, and report the type of slice values.

- Update the repository-only regex adapter to PythonRe 0.4.0. Forward cancellation
  through actual backend work, use lazy native cursors and scalar byte ranges,
  preserve last-closed captures and fund retained replacement plans. Remove the
  buffered Unicode restriction, coordinate fallback and special `.*?` machinery.
  The core package remains independent of Utf8Regex.

- Advance ordinary positional executable calls through explicit caller/callee
  activations, preserving bound storage through suspension and cleanup. Keep
  existing general calls, native callbacks, simple returns and stack admission.

- Resolve str.format_map fields lazily through Python item lookup instead of
  copying a dictionary. Support custom mappings, live mutations and unused
  non-string keys; await supported lookup/format callbacks under RunAsync and
  release formatting scratch on failure or cancellation.

- Build for .NET 11 and .NET 10. Pin the .NET 11 RC3 SDK for development;
  probes and ordinary CI tests default to .NET 11. Package consumers verify
  the library assets and actual runtime on both frameworks.

- Produce exactly one final DEFLATE block for empty gzip members on both
  runtimes, and match Python's level-zero gzip.compress header and empty ZIP block.

- Make white-box tests reference the built core and adapter instead of compiling
  their sources again. Separate syntax semantics from focused compiled-code reuse.

## 0.10.0 - 2026-10-29

- Package only Lokad.Lython. Keep the optional Utf8Regex adapter as a
  non-packable repository project, with direct project-reference integration.

- Consolidate difflib onto borrowed native inputs, governed canonical indexes and
  cached Python block/opcode lists. Remove detached snapshots and post-allocation
  accounting; preserve replacement rollback and funding for extracted aliases.
  Await matcher equality/truth callbacks, honor predicate truth and same-object
  setters, and replace the bespoke Match record with ordinary namedtuples and
  admitted public projection. Correct context-width overflow, omitted defaults,
  NaN cutoff and option validation order. Unified/context diffs, restore and
  grouped opcodes now return lazy iterators; ndiff/Differ/byte output remains eager.
  Explicit None byte metadata is rejected rather than substituted with defaults.
  Keep synchronous container context outside awaits and admit actual working
  capacities, preserving execution isolation and bounded repeated-character work.

- Normalize finite moment inputs to their common exact binary scale, avoiding
  unnecessary wide-integer intermediates while retaining exact rounding.

- Release unpublished NormalDist sample/quantile backing on cancellation or
  construction failure, preserving source funding and shared random state when
  capacity admission fails.

- Replace low-accuracy erf/erfc and inverse-normal approximations with managed
  series/fraction and AS241 kernels. Remove retained coefficient arrays and
  qualify near-zero, central, tail and special values against independent vectors.

- Make NormalDist sampling share execution random state, with independent explicit
  seeds and admitted output capacity; remove its second random engine and staging
  copies. Preserve the existing random module generator. Await sample index and
  constructor comparison/float callbacks, distinguish explicit None, and correct
  zero-division and NaN equality without changing container identity shortcuts.

- Consolidate LinearRegression onto ordinary governed namedtuples: tuple
  semantics, explicit None/arbitrary fields, canonical metadata and awaited
  callbacks. Remove the duplicate record and numeric-only frontend restrictions.
  Project namedtuples through the existing admitted CLR sequence boundary.

- Share exact variance/stdev/sample-distribution calculations, preserving finite
  large-offset results and representable square roots without intermediate
  variance overflow. Admit major arithmetic workspace before calculation.

- Settle unexpectedly pending synchronous host operations before failure cleanup
  releases borrowed storage. Close failed text/binary handles without retrying
  writes. Hosts that violate their completion promise can delay cleanup.
- Use one mutation-visible deepcopy memo, preserve escaping memo ownership and
  remove unnecessary shallow/atomic graph bookkeeping. Correct shallow hook
  lookup, None defaults and dict/Counter callback order.
- Remove the inert copy.dispatch_table export. This is an intentional subset
  correction: unsupported registry access now fails explicitly.
- Consolidate the complete Python license into THIRD_PARTY_NOTICES.md; remove
  the redundant file from source and packages.

- Resolve context-manager special slots on their type, await descriptors and exit
  truth hooks, and preserve active exceptions during lowered cleanup. Normal exits
  ignore the result, while noncallable exits fail when cleanup is invoked.
- Make random rejection loops cancellable, admit wide range arithmetic before
  drawing, and build diagnostics only on errors. Keep large virtual populations
  indexed, borrow native sample counts, remove incidental draw/expanded-position
  width limits and distinguish explicit None from omitted randrange defaults.
  RunAsync awaits randrange index hooks; established small-domain draws stay stable.

- Subprocess exceptions retain their actual positional arguments and one live
  field dictionary. Restore the stdout/output alias and Python messages, awaiting
  metadata callbacks under RunAsync. Construct exceptions without eager diagnostic
  string copies; admit requested rendering through the shared allowance.

- Make shlex consume its exposed stream lazily, including native StringIO and
  host stdin, and await equality/truth callbacks. String inputs borrow immutable
  storage until a write admits writable backing. Remove whole-input lexer
  snapshots and the Int32 line counter.

- Preserve time.strptime field records, including leap seconds and the default
  leap day, without requiring a datetime. Share field-based strftime formatting
  while retaining datetime-specific validation. Correct CPython probe comparison
  of ordinary matching failures without changing command failure exit status.

- Consolidate spreadsheet values and metadata into owned cells, preserving
  detached aliases, blank cells and empty appended rows. Admit native cell/sheet
  capacity and structural replacement storage; keep retained capacity charged.
  Worksheet iterator APIs now yield on demand, correcting previously accepted
  tuple indexing; actual worksheet subscripts and slices still return tuples.
  Await append-source callbacks under RunAsync, share style IDs with serialization,
  preserve loaded style edits and admit the final save copy before allocation.

- Reuse admitted namedtuple field-name objects in dictionary helpers, preserving
  key aliases and removing duplicate UTF-8 conversions.

- Validate Decimal signal dictionaries through their canonical keys, removing
  repeated dictionary enumeration while preserving malformed-map rejection,
  boolean checks and aliases. Reject protocol keys without guest callbacks.

- Consolidate parameterized legacy typing aliases onto governed generic-alias
  storage and substitution. Keep canonical arguments/origins, preserve annotation
  and Callable inspection shapes, and remove private argument arrays, repeated
  origin objects and CLR join rendering. Admit rendering before major copies and
  bound nesting through existing managed guards. Give unions order-independent
  equality/hashing while preserving display order and legacy/builtin distinctions.

- Make functional and supported class TypedDict declarations construct ordinary
  dictionaries, sharing dict binding and async iteration. Retain evaluated
  annotations and boolean total metadata; delete fabricated Any maps, names-only
  inventories and the multi-mapping constructor. Reject unsupported declaration
  options explicitly and remove temporary positional arrays from dict creation.

- Retain NewType base/name metadata, use ordinary factory binding and positional
  identity calls, and govern substantial names and rendering. Remove the orphaned
  legacy TypeVar constructor and callable.
  Await TypeVar and ParamSpec flag callbacks during asynchronous execution.

- Consolidate functional and class-based typing.NamedTuple onto ordinary
  namedtuples. Preserve evaluated annotation maps and aliases, validate binding
  and fields, await asynchronous field iteration, and admit rendering, metadata
  and replacement storage. Delete the duplicate typing tuple implementation.

- Isolate mutable sys.version_info and functools CacheInfo type metadata by
  execution, preserving identities within a run and funding retained metadata.

- Admit namedtuple binding and `_make` buffers before allocation, share tuple
  materialization and validate arity before committing storage. Remove the
  assignment bitmap and extra final copies; refund failed construction.

- Admit namedtuple replacement storage before copying and adopt the result array
  once. Preserve source aliases, refund interrupted construction, and report
  unknown fields with Python 3.13's TypeError.

- Keep provably exact, precision-fitting Decimal addition/subtraction/multiplication
  in native storage. Preserve context validation, zero signs and quantum; retain
  admitted ratios for rounding, scale loss and uncertain overflow cases. Remove
  GetBits array copies and repeated finite power loops.

- Round finite Decimal square roots through context precision/signals using
  the existing admitted integer-root implementation. Preserve half-even ties,
  exact quantum and zero signs. Delete CLR floating-point approximation paths:
  exp/ln/log10 retain exact constants/powers of ten; other valid requests now
  raise NotImplementedError rather than return inaccurate Decimal precision.

- Use context-width coefficient arithmetic for Decimal shifts/rotation; accept
  selected contexts and preserve quiet digit truncation, signs and quantum.
  Delete string/tuple staging, one-digit aliases and the result-alias wrapper.
  Filter Counter signs before contextual subtraction; remove the separate
  negation helper and unary delegates, and fund borrowed-input results.

- Apply Decimal context precision/signals to scaleb, remainder_near and logb.
  Replace overflowing CLR scaling factors and prematurely rounded remainder
  division with admitted exact ratios. Honor quotient precision and zero-result
  trap selection; reject unsupported zero-exponent clamping explicitly.

- Apply Decimal context rounding/signals to normalization and min/max selection.
  Correct signed-zero, negative representation and magnitude-tie ordering;
  remove text normalization, temporary comparison tuples, duplicate finite
  comparison validation and an orphaned digit comparator.

- Consolidate Decimal rounding into admitted exact coefficient arithmetic;
  remove separate CLR scaling formulas and out-of-scale round shortcuts.
  Honor quantize precision/signals, distinguish exact and quiet integral
  methods, and preserve positive quantum and signed zero. Decimal round with
  ndigits now rejects unsupported scales instead of returning an alias or zero.

- Replace linear Decimal power with admitted exact binary exponentiation and
  context rounding. Preserve constant outputs before growth, check cancellation
  in power/reduction loops, enforce modular context precision and remove orphaned
  exponent-clamping code. Preserve significant zeros in inexact rounded quotients.

- Share exact Decimal quotient/remainder preparation, enforce quotient precision,
  and round remainders through the active context. Set zero-division flags and
  honor trap selection; reject unsupported quiet nonfinite results explicitly.

- Apply context precision to Decimal conversion, including exact binary-float
  rounding. Honor FloatOperation and ConversionSyntax, preserve signal aliases
  on refused working-storage admission, and reject unrepresentable results
  instead of rounding them again. Context conversion returns a fresh owned value.

- Populate Decimal's standard signal maps, preserve aliases during assignment,
  and validate their fixed Boolean surface. Create mutable templates on first use.

- Round finite Decimal addition, subtraction, multiplication and division once
  using context precision and all eight rounding modes. Apply context to unary
  signs/abs and Counter arithmetic, and replace fma's multiply-then-add detour.
  Set Inexact/Rounded flags and honor their traps with native error payloads.

- Reject unsupported nondefault Decimal exponent, capitals and clamp policies
  instead of storing settings that arithmetic ignores; remove their mutable state.

- Preserve Decimal context aliases, copy only local children, and restore exact
  parents. Update flags in place and fund mapping copies. Mutable Decimal
  templates are execution-local; constructor defaults follow DefaultContext.

- Normalize supplied Single values to guest float/Double and CLR decimal values
  to guest Decimal. Preserve integral public round trips and reuse integer
  recognition in Decimal and random operations. DecimalTuple now shares tuple
  equality, hashing, operators, methods and admitted sequence projection.

- Correct floating floor division, modulo and divmod through one shared managed
  primitive, including fractional divisors, signed zero, infinities and NaN.
- Divide large integer ratios without first overflowing the individual operands;
  round normal/subnormal results once and admit temporary division workspace.
- Resolve constant integer powers and saturated shifts before narrowing counts
  or admitting magnitudes. Preserve finite floating power overflow as OverflowError
  and remove duplicate operand conversions and misleading TypeError translation.

- Unify native/custom numeric hashes across exposed values, table buckets and
  caches. Use class-creation hash slots instead of inferring unhashability from
  later equality changes; preserve explicit and generated dataclass choices.
- Share reflected dispatch with `divmod`, bind implicit numeric methods from the
  type, and await actual descriptor/index callbacks during asynchronous operators
  and sequence repetition. Preserve in-place aliases and callback ordering.

- Admit expanded keyword-name decoding before copying or mapping lookup. Retain
  labels that native callees can keep; parser options borrow immutable text.
- Replace ungoverned Namespace/help formatting and late adoption with governed
  UTF-8 construction. Remove repeated positional suffix scans, conflicting token
  collectors, decimal-based token classification and all-flags cluster staging.
- Correct argparse positional/optional boundaries, end markers, negative spellings
  and mixed short-option effects. Namespace uses one live dictionary; returned
  action metadata updates parser state. SUPPRESS uses canonical string identity.
- Reuse ordinary truth, addition and SystemExit payload handling. FileType opening
  and output protocols support genuine asynchronous hosting; normal parse failures
  emit mediated diagnostics. Cancellation/refusal remain terminal with awaited cleanup.
- Remove native-only writer/status/Boolean policies and obsolete argparse helpers.
  Custom formatter factories and action layout changes now fail explicitly;
  supported action metadata and the finite help layout are documented.

- Consolidate byte search onto managed bulk operations with cancellation checks,
  exact search windows and type-based index conversion. Search/membership, byte
  counts/widths and line-splitting truth support genuine asynchronous callbacks.
- Remove blanket Int32 count/negative-width rejection, naive candidate scans,
  duplicate bound parsing and reverse-whitespace split staging. Actual C-int and
  managed storage bounds remain checked; integer-conversion overflow reports OverflowError.
- Transfer encoded byte storage into its result without charging the same backing
  twice. Retain allocation preflights, construction overlap and result ownership.
- Correct codec method argument types and case-sensitive handler names. Unknown
  handlers raise LookupError eagerly; None method names raise TypeError.
  Codec-generated Unicode errors expose their five arguments and native fields,
  retain source funding, and await custom rendering under RunAsync.
- Distinguish ordinary guest float-conversion MemoryError from terminal admission
  refusal in struct packing. Cancellation/refusal retain their original origin
  and awaited cleanup, including failures while constructing codec error details.

- Admit slice representations and exact indices arithmetic before substantial
  allocation. Remove the metadata length clamp and oversized-stride empty-slice
  shortcut; large strides now preserve reads, assignments and deletions.
- Recheck live list lengths after comparison callbacks, fixing wrong results
  and CLR exceptions in search, removal and structural comparisons. Deque removal
  selects its Python mutation error before attempting a removal.
- Reuse contextual slice equality, sort-flag truth and implicit native-bound
  index hooks, including actual asynchronous hosting. Remove duplicated insertion
  validators, context-free bound comparison and synchronous lookup detours.

- Make module discovery lazy and use live registry/spec/loader metadata. Cold
  dotted discovery initializes parents; walking follows initialized package paths.
  Discovery, source reads and resolution support actual async host operations.
- Remove eager path inventories, directory-only recursion, competing loader
  factories and the duplicate ModuleInfo record. ModuleInfo now has namedtuple
  semantics; spec properties share backing slots and parent is readonly.
- Admit discovery rendering, path preparation and copies before allocation.
  Breaking corrections: bare string discovery paths are rejected when consumed,
  package callbacks handle initializer errors, unused arguments stay deferred,
  and non-list extend_path inputs pass through unchanged.

- Remove unadmitted JSON separator copies, refund-only iterator cleanup and
  mirrored encoder-subclass state. Failed/completed iterators relinquish frames
  before refunding sort scratch; immutable separator aliases remain funded.
- JSON options and customization use Python truth, attribute, initialization,
  iteration and invocation protocols, including actual suspension under RunAsync.
  Base encode honors iterencode overrides. Unused callbacks are no longer rejected
  eagerly; obsolete frontend option restrictions are removed.
- Align nonfinite JSON keys, all accepted host integer widths, namedtuple arrays
  and default-object cycle detection across writers. Breaking correction: Decimal
  is no longer implicitly a JSON number; use an explicit default conversion.

- Consolidate function wrappers onto Python class, descriptor, attribute and async
  call paths. Single dispatch uses canonical identity, hierarchy and a live readonly
  registry. Typed caches and cached properties keep consistent public state.
- Remove registry snapshots, synthetic dispatch distances, eager metadata-name
  copies and private cached-property accounting. Preserve suspended callback
  cancellation, terminal refusal, ownership, cleanup and Python diagnostics.

- Admit map/chain retained storage and call scratch; remove duplicate map input
  acquisition and chain's eager cursor table. Lazy adapters await source
  acquisition and share ordinary truth, equality and addition protocols.
- Correct islice demand timing, dropwhile predicate lifetime, sentinel/adapter
  exhaustion and deferred callback validation. Native adapters retain needed
  sources/closures without pinning unrelated function locals. Strengthen semantic,
  cancellation, refusal and ownership regressions.

- Consolidate gzip.open onto GzipFile's incremental engine and execution cleanup;
  remove the eager/staged handle, full-history rewrites, append-prefix copying and
  validation-poison rollback. Text mode reuses ordinary file decoding/codecs.
- Admit gzip filename and compressor construction before owned write-open.
  Keep valid writes after caught errors and finalize unclosed streams at execution
  end. Read-open now defers validation and binary-read capability checks.
- Share gzip argument/protocol conversion and await callbacks under RunAsync.
  Omitted/None compression timestamps now use the host clock, matching Python
  3.13; explicit zero stays deterministic. Truncated members raise EOFError.
- Correct the module inventory: ElementTree serialization and element/tree
  construction/mutation remain unsupported; parsed attribute maps are mutable.

- Retain governed timezone-name aliases and admit native date/time text directly;
  remove copied CLR labels, manual repr escaping and post-render adoption.
- Replace four ISO regex recognizers and input copies with a cancellable UTF-8
  scanner. Support scalar separators, fractional hour/minute forms and canonical
  UTC results. Align ISO factory ownership with constructors.
- Translate calendar range failures into Python errors; correct midnight-aware
  time keys, datetime identity/fold rules, tuple-like ISO calendar results and
  native factory ownership. Enforce keyword-only fold and timezone arguments.
- Await date/time conversion callbacks under asynchronous hosting. Remove dead
  context-free calendar helpers and obsolete resource-policy commentary.

- Create/truncate admitted writers at open, preserve later appends on untouched
  close, and emit codec preambles only after writing. Defer file reads/decoding to
  consumption and use the index protocol for text sizes, including async callbacks.
- Publish local modules before execution, honor live registry replacements and
  remove failed entries. Add contained package metadata, preserve spec identity
  and prefer authorized packages over same-name files. Remove the separate
  loading-module inventory and consolidate the shared host fixture's byte store.

- Encode file output directly from funded UTF-8 storage and release encoded
  staging after host completion for every codec. Transfer binary read funding
  once, retaining construction overlap and terminal admission refusal.

- Use exception identity semantics, canonical args and live chaining/code slots;
  remove generated structural equality, eager string-message copies and exception
  dictionary rebuilding. Admit diagnostic conversions and release private repr
  scratch on every exit. Await exception protocols under RunAsync.
- Keep native type identity independent of sys.modules edits. Remove speculative
  host-capability preflight and obsolete typed-name handler resolution. Missing
  capabilities now fail at the reached runtime boundary; earlier effects can occur.

- Admit dataclass conversion storage before allocation, protect recursive walks
  with managed stack admission, and reuse deepcopy behavior for mutable leaves.
  Release failed repr scratch and report unsupported CLR projections as
  attributed failures instead of leaking native exceptions.

- Keep canonical dataclass Fields and live readonly dictionary metadata; remove
  duplicate field definitions, helper inventories and unproved result-shape facts.
  Await generated init, conversion and replacement callbacks under RunAsync.
  Honor descriptors, inherited defaults, reflected comparisons, frozen subclass
  attributes and decoration-time post-init wiring. Invalid factory/default
  combinations now fail at runtime instead of speculative frontend rejection.

- Build paths in governed UTF-8 storage, preserve pure/concrete POSIX kinds and
  double-slash anchors, and await filesystem protocols and Python option truthiness.
  Share lazy component-aware glob traversal; remove eager glob arrays, recursive
  native traversal, late path copies, redundant helpers and stale policy tests.

- Compare path prefixes at Unicode boundaries and preserve lexical parent segments
  in commonpath. Remove repeated os.walk child-name snapshots and spelling-based
  filesystem frontend checks; resolved builtin contracts remain.

- Add explicit host replacement, file-removal and directory-removal operations;
  missing capabilities fail without generic fallback. Remove destructive
  delete-before-move emulation and reject wrong-kind deletion. Apply managed
  stack safety through iterative native filesystem traversal after real suspension.

- Consolidate CSV output into governed UTF-8 rendering with active cancellation,
  admitted history and correct escape/terminator handling. Use Python cell string
  conversion and numeric classification; reject failed rows before publication.
  Admit decoded input lines before conversion. Retain canonical mutable CSV headers,
  infer them lazily, and acquire one iterator through the selected execution mode.
  Use mapping row protocols, Python option truthiness and Excel CRLF defaults;
  extra dictionary fields raise ValueError. Remove string-only header snapshots,
  duplicate cursors and spelling-based CSV validation. Honor QUOTE_NONE on input
  and reject conflicting delimiter, quote, escape and terminator settings.

- Correct subprocess binary/text inputs, captures, errors and buffered pipes;
  default output is bytes. Share numeric-second timeout conversion and
  TimeoutExpired translation. Await guest argv/truth callbacks under RunAsync,
  evaluate check after completion and reject conflicting text flags. Remove
  strict-bool gates, integer-millisecond parsing and unproved string return facts.

- Remove duplicate subprocess validation based on receiver spelling. Local objects
  named subprocess keep their own call contracts; real imports/calls retain host
  authorization and resolved callable checks.

- Admit environment mutations and replacement overlap before publication. Reject
  invalid names/NUL values and expose live keys/values/items views without eager
  decoding. Explicit snapshots keep their independent ownership charges.

- Admit native subprocess request storage before construction and retain it through
  actual host cleanup. Preserve original args aliases and remove completion-time
  re-encoding. Govern command-line quoting directly in UTF-8; document the deferred
  buffered Popen lifecycle.

- Use Python membership and actual async callbacks in argparse. Preserve default
  aliases, append copy-on-use, default conversion and live namespace progress;
  reject option conflicts before publication. Remove mutable parser-shape tracking;
  unproved namespace-value errors now occur at runtime. Admit parser construction
  and working storage through the existing shared owner.

- Drain shutil.copyfileobj through EOF, including binary and explicit chunked
  streams. Compare files through admitted bounded ranges, honoring guest truth
  hooks and actual asynchronous callbacks without duplicate stat/full-file reads.

- Make object namespaces coherent: live instance/script-module dictionaries,
  read-only class views, descriptor-aware vars(), and canonical function custom
  attributes. Native-module definitions remain read-only; whole instance-dictionary
  replacement fails explicitly. Retain deleted instance-table capacity until cleanup.

This release uses the contracts described in the
[documentation](README.md#documentation).

- Keep sys.modules as the actual run-local import registry; preserve aliases,
  deletion, None blockers and supplied cached objects without inventory/refresh
  copies. Remove tests requiring repeated reads to exhaust the allowance.
- Remove the unused ILythonHost.WalkAsync overloads and LythonWalkEntry public
  type. Runtime os.walk retains its iterative traversal through ListDir/Stat.
- Reuse live module/class namespaces for locals() and zero-argument vars(),
  preserving Python 3.13 function-local snapshots.
- Honor local module __all__, including private exports, ordered failures and
  asynchronous sequence indexing. Share export selection across import routes.
- Resolve copy.replace through the class protocol before native defaults; reject
  instance-only hooks and arbitrary _replace methods. Await supported copy and
  replacement hooks in RunAsync while retaining the shared memo through cleanup.
- Preflight deepcopy tuple backing and preserve stack-failure attribution through
  recursive copy unwind. Create the import registry only when first needed.
- Keep combinatoric input pools in governed storage without staging/final copies;
  skip input acquisition for product repeat=0 and shape repeated-table overflow.
- Admit combinatoric result tuples and zip-longest cursor tables before allocation;
  retain output funding through asynchronous input and poll owned index/copy loops.
- Admit tee construction/fanout before allocation or source effects; retain actual
  queue capacity and pending users, and report reentrant source pulls as RuntimeError.
- Share contextual iterator-size/truth conversion, await hooks in RunAsync and
  preserve constructor-specific effects. Accept large bounded batch requests,
  skip input acquisition for zero-output tee and remove duplicate validators.
- Replace total_ordering's Boolean-only dispatcher with Python root formulas,
  inherited/class-descriptor resolution and short-circuit raw results. Await
  supported callbacks; report missing roots as ValueError without requiring __eq__.
- Remove cmp_to_key's integer-sign restriction; compare raw results with zero,
  preserving guest results and awaited comparator/comparison/truth callbacks.
- Keep partial args/keywords and partial/cache/dispatch metadata live and funded;
  enforce readonly partial descriptors and preserve aliases on dictionary replacement.
  Admit partial call scratch through target completion and build repr in governed
  UTF-8 storage, removing copied getters, redundant arrays and string-join rendering.
- Return a lazy Counter.elements iterator with deferred count/index errors,
  bounded first pulls and cancellable asynchronous count hooks.
- Admit deque repetition before mutation, reuse nodes and bound work by retained
  output. Stream async in-place extension and reject non-Python deque slicing.
- Keep ChainMap.maps live and fund retained layers and merge storage. Consolidate
  key merging with Python equality and stored hashes; release private scratch
  after native consumers finish while retaining guest iterator snapshots.
- Copy Counter's initial counts without numeric-only validation; use contextual
  update/subtract arithmetic with Python operand order and asynchronous hooks.
- Share governed Counter ranking/repr sorting, honor guest comparisons and limit
  evaluation order, skip no-work limits and select a single maximum directly.
- Admit statistics scratch and singleton quantile capacity before allocation;
  keep paired and frequency buffers funded through their consumers. Build
  quantile/mode results directly and cover real conversion/replacement overlap.
- Remove shlex's retained UTF-16 input copies, per-character strings and raw
  quoting/token buffers. Govern output growth and poll quoting/comment scans;
  shlex.join supports asynchronous guest iteration.
- Make function caches tolerate reentrant clear/eviction, retain funding until
  active lookups finish and hash each invocation key once. Preserve recursively
  published results; disabled caches accept unhashable arguments.
- Stream unweighted fmean, preserve weighted evaluation/error order and detect
  intermediate floating-sum overflow before later terms can cancel it. Stabilize
  correlation's denominator and poll owned statistical reductions.
- Reuse expression protocols in operator truth, comparisons, containment and
  sequence search, including suspending hooks. Correct length-hint default/error
  ordering and native iterator hints; rich comparison contracts allow guest values.
- Retain shlex/regression option objects and honor repeated, suspending truth
  checks. Preserve numeric regression field types, govern their rendering and
  expose their public sequence projection. Remove the integral-variance copy.

- Admit random byte generation before drawing, correct width arithmetic and remove
  incidental bit-width cutoffs. Poll cancellation in random native loops and await
  getrandbits/randbytes conversion hooks without changing ordinary draw sequences.
- Dispose hash originals/copies after execution cleanup and projection; remove the
  no-op tracking hook. Honor usedforsecurity truthiness and difflib option/predicate
  results, including supported asynchronous callbacks. Clarify file_digest remains
  unsupported despite mediated binary stream support.
- Transfer URL output/encoding storage instead of charging backing twice; borrow
  immutable UTF-8 when quoting. Public byte copies use their projection owner,
  avoiding duplicate execution charges and an escaping refusal on sync returns.

- Preflight integer byte output and replace repeated BigInteger shifts with a
  direct managed write. Admit Decimal digit storage and text/tuple parser scratch;
  remove raw digit inventories, redundant boxing and parser string copies.
- Await to_bytes conversion, DecimalTuple iteration and JSON strict callbacks
  through RunAsync. Honor guest signed/strict truthiness, zero-byte signed -1 and
  empty DecimalTuple digits. JSON scanners capture strict at construction.
- Transfer codec and struct reservations to final storage; remove duplicate
  backing charges and refund/readmission handoffs.

- Admit sized/iterable byte construction, fromhex and right-split storage before
  allocation; remove raw conversion lists and the right-split match inventory.
  Transfer BytesIO copy reservations instead of charging the same backing twice.
- Preserve bytes identity, __bytes__ precedence and type-based index lookup.
  Asynchronous byte/integer conversions await guest conversion/iteration protocols.
- Format date/time text directly into governed UTF-8. Replace strptime's generated
  backtracking regex with an admitted, cancellable directive parser. Duplicate
  directives raise PatternError. Correct strftime's full six-digit microseconds
  and datetime's naive result for a timezone label without a numeric offset.
- Admit byte slices, representations, transformations and join working storage
  before allocation, using the active owner. Remove redundant input/output copies
  and unreachable unfunded construction branches. Byte tab expansion observes
  cancellation during bounded output growth; RunAsync joins await input iteration.
- Preserve full byte-join input effects/errors before item validation. Correct
  negative hexadecimal grouping and bytes.translate(None, ...) identity mapping.
- Remove the fixed JSON indentation ceiling. Encoding admits prepared indentation
  through the shared allowance; constructors avoid materializing unused padding.
- Admit byte concatenation and text transformations through the active execution
  owner before allocation, including compiled literals. Translation builds UTF-8
  directly; tab expansion observes cancellation during output growth.
- Correct Unicode translation-table indexing and remove full byte-table copies
  per character. Namedtuple defaults preserve tail effects/errors after excess
  length and validate names before iteration; infinite defaults need cancellation.
- Remove unused ownership-snapshot implementations, orphaned totals and no-op
  registration helpers while preserving actual construction rollback.
- Breaking: execution-fuel/host-call quotas and independent execution, projection,
  string, collection, host-read and captured-output caps are removed. Hosts supply
  cancellation and any finite shared memory allowance.
- Breaking: `MaxRecursionDepth` is now `int?`; remove `LythonCountLimit`.
  Null still uses the default, and managed stack admission remains enforced.
- Breaking: regex uses a Lython-owned provider contract supplied to the engine.
  Core has no Utf8Regex dependency; its optional adapter has explicit capability
  limitations, including unsupported in-flight backend cancellation.
- Compiled programs and results have explicit disposal/ownership lifetimes.
  Storage admission covers retained capacity and construction overlap; refusal
  fails the command after cleanup. Long streams use bounded host-driven batches.
- Both execution APIs and imports use executable code, with managed stack safety,
  real asynchronous host effects, generators and deterministic native cleanup.
- Remove residual factorial/comb/perm work ceilings, retaining cancellation and
  conservative preflight admission; correct combination working-storage estimates.
  Dictionary pair validation preserves full iterator effects and exact length
  errors without a pull cutoff or proportional materialization.
- Compute `math.nextafter` directly for arbitrary nonnegative step counts,
  preserving signed zero and special values. Remove unused runtime helpers,
  duplicate argument checkpoints and duplicate integer/tuple reservations.
- Transfer difflib snapshot admission without duplicate reservations or unrelated
  refunds. Fund sequence growth, starred-tuple scratch and final copies throughout
  construction, including suspended execution and failure cleanup.
- Keep materialized sort/deque/tuple inputs funded through use and unwind;
  remove unleased drain routes, obsolete sort roots and duplicate cancellation checks.
- Admit HtmlDiff UTF-8 rendering before growth, including escaping and final copies.
  Non-None `wrapcolumn` and `charjunk` now fail explicitly instead of being ignored;
  the supported HTML renderer remains line-level.
- Charge string padding to the active execution owner, including compiled literals,
  and build zero-fill directly in admitted UTF-8 storage with cancellation checks.
- Python compatibility includes ordinary class annotations, modern typing,
  expanded syntax and contained file/data modules. UTF-8 classification scans
  honor active cancellation without whole-subject identifier conversion.

- Add immutable per-invocation built-in module selection, enforced across imports, attributes, registries and discovery without granting missing host capabilities.
- Report typed terminal failure origins and guard quantities, preserving provenance through catches/rethrows and bounded diagnostic projection.
- Own exception storage once across constructors, bindings and aliases; retain funded args, attributes and chains, and preserve native cleanup on admission refusal.
- Report retained UTF-8 output capture sizes, with shared-memory and package-consumer controls.
- Return ordinary Python lists from regex findall, including normal type identity, mutation, JSON encoding and CLR projection; fund retained results through their explicit owner.
- Check cancellation during every lowered for-loop iteration, including pass-only range loops.
- Observe cancellation during shared built-in iteration, including sum, any/all and min/max over primitive iterators; retain list self-extension snapshots and dispose canceled cursors.
- Observe cancellation through stable-sort comparisons/copy tails and native sorting in statistics, JSON, difflib, directory traversal and protocol snapshots; preserve Python failures and refund sorting scratch.
- Check shared execution guards through whole-buffer Latin-1 decoding, non-strict UTF-8 decoding and newline rewriting, including mediated stdin and file windows.
- Fund regex cache slots only while its bounded capacity grows; eviction reuses existing funding and purge refunds it.
- Keep returned CSV/text aliases funded by surviving owners and release abandoned scratch deterministically.
- Add sequential gzip.GzipFile wrappers over contained filenames and supplied binary objects, lazy metadata/trailer checks, persistent governed compression, sync flush, awaited callbacks, and correct supplied-object ownership.
- Preserve compression resource cleanup, expose EOFError/FutureWarning hierarchy, and emit inferred gzip write-mode notices through mediated stderr.
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
- Add governed Path.read_bytes/write_bytes through optional host binary methods, with owned read copies, staged publication, byte counts and asynchronous cancellation.
- Apply Python's conditional Greek final-sigma lowercasing with Unicode 15.1 cased/ignorable context, bounded scans and governed scratch.
- Release lower/upper casing scratch when conversion is denied, and scan UTF-8 directly with execution guard checks.
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
- Temporary `*args`/`**kwargs` materializations follow explicit ownership lifetimes; retained values stay charged.

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
