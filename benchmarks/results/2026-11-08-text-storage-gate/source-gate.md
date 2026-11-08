# Current representation gate

Source33b6c191. ExecutableValueStorage already shares local and temporary object
storage; direct input indices and result publication skip redundant stack moves.
Native helper returns, readonly call descriptors and intrinsic sequence adoption
already remove ordinary activation and operand packaging. SplitWhitespaceCore
admits final fragments/list capacity together and writes lower/split output
directly, so there is no intermediate cased line or per-word reservation to remove.
RuntimeValue preserves numeric references; it does not unbox/rebox constants.

Exact integer addition still produces one boxed BigInteger result. A typed operand
array alone cannot remove boxes retained in dictionaries, mirrored namespaces,
closures, generators, protocol arguments or pending operations. Replacing every
reference with a wider tagged slot also increases frame capacity and can force
materialization at ordinary boundaries. Any selected design must preserve identity
across copy/alias/rebinding, materialize once when exposed, retain original boxes
from opaque calls, and promote exact overflow without restarting guest work.
Unbound locals, exception/cleanup order, current cancellation and first terminal
admission refusal remain required. Scope capacity and surviving values stay funded.

Allocation ticks can select a storage phase, not prove that phase dominates CPU.
Use the existing R63 guest CPU leaves as an independent, revision-labelled clue;
do not add inclusive frames into saved-CPU estimates or attribute collector cost
to normal execution. No runtime prototype is justified by the existence of boxing
alone. Retain R54/R60 array-window, call-arena and call-control adverse lessons.
