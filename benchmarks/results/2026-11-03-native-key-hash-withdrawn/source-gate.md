# B139 native key construction source gate, 2026-11-03

Current commit802fb318. B138 semantic/ownership focus-qualified; full delivery
41554275552 in progress. One current-source map, not another CPU-capture campaign.

ExecutionState.GetJsonPropertyName calls Dictionary.TryGetValue with a ROM probe;
Utf8PropertyNameComparer hashes it. A miss copies into owned PyString, then Add
hashes the owned ROM again. First PyDict insertion uses PyValueComparer and the
new PyString's lazy hash, which repeats exactly the native HashCode.AddBytes
kernel. Repeated cache hits share the PyString hash; unique names pay three passes
and the extra index/probes. Report gain and unique regression are measured; precise
causal CPU attribution among those added operations is not yet measured.

OrderedPyDictStorage already unifies Python insertion order and original-key
retention via IPyDictStorage. General custom-key scans, mutation versions/views,
async equality and aliases are meaningful. A shared record-shape replacement would
be a substantial storage redesign, not a free removal of a BCL wrapper. Do not
start it merely because B138 creates a second index. Existing positional user
calls already consume operands into final locals, use explicit admitted frames
and can omit contexts for closed native returns; no lost array copy was found.

Candidate before-code decision: consolidate UTF-8 probe hashing, owned string
construction and hash initialization using one safe BCL kernel, with a prehashed
content key. This can remove two hash passes but adds a hash field/padding to index
entries. Weigh actual complete-script and unique-input cost; do not predict gain.
No mutable-comparer last-key cache, input-memory table keys, guest tracing, new
owner/pool, second parser or universal interner. Borrowing original input is unsafe
for the ownership proof: native temporary users may relinquish it independently,
while stored names must retain independent admitted storage. No raw-memory tricks.

If selected, define exact displaced code, semantic/hash/Unicode/admission tests,
workloads and bounded comparison before implementation. Include short and longer
unique names to distinguish payload-hash work from indexing/allocation; retain all
adverse conditions and test a shared hash seed with unchanged controls. One coherent
completion decision, including remaining allocation/unique CPU debt. Otherwise make
an explicit tradeoff/withdrawal decision and proceed to a different substantial
roadmap area. The performance objective stays open; producer release remains
independent. No new implementation selected merely by this source gate.
