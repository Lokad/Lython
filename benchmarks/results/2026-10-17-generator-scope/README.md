# Generator scope and empty metadata allocation diagnostic

One local Windows capture on2026-10-17 UTC compares Lythond95638ba,
scope-only commit72feb124 and a[frozen combined patch](combined.patch).
The boundary is16 fresh public Compile calls,validity checks and deterministic
compiled-owner disposal per sample. Sources,engine and shared allowance already
exist. There is no script cache. Parsing,analysis,lowering and executable-code
construction are included;guest execution,startup and host I/O are excluded.

| Definition form | Baseline bytes | Scope removal only | Combined with empty metadata |
| --- | ---: | ---: | ---: |
| Generator function | 1,803,520 | 1,764,608 | 1,762,560 |
| Ordinary function | 1,579,392 | 1,579,392 | 1,577,344 |
| Generator lambda | 1,407,872 | 1,407,872 | 1,407,872 |
| Ordinary lambda | 1,145,728 | 1,145,728 | 1,145,728 |

Each cell is the median of eight managed-allocation samples after four warmups.
The ordinary-function baseline has one192-byte high sample;scope-only has one
128-byte high sample. Every other sample equals its cell median;all raw values
are preserved. The scope-only change saves2,432bytes per generator compilation
at the median by skipping an unused collector result. Reusing a framework
immutable empty default-expression map saves another128bytes per default-free
function. Runtime defaults remain separate mutable values. The lambda controls
have cached scope facts and show no allocation change. These are small compiler
allocation improvements;no elapsed-time,execution-speed,CPython multiplier,RSS
or embedding-latency gain is established. They are not a current-HEAD timing.

The compiler helper previously accepted already compiled generator code but its
caller eagerly constructed scope facts first. Both function and lambda callers
now short-circuit before helper arguments;the redundant parameter/branch are
removed. Generator-lambda default ordering remains unchanged. The combined patch
also uses one indexed pass and creates a default-expression dictionary only
when a real default exists,removing LINQ filtering and empty per-definition
dictionaries. There is no new
scope-fact cache,analysis representation,runtime owner or configuration option.
FrozenDictionary.Empty is supplied by the existing .NET10 framework.

Machine:Intel Core i7-14700KF,28 logical processors,Windows10.0.26200 x64.
Source-linked Release .NET10 white-box test assembly;SDK version and machine are
recorded in[evidence](evidence.json). The exact test-runtime patch was not
captured with these samples;PowerShell's runtime is recorded separately and is
not a test-runtime claim. GC/tiering/protection settings are ordinary;there is
no forced collection or long benchmark lane.

[Evidence](evidence.json) contains every sample,source/binary identities,process
receipts and bounds. Raw TRX files preserve original output. The[temporary probe](GeneratorCompileAllocationProbe.cs.txt)
was removed before final qualification. Reproduce by checking outd95638ba,
copying the probe into the white-box test folder,building Release and selecting
GeneratorCompileAllocationProbe. Apply[scope-only](scope-only.patch) or[combined](combined.patch)
against the baseline and repeat once;remove the temporary source afterward.
Keep the existing28-second owned-process limit plus one second for cleanup.
An intermediate metadata prototype used an Any prescan. The final indexed pass
removes that extra traversal and has the same allocation medians. Its[archived
patch](metadata-prescan-prototype.patch),samples and final confirmation are in
the evidence;neither temporary probe nor prototype is compiled by ordinary tests.
This is an archived recipe,not another benchmark framework or an ordinary test.
