# Current builtin-method source and CPU gate

2026-11-04,a00fe76d/frozen1dcca6f2. Two complete scripts, exact loaded core and
checked file/stdout goldens: Unicode/NUL long-line prefix/count filter and existing
CSV aggregation. Existing external perf199Hz/frame-pointer collector,6sec guest
warmups and4.13/10.07sec actual guestCPU;810/2,000 guest samples. Normal GC/tiering,
only explicit symbolization-map override. No runtime instrumentation/unsafe code,
source-pair timing, speedup prediction or new benchmark framework.

Long-line ReadLine has29.51% leaf samples, instruction prefix7.78%, native byte
search2.84%, builtin argument binding1.60%. ReadLine includes inlined copy/admission/
scanner work;29.51% is NOT the newline loop's attributed cost. Actual shared
TextLineScanning source performs scalar per-byte mode/terminator branches.
CSV prefix11.60%, parserFeed7.30%, readerNextAsync6.15%, monitorEnter/Exit3.20/2.70%;
these are leaves, not all removable overhead. Unknown6.54/5.95%, malformed terminal
map lines retained. [All counts](assessment.json).

General methods still have per-receiver closures and receiver metadata, five
string providers and validity-path error-signature construction; ten direct
sentinels/no-argument static operations already exist. Prior ordinary-function
frame4.5% is not builtin attribution. These profiles do not establish a dominant
removable general-method mechanism; do not add a descriptor framework or another
arity series on allocation totals. Reopen with changed deletion/cost evidence,
not a permanent ban. Operand-cleanup scanning is likewise not the whole prefix.

Next selected bounded change: replace the existing shared per-byte newline scan
with managed span searches, choose newline mode once, preserve all terminators/
windows and caller checks. It removes actual repeated work in the measured hot
reader without a new backend, representation, owner, cache or policy.

The diagnostic catalog initially failed native size-metadata validation BEFORE
capture; retain original manifest/exit2 stderr. Correct metadata/digest only,
source/file bytes/output unchanged. Both completed workers/collectors exit0,
fixtures removed, both quiet gates pass, service inactive/MainPID0, lease free.
All maps/traces are persistent and archived; named control FIFOs excluded.
[Original plan](plan.md), [raw evidence](profile-observations.tgz), SHA256
`58fb13501a2f641440472b8425a9e2812f588a8f72de597656934b692f16fd9e`.
