# B197 explicit binding order before next freeze

Additional independent probe finds f(): z=1; a=2; return list(locals()) produces
[a,z] instead of CPython[z,a]. Existing3871 public/776 internal focused cases had
not asserted this ordering. Prior9cc05a38 capture remains diagnostic for its exact
source, not retained qualification. Do not hide this regression or sort tests.

Replace sorting with explicit first-binding order carried by the existing shared
ScopeDirectiveFacts. The one binding walk collects names into an ordinary list;
existing membership sets still settle globals/nonlocals after the whole scope.
Freeze unique local order once; builder consumes it after its unchanged argument
prefix. Modules keep actual-store interning from the lifetime correction. Broaden
name-collection helpers to ICollection so current HashSet consumers remain valid;
no new visitor, ordered-set framework, runtime policy or nullable compatibility path.
Comprehension/lambda/type-parameter facts supply explicit order too. This adds a
small retained order array per scope and temporary name-list/dedup work; preserve
compilation preflight/headroom and measure allocation rather than assume savings.
Remove the old directive/local/pattern collectors as intended. Add CPython-checked
function locals order assertions, including arguments, repeated bindings and dead
control bodies. Validate existing semantics/ownership/cancellation before a short
corrected source-pair; preserve prior samples and failed conditions.

Compound with/try/match scopes preserve their own lexical interleaving rather than
batching all headers before bodies. A partial runtime mirror inserted match capture
before every other locals key; canonical frame snapshots now precede extra namespace
entries and keep current cell values. Existing maps/membership sets remain; no new
ordered-set class or compatibility branch. The fixture initially used unsupported
contextlib, then a supported guest Manager protocol; both failed pilot conditions
are retained. Parameter/with/try/match ordering now has an independent CPython golden.

Final-source comparison: five existing jobs (CSV/report compile, fresh config,
warm CSV/report), two unchanged B194 roles, same5s warm/9~200ms/quiet/hash/ownership
protocol plus separate8-case compilation allocation. ~140s under210s owned bound.
Earlier9cc source's completed151.380s capture (151.986s service) is diagnostic,
not qualification of this later representation. All original results retained.
