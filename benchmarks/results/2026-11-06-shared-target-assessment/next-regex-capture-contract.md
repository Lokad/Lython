# Independent next regex milestone: caller-owned capture output

Source gate, not an implementation or measured speedup. Current provider methods
return LythonRegexMatch, a record carrying IReadOnlyList<LythonRegexCapture?>. Both
the normal adapter and selected native-range variant allocate a record plus a
nullable capture array for every successful advance. Core validates the list and
copies retained match data; findall/sub/split need only borrowed current captures.
The dev44 direct64-match diagnostic measures22,368B/cursor; source establishes
these per-match carriers, not their exact removable fraction or whole-script cost.

Candidate contract: TryMatch/TryNext fill a caller-owned
Span<LythonRegexCapture?> and return success plus out int? lastIndex. The pattern
already supplies CaptureSlotCount. Preserve UTF8 byte coordinates, original-window
semantics, named groups/effective flags, Python errors and current cancellation.
Providers write every slot (including null) on success; core keeps validation of
slot zero, lastIndex, scalar boundaries and ranges. Failure exposes no result.
Remove LythonRegexMatch and old compatibility methods rather than keeping two APIs.

Core scratch uses existing subject/iterator working ownership and conservative
capacity admission. Retained Python matches still copy to separately funded final
storage; scratch must never become an escaping alias. Existing scopes/reservations,
no new memory abstraction/ledger/collector. Release only after all actual users.
Uninterrupted matching is bounded by capture capacity, not result traffic.

Span cannot be captured in the existing per-match ProviderCall(Func<T>) lambdas.
Use direct matching/advance calls with the existing error/first-refusal mapping;
factor that mapping and remove per-match delegate creation without introducing
another dispatcher or universal operation framework. Keep generic ProviderCall
where real compile/preparation callers need it. No producer API required: adapter
bridges can copy existing native ranges or legacy detailed captures into the span.

Expected breaking change is the provider matching surface. Normal adapter, test
providers, owned cancellation fixture and installed-package consumer must migrate
together. Core remains backend-independent; no producer pin promotion. dev44's
native allowance/token bridges remain adapter-local and separately qualified.

Acceptance: existing Python re/window/replacement/split/name/effective-flag tests;
cooperating-provider wrong metadata, in-flight cancellation, terminal refusal,
retained aliases/results/caches and cleanup/budget reuse; normal0.4 integration
and private immutabledev44 both actual runtimes. One bounded before/after direct
cursor allocation diagnostic with identical producer/subject/capture goldens and
whole regex script if needed. Separate API/ownership deletion, physical allocation
and CPU claims. Full platform/stress/package checks at the combined delivery.

Stop after one coherent ownership/API migration and bounded comparison; no final
buffer coalescing, producer one-shot funding redesign or new Unicode coordinate
cache unless independent later evidence justifies it. Missing producer APIs defer
only their final adapter qualification, not this core architecture.
