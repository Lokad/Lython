# Deferred invocation contexts — 2026-11-06

R13 uses one executable instruction implementation with an optional lexical
context. Closed module helpers start against their already admitted bound locals
and operand state. Scope-sensitive work, protocols, nested calls and host I/O
materialize the normal context at the current instruction, adopting the same
storage without rebinding or replay. Capturing/protected/class-cell bodies start
normally. Existing native-return and direct UTF-8 paths remain active.

Operand storage retains shared execution services rather than a lexical context.
Function/interpreter admission transfers exactly once. Native string-key dictionary
writes complete only in already funded native storage, with one lookup and the
same publication helper as ordinary writes. Growth materializes before admission.
No second interpreter, saved caller record, slot arena, context swap, pool,
ownership API, policy or public option was introduced.

This is an active performance milestone, not a claim that the whole-script
objective is complete. Final qualification and regression-debt decisions belong
in [validation.json](validation.json).

## Original prototype: B346

[Assessment](b346-assessment.json), [identity](b346-identity.json) and
[immutable observations](b346-observations.tgz) preserve the first source pair.
It predates the Slice correction and shared no-growth write refinement.

| Workload | Warm elapsed vs qualified R10, two identical roles | Fresh elapsed vs R10 |
| --- | ---: | ---: |
| CSV summary/helper | −3.93% / −1.72% | −1.58% / −3.50% |
| Text summary/helpers | +4.80% / +2.84% | −4.97% / −2.86% |
| JSON-lines/helpers | +5.77% / +3.49% | +10.57% / +4.29% |
| Report | +2.80% / +5.00% | +0.58% / −0.03% |

Negative means faster; 100×(candidate/reference−1), medians of seven rounds.
Adverse results remain visible. Text fresh elapsed and process CPU conflict;
unchanged JSON compilation diverges 18.17% between identical candidate roles.
Three-second warmup results establish uncertainty and debt, not a CPU gain or
automatic rejection. No exact allocation/live-memory/RSS/embedding-latency claim.

B346 completed twelve compile/warm/fresh jobs in 228.43s, all quiet/identity/
golden/Closed/join/fixture gates. Reference is qualified R10 source `88f12099`,
source-equivalent core `1d1eff12`; prototype `91628fe2` is `3eff1b37` plus the
archived patch. Frozen worker `b94fa98c`, CPython 3.13.16, normal GC/tiering on the
existing Azure quiet VM. Warm/fresh include mediated RAM I/O, full validation and
prior-result cleanup; fresh adds compilation/program disposal. Startup, staging,
reset, transport and final teardown are outside clocks. CPython imports persist;
Lython imports are isolated. Compilation is timed separately, not subtracted.

The full initial local pass found one existing Slice regression: 4,259 internal
passed and 16,185 public passed/one failed. The insertion script had omitted the
Slice context guard and duplicated GetIter's guard. Corrected the existing case
before effects, removed the duplicate and added a both-API late-index golden.
Original failures/build logs are in [focused-checks.tgz](focused-checks.tgz).
Source inspection also removed the first full-table write predicate's repeated
lookup by sharing actual publication. [Refinement rationale](refinement.md).

## Corrected source: B347 and decision

[Assessment](assessment.json), [identity](identity.json) and
[observations](observations.tgz) retain the corrected source pair. Twelve jobs
completed in 328.10s with the established five-second warmup, seven rotating150ms
rounds and all original gates. Core `1daf56c3` is the final archived source patch;
both candidate roles load identical bits. All workers joined and fixtures closed.

| Workload | Warm elapsed vs R10, two identical roles | Fresh elapsed vs R10 |
| --- | ---: | ---: |
| CSV summary/helper | −5.85% / −6.24% | −5.53% / −5.83% |
| Text summary/helpers | +3.40% / +6.45% | −0.22% / +2.50% |
| JSON-lines/helpers | +1.77% / −0.36% | +2.73% / +6.74% |
| Report | +2.91% / +1.42% | −2.48% / +0.53% |

Retain the coherent context/adoption implementation provisionally with this
explicit debt. CSV elapsed improves in both roles, with0.31–0.42% role differences;
warm CSV process CPU is flat, so this is not a broad CPU gain. Text warm remains
adverse, JSON is mixed/adverse, and report remains variable. Identical roles still
differ2–4% in several jobs despite low within-role IQR. Five-second warmup corrected
the extreme compiler divergence, not all variability. PyString's System.HashCode
and native bucket layouts make process-specific hash layout a plausible contributor;
that hypothesis has not been measured. No exact allocation reduction is claimed.

Seven CPython goldens and focused296internal/1,078public per actual.NET11/.NET10
pass on the final source, including real suspension, retained aliases, refused
growth, native error traces and budget reuse. Earlier broader focused qualification
and the original full-suite failure remain scoped separately in validation.json.
Full platform/stress/installed-consumer delivery is pending. The next runtime step
must address the common execution/early-promotion surcharge or explicitly resolve
that debt before calling R13 a performance completion. Do not treat another isolated
experiment as completion of the substantial whole-script objective. No publication.
