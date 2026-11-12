# First R274 delivery and numeric preflight fixture correction

Exact43541e78 ends [run43249014616](https://github.com/Lokad/Lython/actions/runs/43249014616)
with Ubuntu green across actual .NET11/10 full Release, stress and installed
consumers. Windows passes4,581internal and17,442public ordinary cases, but fails
one numeric power assertion; later Windows stages are skipped. Terminal state is
cached. This is not a fully qualified cross-platform delivery.

The power test observes terminal shared-memory refusal, then measures141,336B
of process-wide allocation from a host gate through error projection, cleanup and
xUnit assertions against131,071B. Preserve the original failed condition. Its
exact excess-allocation cause is not established; do not label it a demonstrated
runtime regression or proven external noise.

The corrected fixture uses its existing allocation snapshot at first refusal to
check admission before magnitude/workspace allocation. It keeps the same131,071B
bound and adds a structured requested-size lower bound proving the large numeric
storage was denied. Failure attribution, attempted finally-return suppression,
empty output and shared-budget cleanup assertions remain. No runtime code, budget,
timeout, stress scale or resource policy changes.

A bounded negative control temporarily displaces only eager power admission,
leaving result admission in place: the new preflight allocation assertion detects
the unadmitted magnitude; the unchanged shift case passes. Exact original production
bytes are restored in finally. The first control invocation used the wrong SDK
host and never built a mutant; that log is retained, then the pinned executable
produces the expected single test failure. A forced nonincremental Release solution
build passes0warnings/errors, followed by29focused cases per actual.NET11/10.
All568production hashes remain identical to the measured R274 source snapshot.
The corrected source needs its own coherent full delivery boundary; no timing replay.
