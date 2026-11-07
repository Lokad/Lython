# Canonical formatted execution — R26

Retain a correctness correction and deletion, with explicit elapsed/CPU debt.
On baseline01aedc3b, substantial output admission can change a later global
receiver after the full native-body type proof. The separate formatted evaluator
then throws raw CLR InvalidCastException. Preserve the failing regression source,
stack and original built image hashes. Plain string helpers have the analogous
late-read hazard. This is demonstrated; no timing is needed to justify correction.

Delete LythonRuntime.Executable.NativeFormatting.cs, its full builder-spine enum
operations, preparation and proof/evaluator selection. Keep direct single-value
format/conversion and measured primitive native returns. Existing native
preparation declines mutable name/global/subscript reads after potentially
allocating operations. Parameters/constants remain captured immutable references.
No native operation is retried after work or refused admission.

Multipart strings use the canonical executable slots and same UTF8 kernels.
Move existing builder operations to the ordinary synchronous instruction prefix;
closed primitive fields run there without creating a function context. A dynamic
operation requests the existing context at its current instruction and adopts
live values/builder without replay. Real asynchronous protocols and cleanup keep
their original path. Production removes more code than it adds; no new owner, journal,
backend, option, policy, unsafe code or public API change.

The wider single-pass native promotion proposal was not silently implemented.
The recursive evaluator lacks retained forwarded locals, operands and builder
state needed for arbitrary replay-free promotion. A new execution journal/arena
would broaden this bounded milestone. The selected deletion uses existing state.

## Allocation and controlled scripts

2026-11-07UTC, source14366ac7/01aedc3b runtime plus archived R26 patch;
candidate9e1dbc38. Local allocation reference4f063e7a and VM reference0933d540
have identical baseline production source, with distinct build metadata. Exact
source/image hashes are retained. Same b94fa98c worker/host and optional0.4
producer; normal GC/tiering. Later HEAD is not measured by these observations.

Existing eight-case complete-job allocation driver, sync and async, three isolated
images sharing CoreLib/key hashes: all goldens, balances, fixtures and isolation
pass. CSV/text/JSON/report/scalar/padding/slice allocation is effectively flat;
configuration process-allocation noise is below0.2%. Report3,300,313.5→3,299,968.5B
sync and3,301,450.5→3,301,243B async; original reference-control3,299,647.5/
3,301,009.5B. No new allocation reduction, heap/RSS or embedding-tail claim.
Earlier native-return allocation gains are not lost on these measured jobs.

B374 quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3: four jobs/87.91s,5s warmups per
role, seven rotated150ms rounds, reference/current/identical-current processes.
Full goldens/actual Ready images, terminal funding, quiet endpoints, Closed/joins,
removed fixtures and under-lease persistent verified archive pass. No SSH until
after150sowner+30sstop+10smargin. Warm includes mediated RAM I/O, full validation
and prior-result cleanup; startup/staging/final teardown excluded. Warm-async
uses completed host I/O, not genuinely suspended-I/O latency. Compile excludes
execution/I/O. No fresh or CPython comparison is inferred.

| Boundary / control | Reference | Current / identical current | Elapsed change |
| --- | ---: | ---: | ---: |
| Report warm sync |3.5613ms|3.5385 /3.5716ms|−0.64 /+0.29%|
| Report warm async |3.6021ms|3.7164 /3.7028ms|+3.17 /+2.80%|
| Text warm control |2.1414ms|2.1856 /2.1567ms|+2.07 /+0.71%|
| JSON compilation control |64.966us|64.649 /64.273us|−0.49 /−1.07%|

Report sync referenceIQR6.41%, async currentIQR4.02/3.07%, text role spread1.32%
and moving compile controls limit precision. Coarse process CPU is adverse
(sync report~+4.9%, async~+2.4–3.8%, text~+2.9%); do not claim CPU improvement.
Retain correctness and deletion with allocation flat. Treat async/text elapsed
as explicit possible regression debt, not parity. A later coherent execution/
operand milestone must decide remaining debt; no fixed-percentage veto or
permanent ban on justified confirmation. Whole-script objective stays open.

## Semantics and delivery

Release build0warnings/errors. Focused280internal/1,186public per actual.NET11/10
pass, including primitive/native returns, formatting, dynamic binding, source
frames/active exceptions, cancellation, first admission refusal, retained input,
shared budget reuse and original aliases. Four new cases cover updated receiver
reads through general/direct sync/async calls, actual async callback suspension,
token cancellation and a deliberately held cleanup while working storage stays
funded. Complete once, restore depth/exception state, cleanup before owner ends.
Initial deletion and subsequent canonical-prefix refinement have separate logs.
Explicit installed Release consumers pass both actual runtimes. Full Windows/
Ubuntu/original Stress/package delivery is pending the coherent commit boundary.

Initial B374 disk preflight refuses before creating the capture root or starting
workers. Refine setup to share the unchanged persistent catalog with its hash
verified, preserving the original failed payload/condition.2MiB free after
expanded payload is deliberate headroom against the prior15-job122KiB archive;
no perf trace in this four-job capture. An attempted retirement finds R24 files
already absent and deletes nothing. Retire only four exact B373 expanded copies
after independently committed/pushed14366ac7 archive/hash/user/link checks. Keep
all raw archives, catalogs and binaries. Inspection quoting/syntax failures occur
before read-only SSH executes; corrected parsed source and receipts are retained.
Publication/tagging remain postponed.
