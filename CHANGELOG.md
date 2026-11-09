# Changelog

## 0.11.0 - Unreleased

- Share direct constant/local operand sources with closed native helpers, removing
  consumed read nodes and their recursive dispatch. Prepare the fixed local prefix
  once while preserving guard-before-effects, cancellation, ownership and fallback.

- Keep text-reader storage funded through active copies, decoding, pending reads
  and awaited disposal. Preserve the original failure when cleanup also fails.

- Share immutable function-declaration facts across analysis, lowering and binding.
  Decode string literals from borrowed source spans and allocate concatenation
  storage only when needed, reducing compilation allocation.

- Store executable lookup facts in a flat admitted array. Reuse immutable CSV/JSON
  field layouts for repeated reads across rows, preserving current values,
  mutation, cancellation, admission callbacks and cleanup. Avoid field-cache
  allocation for one-shot helpers and unused member tables for mutable or
  contextual dynamic receivers.

- Reuse ordinary function lookup facts across calls under the existing execution
  owner. Keep frame size unchanged, read current field values after admission,
  and preserve reentrant allocation and genuinely asynchronous formatting hooks.
  Function-cache capacity remains charged until execution cleanup.

- Use retained JSON record layouts to admit their final value capacity on growth,
  avoiding repeated replacement arrays for wide records. Keep changing shapes,
  callbacks and result ownership; spare hinted capacity remains charged.

- Prepare native helper return trees across branches, retaining guard-before-work
  semantics and selected return spans. Share fresh empty-list admission and keep
  custom protocols, loops and shared joins on the existing executable path.

- Share JSON encoder initialization and consume bound native options directly,
  removing temporary constructor dictionaries and keyword forwarding collections.
  Native eager calls borrow their value with the existing managed call protection;
  preserve mutable attributes, guest classes and awaited callbacks.

- Share one iterative JSON traversal for eager and incremental encoding, removing
  the separate recursive renderer. Complete native steps directly and await actual
  callbacks; release private working funding after traversal and cleanup.

- Let default whitespace `rsplit()` share the existing casing/word preparation,
  eliminating its intermediate cased line in ordinary chains and closed helpers.
  Preserve supplied options, visible locals, custom methods and awaited cleanup.

- Share final storage construction for explicit-separator and whitespace splitting.
  Remove reverse-split temporary lists, conversion arrays and final copies; admit
  the complete result before creating fragments. Preserve direction, aliases and
  cooperative cancellation through the existing bounded byte search. Carry the
  active context after an asynchronously suspended split-count protocol.

- Read prepared operands directly, removing the shared operand bundle and runtime
  arity reader. Preserve descriptor snapshots, original read errors, suspension
  and ownership; borrow synchronous instruction metadata.

- Combine direct comparison conditions into one executable operation and share
  exact numeric ordering before protocol preparation. Remove ordered-comparison
  predicate/name transport, the forwarding wrapper and obsolete numeric fallback;
  preserve raw Python results, custom protocols and suspended truth conversion.

- Share existing native casing-and-splitting preparation across closed helpers and
  ordinary executable chains, including branches and protected bodies. Remove the
  native-only fused operation variants while preserving dynamic method behavior.

- Prepare closed string-method calls from shared declarations and compiler return
  facts, removing the duplicate native method inventory and dead fused casing node.
  Preserve Python dispatch, cancellation, admission and original source checks.

- Consolidate string operations and dictionary views onto shared receiver methods.
  Remove their separate delegate targets and special member-load opcode, preserving
  native fusion, Python binding and deterministic view-admission rollback.

- Forward adjacent plain local/constant operands directly into member lookups,
  iteration and truth branches, preserving receiver snapshots and suspended protocols.

- Execute existing native receiver operations and exact list/tuple iteration
  before allocating a function context. Keep context promotion before guest
  protocols or suspension, and transfer admitted working storage with it.

- Reject keyword arguments for string padding, search and lstrip/rstrip methods,
  following Python's positional-only signatures. These invalid Python calls were
  previously accepted. Argument evaluation and admission refusal still precede
  binding errors.

- Share string segmentation, search and padding method declarations; immediate
  calls borrow their operands instead of creating receiver closures and bound
  wrappers. Await custom index, width and line-ending truth callbacks in RunAsync.

- Store short copied UTF-8 text inline and write casing, split and CSV payloads
  directly into reserved final storage. Preserve adopted capacity and stable
  retained-memory borrows under the existing ownership allowance.

- Observe cancellation before allocating Unicode indexing caches, during their
  construction and before publication; return unpublished working allowance.

- Resolve executable jump chains once and normalize dense block IDs directly,
  removing repeated hash-set walks, reachability sorting and dictionary remapping.
  Preserve cycle entry identity and protected cleanup targets.

- Hold `math.prod` working admission through computation and transfer retained
  result funding from it, removing premature refunds and duplicate admission.
  Observe cancellation after admission; preserve empty integer/bool starts.

- Consolidate delimiter nesting checks into tokenization, removing the duplicate
  whole-source scanner. Valid same-quote nested f-string literals no longer trigger
  false nesting errors; parser and managed stack protections remain.

- Keep pending allocations funded across reentrant host memory-budget callbacks.
  Roll back unallocated storage on failure, retain callback results and first
  refusal, and explicitly reject same-execution allocation before a host resize
  is published. Independent executions continue sharing the same allowance.

- Emit `fnmatch.translate` and `glob.translate` patterns directly from UTF-8,
  sharing scalar-aware class translation. Remove decoded inputs, copied path
  segments and intermediate regex builders; retain cancellation and admitted
  output ownership without requiring a regex provider for translation.

- Use existing executable slots for module declaration bindings, preserving live
  globals, imports and declarations beyond frame cleanup. Share import binding
  facts across compilation phases without predeclaring unemitted module names.

- Parse general integer text directly from UTF-8 with native accumulation and
  exact large-magnitude promotion. Support Unicode decimal string digits, retain
  ASCII bytes grammar, and observe cancellation during long conversion. Admit
  magnitude construction before allocation and preserve first refusal and cleanup.
  A managed 128-bit accumulator avoids intermediate magnitudes for wider inputs;
  retained values above 64 bits remain admitted before conversion.

- Forward adjacent plain local/constant operands before assigning executable
  storage. Preserve original error spans and protocol snapshots while removing
  initialized-local tracking and duplicate call-entry checks.

- Share builtin method implementations independently of their receivers. Common
  immediate calls borrow evaluated operands; exposed methods keep Python binding
  and metadata. Remove duplicated method markers, factories and dispatch helpers.

- Avoid repeated Unicode prefix scans for nearby overlapping regex captures by
  moving the existing scalar cursor backwards when nearer. Preserve original
  capture coordinates, current cancellation and subject/result ownership.

- Omit unused annotation binding analysis after the final relevant check in
  non-regex module/function/class scopes. Preserve conservative branch facts,
  nested checks and constant regex analysis in unreachable/deferred scopes.

- Identify builtin object/type by their runtime relationship, so user classes with
  those names retain normal attribute and instance behavior. Preserve immutable
  builtin aliases, descriptors and per-execution type isolation.

- Execute formatted output through direct instructions, removing its generic
  prepared-operation detour and wrapper records. Native dynamic specifiers avoid
  unnecessary function contexts; real protocols still await callbacks and cleanup.
  Keep transient formatting values outside the shared dispatch loop.

- Carry current cancellation through native string/bytes hashing, explicit hash
  methods and dictionary shortcuts. Reuse bounded byte-hash chunks, publish caches
  only after completion and preserve caller independence and first memory refusal.

- Prepare immutable function parameter layouts once and share them across created
  executable, lowered, lambda and generator functions. Remove repeated parameter
  classification, layout arrays and name-map construction; keep defaults, closures
  and keyword-name storage private, with native generated admission/rollback.
  Reuse the same parameter order for executable slots, removing the compiler's
  separate classification pass.

- Prepare class own-cell requirements with lexical facts, removing repeated syntax
  analysis during class creation while preserving fresh class/closure cells.

- Complete fully supplied positional, unbound closed native helpers directly in call
  dispatch, removing their synthetic pending-call/completion transition. Preserve
  argument borrows, stack admission, exception provenance and dynamic or genuinely
  suspending calls through the existing frame runner.

- Execute multipart formatted helpers through the ordinary operand slots and
  formatter, removing their separate native-body proof/evaluator. Primitive
  fields can run without constructing a function context; dynamic fields resume
  at their actual instruction. Prevent stale native type proofs when admission
  changes a later global receiver; preserve asynchronous callbacks and cleanup.

- Transfer completed CSV row arrays directly into dictionaries when native header
  identities match shared record keys. Remove the second values allocation and
  per-field publication on that path. Revalidate headers after admission, preserve
  dynamic headers and retain original conservative row-capacity funding.

- Reuse immutable provider programs from successful constant regex analysis
  during execution, removing duplicate compilation of the same UTF-8 request.
  Keep per-run pattern identity, purge behavior, group-name storage and current
  cancellation tokens. Programs remain funded until compiled-script disposal
  after all runs; retained compilation reservations can grow and reject earlier.

- Prepare fixed builtin function definitions and constructor signatures once,
  removing their repeated construction and signature-interning lookups from
  invocation setup. Preserve private mutable type/exception graphs, builtin
  namespaces and caller-specific module identities.

- Route pending frame exceptions at resumption after operand cleanup, preserving
  generator delegation and original instruction spans. Remove the duplicate
  return-completion flag and repeated pending-error polling from ordinary
  execution. Instruction cancellation and memory-admission checks remain.

- Share immutable native key metadata across matching JSON and CSV records while
  keeping values independent. Remove the separate sorted JSON key hint and
  coupled key/value entries; key-only sets omit values storage. Preserve mutable
  headers, key order/protocols, retained aliases and cancellation. Adding a new
  shared-record key admits private replacement before mutation; shared metadata
  stays charged to the existing batch/result owner. Conservative admission
  thresholds can change.

- Replace provider match carriers with caller-owned capture spans through
  `ILythonRegexSubject.TryMatch` and `ILythonRegexCursor.TryNext`; remove
  `LythonRegexMatch` and the old `Match`/`Next` signatures. This intentionally
  breaks provider implementations. Retained Python matches keep immutable ranges
  over the original subject and admit partial group strings on access, moving
  possible memory refusal to that access. Expansion reads ranges directly.
  Add read-only Python `match.regs` scalar bounds.

- Share class construction across execution APIs and pass ordinary positional
  arguments directly from operands. Remove default-allocator class copies and
  compiled initializer receiver repackaging. Select a returned subtype's actual
  initializer and support staticmethod/classmethod/descriptor initializers, with
  awaited host I/O, retained arguments and terminal cancellation/admission refusal.

- Extract class-pattern attributes before subpatterns, resolve `__match_args__`
  descriptors, reject duplicate attributes and treat extraction `AttributeError`
  as mismatch. Reuse namedtuple field metadata and contextual value equality.
  Remove the full per-match metadata copy and separate URL matching route;
  admit selected-name storage and preserve cancellation/refusal through errors.

- Await custom hash descriptors/bodies in public hashing and native dictionary/
  set read paths under RunAsync. Generated dataclass hashes await all field reads
  before hashing; pending snapshots stay funded. Share prepared lookups and remove
  protocol-query synchronous detours. Mutation and cache-key restrictions remain.
  Explicit tuple hashing now follows key protocols, and empty-set queries preserve
  earlier hash effects before a later unhashable tuple element.
  Share the existing native string-key proof across get/subscript/lookup, checking
  it after arguments and retaining awaited equality when the table changes.

- Resolve builtin class patterns through their actual type values, including
  aliases. Match the whole subject for Python's builtin positional patterns;
  reject extra positions. Remove synthetic sequence storage and duplicate
  class-name dispatch. Use `list([left, right])` to decompose a list.

- Share ordinary/cased whitespace word discovery and bounded ASCII category
  runs, preserving Unicode casing, cancellation and admitted output ownership.

- Transfer executable tuple literals into one admitted final backing, removing
  an intermediate copy and its construction overlap. Keep aliases and fund final
  storage before retiring inputs; borrowed-array constructors retain their copy.

- Use one active structural identity path for hashing and comparison, removing
  separate cycle hash tables and their identity comparer. Preserve immediate
  cycle errors, managed stack limits, asynchronous isolation and cleanup.

- Hash slice bounds through Python protocols, including bound/unbound hash
  methods, with managed recursion protection. Generated dataclass hashes keep
  their defining field plan and read all included fields before hashing them.
  Admit temporary field storage first and stop after cancelled/refused callbacks.
  Explicit `object.__hash__` retains identity behavior for unhashable dataclasses.

- Share custom set add, membership and removal resolution. Hash empty queries,
  match native members and preserve stored hashes and equality-driven mutation.
  Admit protocol-entry metadata before publication, keeping refusal terminal and
  retained capacity funded through rollback and cleanup.

- Select custom-key equality candidates by their actual Python hash. Remove
  candidate sets, sorting and intermediate lists while preserving snapshot
  ownership, insertion order and the stored-entry structural index.

- Invoke custom key hashes for dictionary/set queries and removal, including
  identity hits, and propagate their errors. Native dictionary copies reuse stored
  keys without invoking hashes again. Report the failing nested unhashable value;
  prepare contextual hashes and equality requirements in one walk. Earlier tuple
  hash callbacks and their errors precede later unhashable elements, matching Python.

- Complete ordinary expression and iteration results directly into adjacent local
  bindings, avoiding intermediate result transport when cleanup has finished.
  Preserve separate operation/store spans, delayed protocols, cancellation,
  terminal refusal and funded operands through actual cleanup. Remove the
  dedicated augmented-update opcode and duplicate augmented-operator mappings.

- Restore saved exception state and remove handler bindings at actual executable
  frame exit, including cancellation or refused admission at instruction entry.
  Share existing cleanup across both APIs; yielded/pending frames retain state.

- Bound private operand cleanup using facts from the existing compiler shape pass.
  Skip classification of higher guest operands while preserving full reference
  clearing, reverse disposal and awaited cursor cleanup.

- Check cooperative cancellation in bounded UTF-8 chunks while finding whitespace
  words, removing shared traversal updates for every scalar. Preserve Unicode,
  split limits, conservative admission and independently owned results.

- Replace the three thread-local tiny function-argument pools with one funded,
  cleared workspace per execution. Repeated equal-sized calls reuse its capacity;
  nested and suspended calls retain their own funding. Small calls now use shared
  admission, and idle retention can reject tight budgets earlier. Cleanup returns
  the cache without collecting or retrying refused storage.

- Compute fused lower/upper whitespace-split capacity during word discovery,
  removing repeated sizing scans. Preserve ordinary split/rsplit work, Unicode
  casing, conservative admission, cancellation and independently owned results.

- Identify regex-provider memory refusals during compilation by operation,
  provider explanation and available source location. Preserve `LA0008`, terminal
  shared-memory refusal attribution and cleanup through compile-to-result APIs.

- Build CSV records directly into final field arrays. DictReader releases private
  capacity after callbacks and awaited cleanup; csv.reader and fieldnames adopt
  the same funded backing. Remove intermediate row lists and per-field insertion,
  preserving queued records, retained values, cancellation and terminal refusal.

- Respect known list capacities, start empty without element backing and use one
  geometric growth rule. Clear without allocating replacement storage, retaining
  metadata and alias funding. Remove the capacity floor and first-growth exception;
  preserve sort mutation detection and admission before replacement peaks.

- Consume prepared native formatted returns in source order. Remove their runtime
  builder-shape proof and recursive append-chain execution, retaining actual field
  checks, original reads, shared formatting, cancellation and funded cleanup.

- Plan initial multi-part f-string storage from existing literal and format
  metadata on both execution paths, avoiding intermediate buffer growth and
  copying. Preserve current cancellation and allocation rollback; tight shared
  allowances may reject construction earlier without retrying smaller storage.

- Prepare closed native-helper operations during compilation and remove their
  provenance from ordinary instructions. Share string-method metadata and retain
  dynamic checks, original spans, cancellation and funded cleanup.

- Write eligible native lower/upper plus whitespace-split results directly into
  final word storage, avoiding an intermediate cased line. Share existing Unicode
  casing and split construction; keep independent owners and admission/cancellation.

- Parse arithmetic and bitwise precedence without descending every grammar layer
  for each operand. Remove redundant parser routes while preserving evaluation
  order, diagnostics, cancellation and existing nesting bounds.

- Admit default JSON object shells and initial native storage together. Share
  separator handling across completed and suspended parsing, removing the initial
  position sentinel and redundant whitespace scan. Tight budgets can refuse
  earlier, before the first property callback; refusal and cleanup remain terminal.

- Retain executable function headers without unused body trees or duplicate
  default-expression maps. Share completed/pending default evaluation and empty
  construction metadata while preserving mutable defaults and asynchronous order.

- Reject private rollback of live dictionary views before returning allowance.
  Keep capacity and rollback on native owners; remove unused view memory hooks.

- Consolidate CSV parsing into explicit record/field states and bounded UTF-8
  boundary searches. Preserve batched storage and cleanup; correct ignored-space
  records and repeated space delimiters with `skipinitialspace`.

- Use managed bulk byte searches for shared line boundaries, preserving newline
  modes, chunk-edge handling and scalar read limits with less scanning work.

- Share normalized UTF-8 windows across string search and prefix/suffix operations.
  Reject empty searches starting beyond the string and invalid start bounds before
  invoking end-bound hooks, matching Python. Preserve scalar indexes and cancellation.

- Use constant-time UTF-8 string emptiness in truth and text-file EOF checks.
  Avoid unused character counts in unhinted `readlines()`; preserve scalar lengths.

- Share module scope facts through lexical preparation and executable construction.
  Fold implicit-super discovery into the existing walk and remove lowered-to-syntax
  reconstruction and nullable builder-fact fallback. Preserve class cells, delayed
  global shadowing and generator/comprehension scope boundaries.

- Simplify frontend resolution caching to syntax-node identity with clearing on
  binding changes. Remove recursive structural key hashing, version counters and
  per-entry versions while preserving diagnostic flow, cancellation and ownership.

- Keep copy hashing, equality and memo lookups on the active caller's allowance
  after asynchronous hooks. Reuse native shallow copies to preserve stored
  hashes and aliases; prevent unrelated thread provenance from selecting an
  expired allowance.

- Share executable locals and temporaries in invocation-owned storage, use
  compiler-prepared input positions and retain native-return provenance in the
  instructions. Remove duplicate graph/array transport; preserve failable reads,
  cooperative cancellation and funded cleanup through suspension.

- Derive string cache funding from its retained scalar-offset table, removing a
  redundant charge counter. Preserve allocation preflight and construction rollback.

- Store list backing directly in each list, removing separate small/large storage
  implementations and their promotion. Keep replacement preflights, rollback,
  live iteration and funded sort cleanup; growth uses conservative geometric capacity.

- Extend native helper returns to static builtin formatting, removing full frame
  construction for proven bodies. Reuse shared formatting and deterministic working
  storage cleanup; keep custom protocols and real suspension on the ordinary path.

- Share recursive JSON container grammar across arrays, dictionaries and pairs.
  Keep completed parsing outside async state machines and resume the same loop
  after actual suspension. Preserve hooks, duplicates, cancellation and ownership.

- Share ordered lexical binding facts across frontend and executable construction,
  removing duplicate scope/local/pattern scans. Preserve module namespaces and
  parameter order, and keep partial mirrors from reordering function local snapshots.

- Remove temporary reservation objects from fixed UTF-8 copying, casing and
  padding, and duplicate admission of fresh string slices. Keep conservative
  capacity preflights, cancellation and rollback before publication.

- Let closed native helpers read fully supplied positional arguments directly from
  admitted caller operands, removing temporary binding storage and its copy/cleanup.
  Keep owned arguments for defaults, dynamic calls and genuine suspension, and
  preserve funding for returned aliases.

- Share numeric grouping/padding across general/default/percentage floats,
  Decimal and native span fields, fixing sign-aware zero fill at all widths.
  Format complex components into a governed UTF-8 body and remove separate
  grouped/padded CLR-string assembly. Keep temporary/final overlap funded.

- Format integer and finite fixed/scientific fields into admitted UTF-8 storage,
  sharing digit construction with percent integers and `bin`/`oct`/`hex`.
  Admit large scratch before conversion and remove per-digit large-integer
  division. Include sign-aware padding zeros in grouping for these native fields, as in Python.

- Transfer terminal UTF-8 builder storage into immutable strings without a final
  trimming copy. Keep unused backing capacity charged, admit metadata before
  publication, and preserve independent snapshots from reusable builders.

- Keep private builtin list/tuple loop state in admitted operand storage, removing
  repeated general cursor and enumerator construction. Preserve live list mutation,
  generators, cancellation and asynchronous protocol/resource cleanup. Position
  storage stays funded through suspension and releases with its frame.

- Replace the execution-wide JSON property-name index with an immutable previous-
  object key snapshot. Reuse names independently of field order without indexing
  every unique name. Preserve owned bytes, dictionary order/duplicate replacement,
  hooks and cancellation; equal decoded strings have no interning guarantee.
  Replaced private snapshot metadata remains funded until execution cleanup.

- Handle already-completed failures and cancellation in the shared instruction
  loop, avoiding redundant async fault relays while retaining genuine suspension
  and awaited cleanup.

- Read evaluated call targets once and share ordinary call selection and result
  transfer across both APIs. Keep native values outside async call wrappers and
  preserve dynamic binding, suspended users and argument-storage cleanup.

- Share the synchronous instruction loop with asynchronous execution, resuming
  after incomplete operations and awaited cleanup. Preserve original error spans,
  cancellation, memory ownership and dynamic member behavior.

- Prepare constant f-string format layouts once in compiled code, sharing the
  existing parser and formatter. Preserve custom raw specifications, value checks,
  dynamic formats, asynchronous protocols and ownership cleanup.

- Stage native JSON/CSV UTF-8 output without intermediate Python strings, sharing
  the existing text-file codec. Preserve public chunks, codec-error inputs,
  resumable encoder aliases and asynchronous publication/cleanup.

- Do not reject valid code when a class method rebinds a known outer name through
  `global` or `nonlocal`; class-local analysis no longer hides that invalidation.

- Share guarded native file writes with JSON and CSV producers, eliminating repeated
  method/argument binding. Preserve per-chunk guest JSON lookup, captured guest CSV
  callables, cancellation, memory admission and asynchronous publication cleanup.

- Reuse JSON iterator working storage across chunks while keeping yielded strings
  independent. Retained largest-chunk capacity can require more admission headroom.
  Reject reentrant encoder advancement with Python's ValueError, fixing silent
  item loss through default callbacks.

- Convert ASCII lower/upper spans directly into admitted UTF-8 output windows
  with managed bulk operations, preserving Unicode context and cancellation.

- Batch CSV text-field admission and row construction within the existing parser.
  Preserve immediate numeric conversion, cancellation and retained-row ownership.
  Record working storage can exceed the largest-field buffer used previously;
  this can require more admission headroom for a wide record.

- Return a fresh read-only proxy for each class `__dict__`/`vars` access, as in
  Python, while sharing live namespace storage and mutation tracking. Admit each
  proxy through its execution owner; instance dictionary identity is unchanged.

- Match filename wildcards directly over UTF-8, removing whole-string rune arrays,
  retained character caches and per-character CLR conversions. Preserve Unicode
  classes, cancellation and result ownership.

- Fix `fnmatch.translate` astral ranges and share scalar-aware character classes
  with `glob.translate`. Generated regex text now uses scoped DOTALL and strict
  end anchors. Correct recursive, hidden and Unicode-separator glob translation;
  admit translation working storage before decoding.

- Prepare operand capacity and handler-entry depths during compilation; execute
  one instruction stream without runtime operand resizing or per-frame depth
  tables. Successful loop/conditional arms avoid redundant entry jumps. Whole
  workspaces are admitted before construction, including unstarted generators;
  this can refuse tighter allowances earlier. Cleanup and retained aliases stay
  funded through actual completion.

- Share the existing owned iterator cursor across executable loops and checked
  consumers. Preserve async protocol precedence and mixed-iterator cleanup
  lifetimes; disposal cannot restart a retired private cursor.

- Replace existing dictionary values through one storage lookup, consolidating
  native writes while preserving equal-key identity, order and admission checks.

- Share unary builtin selection across executable calls and native returns,
  removing duplicate guards without new argument storage or cache state.

- Route prepared operations directly to their existing handler, removing chained
  asynchronous forwarding while preserving actual suspension and cleanup.

- Construct native CSV dictionary records at their admitted capacity, including
  missing/extra fields, without generic header cursors or repeated per-key
  wrappers. Preserve mutable headers, duplicate keys, protocols and retained rows.

- Keep Unicode 16/17 case pairs unchanged as in Python 3.13, including older
  characters whose partner was newly assigned. Reuse existing compatibility
  data so platform Unicode updates do not change these casing results.

- Avoid resumable interpreter construction for eligible native helper return
  expressions, including compound string operations, native conversions and
  dictionary string-key reads. Prepare expression links once without per-call
  extra operand or argument packet arrays. Closed module helpers also omit
  unobservable function contexts; adjacent named intermediates forward within
  the existing native proof while original instructions preserve dynamic bindings.
  Keep required lexical contexts, current name lookup, exceptions, admission and aliases;
  protocol-capable values retain the full executable path.

- Inspect shared compiler facts once per container/callback query instead of
  recursively expanding tuple and dictionary aliases. Remove exponential work
  and temporary allocations, preserve mutable-alias diagnostics, and observe
  cancellation and
  prior admission refusal while inspecting facts. No public API changes.

- Share one governed UTF-8 pipeline for `lower`, `upper`, `capitalize`, `title`
  and `swapcase`, inspecting original-subject final-sigma context only when needed.
  Preserve cancellation while funding final output and refund only unpublished
  storage; input aliases and terminal admission refusal keep their contracts.

- Defer the optional regex adapter's error-only pattern text lookup until an
  actual pattern failure. Preserve scalar error positions and current-token
  cancellation without decoding native UTF-8 patterns on successful operations.
  The ordinary adapter package pin and the core dependency boundary are unchanged.

- Admit large native-call argument packets independently of operand capacity,
  retaining their working allowance through actual synchronous/asynchronous
  completion. Observe cancellation during packet construction and refuse before
  invoking a consumer when its packet cannot be funded. No public API changes.

- Preserve Python regex error metadata and original patterns. Implement the
  `re.PatternError(msg, pattern=None, pos=None)` constructor, mutable fields and
  Python position/line formatting, including asynchronous custom protocols.
  Normalize optional-adapter UTF-16 error positions to scalar positions. Keep
  cancellation and terminal admission refusal distinct from catchable errors;
  error storage remains funded until owner cleanup. Provider pattern messages
  must exclude location suffixes; this clarifies the existing scalar-position API.

- Remove the legacy regex-only binding pass. Resolve regex flags through ordinary
  typed flow, accepting unrelated receivers named `re` and preserving known flag
  diagnostics through aliases. Raw pattern syntax belongs to the supplied
  provider; remove LA3044's textual named-escape ban. Provider pattern failures
  retain the existing catchable `re.PatternError` behavior.

- Compile starred variadic annotations with the existing one-value unpack
  operation. Accept ordinary/custom iterables, preserve Python arity errors and
  async callbacks, and share those semantics with retained lowered consumers.
  Remove the generic executable-expression fallback opcode, storage and dispatch;
  unknown compiler nodes fail explicitly. No public API changes.

- Observe compilation cancellation during ordinary parsing, recovery and literal
  scans, forward it through formatted-string helpers, and check between later
  compiler phases. Preserve terminal memory refusal and compilation-owner cleanup.

- Consolidate execution services, cancellation and nesting checks into the
  existing shared execution state, removing three internal forwarding holders.
  Preserve lexical frames, terminal memory refusal, managed stack admission and
  awaited host cleanup. No public API changes.

- Resolve native dictionary hits after earlier same-hash custom keys across
  reads, membership, writes, setdefault and removal. Share funded candidate
  resolution, preserve reentrant mutation/errors and keep popitem cleanup free
  of equality callbacks. Native integer/tuple lookup equality can now suspend
  under RunAsync, removing the former string-only routing restriction.

- Preserve existing custom keys when equal native keys update dictionaries,
  including setdefault, unions, Counter and defaultdict. Keep Python callback
  effects, cancellation and terminal memory refusal. Share defaultdict's general
  pair parser and copy native mappings without rehashing custom keys or creating
  an intermediate entry array; retain capacity admission and copy rollback.

- Observe cooperative cancellation during nonallocating whitespace split/rsplit
  scans, including long words and whitespace runs. Forward the active split
  context after suspended count protocols; preserve Unicode and maxsplit behavior.

- Fund dictionary and instance scalar/reference headroom at retained table capacity,
  removing separate dictionary entry accounting and per-entry admission/refunds.
  Deletion and clear reuse funded slots; growth still admits replacement overlap.
  Conservative unused capacity can reject earlier; refused admission remains terminal.

- Use Python eight-column tab stops and reject ambiguous tab/space indentation
  and unmatched dedents. Accept valid mixed-prefix nesting and ignore indentation
  on a final comment-only line.

- Tokenize Python source directly, removing masked-source copies, generic lexer
  rules and parser continuation-trivia bookkeeping. Remove the core's last runtime
  NuGet dependency (Lokad.Parsing). Preserve positions, diagnostics and ownership;
  observe compilation cancellation in lexical scans. Correct escaped physical
  newlines/triple quotes and reject whitespace after continuation backslashes.

- Preserve dictionary insertion order after deletion/reinsertion at every size.
  Replace small/map promotion with one managed ordered store, retaining capacity
  funding and replacement preflights. Larger finite batches may need more allowance;
  middle deletion shifts later entries. Make named-tuple/struct_time types hashable
  by identity and report invalid Counter keys as Python TypeError before probing.

- Decode UTF-8 error/newline paths directly into admitted native output, removing
  per-character CLR strings, UTF-16 builder scratch and re-encoding. Preserve
  malformed-unit grouping, error-before-newline behavior and ownership/cancellation.

- Decode ordinary UTF-8 and Latin-1 file windows into governed native text,
  removing UTF-16 roundtrips. Preserve independent output ownership, codec/newline
  behavior, cancellation and allocation preflights.

- Read the optional adapter's validated global flag prefix from original UTF-8,
  removing backend pattern-text access while preserving guest flag-object behavior.

- Avoid temporary limb reservations for callback-free JSON integers that fit the
  bounded signed-64-bit parse. Preserve large-integer construction preflights,
  retained payload ownership, parse hooks and cancellation.

- Keep eligible `sum` integer and compensated-float prefixes unboxed until result
  or general-operation transitions. Preserve exact promotion, Python numeric
  phase behavior, guest callbacks, cancellation and deterministic cleanup.

- Compact executable instructions to opcode/span and two operand slots, removing
  duplicate operand-domain tags and unused operator fields while retaining
  operand validity and control-flow guards.

- Observe cancellation before lower/upper casing returns its result, including
  cancellation raised during the final expansion growth. Return private working
  storage and preserve the original input on cancellation or admission refusal.

- Walk ordinary frontend expression fields, statement expressions and child bodies
  with value enumerators, removing per-node iterator objects across shared analysis
  passes. Preserve recursive header/target walkers, traversal order and scope rules;
  introduce no cross-script syntax cache or alternate compiler.

- Move regex splitting, template substitution and expansion onto one Lython-owned
  UTF-8 literal/capture pipeline. Providers supply matching/range cursors; remove
  their split/substitution API and adapter replacement-cache machinery. Temporary
  plans are refunded per operation and compiled-pattern caching is preserved.
  This intentionally changes the unpublished provider API; repeated substitutions
  parse their template once each instead of using a backend template cache.

- Parse CSV records directly from UTF-8 lines into governed byte storage, removing
  whole-line decoding, UTF-16 field builders and field re-encoding. Preserve scalar
  dialect characters, multiline state, cancellation, refusal and retained-row funding.

- Regex provider preparation now receives scalar and byte window bounds, and
  captures contain absolute byte ranges instead of copied text. Lython projects
  scalar positions from the original UTF-8 subject, with a sparse iteration
  checkpoint instead of the adapter's dense coordinate map, and rejects malformed
  capture boundaries. This
  intentionally changes the provider contract; the optional adapter retains its
  temporary backend conversion bridge.

- Regex provider group metadata now contains borrowed UTF-8 names in capture order,
  replacing the CLR string dictionary. Frontend analysis and cached runtime patterns
  copy names into admitted native storage; providers retain ownership of their
  metadata. Malformed metadata fails explicitly. This intentionally changes the
  unpublished provider API; the optional adapter encodes backend names once.

- Admit executable frame locals, cells, caches and control arrays before allocation,
  using the existing operand-storage lifetime. Retain private funding through
  suspension and growth, and refund on cleanup; reused bound locals are not
  charged twice. Conservative capacity estimates can reject tighter allowances
  earlier. Generator metadata keeps its existing retained owner.

- Prepare ordinary class-method bodies once with the executable compiler and
  discard their unused lowered bodies. Preserve class-cell, nonlocal, decorator,
  annotation, generator and asynchronous callback behavior. Capture docstrings
  once; retain namespace lookup where enclosing compiler cell slots are unknown.
  Execute eligible type-subscription methods and descriptor getters through the
  existing explicit call frames. Consolidate duplicate call/body stack admission,
  preserving recursion bounds, cancellation, traceback and awaited cleanup.

- Write eager and incremental JSON output into governed UTF-8 storage. Remove the
  whole-output UTF-16 builder, separate growth accounting and string/key/indent
  decoding; preserve escaping, callbacks, chunks, cancellation and result funding.

- Preserve the original destination object returned by shutil.copyfile/copy/move
  for non-directory targets, including Path and custom pathlike objects; remove
  incorrect string-only frontend return assumptions. Joined child-path results
  retain independent admission before host effects.

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
