# R10 bounded source gate — existing constructor continuations

R09/933a7fff is retained: ordinary positional constructor transport and shared
sequence, four Python fixes, focused/full-local/installed checks; final CI remains
independent. B341 elapsed changes are modest and controls improve similarly.
No claim the whole-script gap or earlier class/text debt is resolved.

Verified source: ExecutionContext.TrySuspendForExecutableCall supports compiled
functions and bound compiled methods, not types. PyType.InvokeCoreAsync still
enters initializer execution recursively. The existing linear runner already has
CallCompletion.Call/TypeGetter/TypeSubscription and prepared-value retention;
BeginPendingCall, CloseInvocation and ResumePendingCall own admission/unwind.

Before a prototype, verify whether ordinary default-allocator construction can
use that same completion mechanism and retire R09's separate type operand entry,
dual array/operand input plumbing and object-target dispatch in the common wrapper.
Keep the shared generic constructor driver and admitted copies for real dynamic
forms. No new runner, constructor activation object, cache, frame pool, owner,
configuration or dormant backend. A source rejection is a valid outcome.

Critical gate: admit before allocator/observable work, allocate exactly once,
select init on the actual result after allocation, retain constructor result and
arguments through child/native completion, validate None before publication and
clear retained references after actual cleanup. Check whether eligibility can
change during admission; never restart construction or silently retry a fallback
after an allocation/refusal. Preserve generic custom allocators, static/classmethod
and descriptor binding, kwargs/defaults/variadics, metaclass/JSON behavior and
returned subtypes/unrelated values. Preserve first-origin refusal/cancellation,
Python exception/guard/finally order, generator/closure lifetimes and managed stack.

One source pass through those runner/cleanup/binding paths; stop with concrete
deletion/replacement/lifetime proof or reject if it needs separate state machinery.
Then focused constructor/type-subscription/function/stack/refusal/cancellation/
alias checks both actual runtimes before any source-pair comparison. Reuse B341
class records warm/fresh plus unchanged CSV/text controls, quiet VM/exact worker/
full goldens/owned joins/persistent evidence. No broad primary refresh until a
coherent milestone, no predicted gain and no repeated profiling in this gate.

Independent regex development remains available: exact immutabledev40 packages
and current source were assessed read-only, not qualified/adopted. Missing producer
capabilities defer only affected final adapter qualification. No publish.
