# R285 — private iteration transport source gate

Source da61f00f, production identical to qualified9bca1337. No runtime prototype.

`PrepareCasedWhitespaceIteration` already rents ranges through the execution's
existing WorkspaceCache. One CasedWhitespaceIteration carries source, mapping,
context/span, range loan/count and position. Ordinary list/tuple iteration holds
the real guest source in operand storage and a lazily admitted frame position
array. The cursor is not simply redundant with that array: it can exist before
the consumer's GetIter, outside the producing frame's lifetime.

The consumed-return intent follows native direct calls, named/bound binding,
ordinary deferred prefixes and materialized functions. Native StoreClosure and
Conditional propagate it only to the actual root. BeginBoundCall and
ApplyCallResult carry object results; protected PendingReturn must keep its source
and loan alive while finally performs genuine asynchronous host I/O. Completion
can be overridden by another return or exception. Public/protocol/generator
returns keep ordinary Python values. The existing ReturnedIteration tests cover
these independent semantics and pending cleanup, not a cursor layout.

Putting state only in an immediate native caller would miss full framed and
protected returns. Boxing a state struct preserves an allocation. A large inline
result union changes ordinary call/return transport, pending abrupt state and
async handoff, rather than deleting a carrier locally. A frame side table needs
an explicit transfer representation and funded lifetime; using a guest source
as a hidden sentinel is not sufficient. Pooling adds reuse/clearing/retention
machinery without eliminating the protocol. No such layer is selected merely
from allocation presence. It is not proven infeasible or permanently rejected.

**Close this source gate without a production change or another unchanged timing.**
The coherent larger alternative would unify *all* executable private-result
ownership through native/framed/protected returns and remove the object carrier,
separate positions and displaced cleanup together. The current source does not
yet show that this broader machinery is simpler or a dominant CPU improvement.
Prepared ranges and R281 authoritative cells already remove proven duplicate
work; do not count them again. Preserve eager admission, context-sensitive Unicode,
last Python bindings before refund reentry, terminal refusal and awaited cleanup.

Next: a bounded current-image diagnostic on captured text, CSV and report using
the existing complete scripts/host. Separate executing guest frames from host
validation/reset and GC/runtime stacks. Local sampled diagnostics may identify a
source hypothesis, but cannot establish quiet-VM CPU improvement or CPython ratios.
Select a materially different redesign from those costs, with displaced code and
semantic/ownership acceptance, rather than replacing this gate with another
cursor-field or call-arena tweak. Full qualification waits for retained source.
