# Container ownership of fresh split strings

The implemented representation is an `IPyListStorage` decorator. It adds no
state link to PyString and no field to PyList. Eligibility is restricted to
fresh, distinct governed string slices from native split, rsplit and splitlines
producers. Shared Empty stays ungoverned. Already registered values, unrelated
owners and the path/difflib adoption helpers preserve eager adoption.

The ordinary weak list entry initially owns backing storage, fresh payloads,
128 bytes of decorator metadata and conservatively prefunded 128-byte entry fees
for every nonempty string. Count/truthiness do not expose values. Native sync
join borrows bytes through the existing `CheckedSequence` cancellation/checkpoint
policy, with no guest callbacks. Async join and arbitrary iterables acquire
through normal iteration. Borrowed strings cannot reach guest cache operations.

Before a getter exposes an item, its ordinary independent weak entry is funded
and published; actual tier growth is reserved first. Its existing payload and
entry fee then transfer out of the list snapshot without a second charge or
refund. Successful prefunded publication marks allocation progress. Indexing
acquires one identity, slicing the selected identities. Bulk storage copying and
mutation acquire all remaining strings. After the last transfer, the decorator
detaches and its metadata releases. Every intermediate snapshot reconciles with
the pool/governor totals, including a caught transfer denial and funded retry.

The decorator keeps its owning list alive during transfer and exhaustion relief;
no escaped string holds the decorator, parent or siblings. Retaining one item
and dropping its list collects its unpublished siblings while preserving that
item's construction/cache charge. Clear, zero repetition and whole replacement
can discard the remaining aggregate directly. Independently transferred strings
continue under ordinary weak-entry accounting. String cache updates refresh
their independent entry snapshots.

Storage dispatch covers PyList indexers/GetItem/GetIndex, slices, copying,
ToArray, raw storage enumeration, mutation and capacity growth. Live Python
iteration still observes the owning list's Count/indexer, so guest mutation
behavior remains intact. Copy/deepcopy, sorting callbacks, conversion, rendering
and public projection consume those ordinary acquisition gates.

Creation reserves metadata/fees before attaching; denial refunds unpublished
payloads/backing. List-entry publication retains its existing refund contract.
Failed string transfer keeps the remaining aggregate and all earlier successful
transfers funded; retry resumes without exposing a denied value. No budget,
checkpoint, host-mediated I/O contract or generic registration path is weakened.

Tests prove exact list/pool/governor snapshots, both creation denial stages,
partial acquisition denial/retry, selected escapes, collected parent/siblings,
pending/partially escaped clear, alias/copy/slice/repetition/mutation, cache
construction/denial, public projection, and first/every-64-pull cancellation.
An independent governor regression additionally requires collected young entries
to drain before a live promotion can block exhaustion relief. Neither this bug
nor the prefunded progress bug is a demonstrated C05 cause.

The first all-at-once prototype was superseded after both index micros regressed.
The per-item refinement keeps repeatable split/count and split/join gains while
removing that regression. [The report](README.md) retains both source comparisons,
all measured controls and limitations. Native allocation cost shares and strict
cross-language qualification require separate evidence.
