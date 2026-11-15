# Qualified live-default binding correction

Runtime **6e276759ebeea586bbb0c33abb361ffb3a30eb34** passes
[43717654522](https://github.com/Lokad/Lython/actions/runs/43717654522), terminal
**success**, 2026-11-15. Terminal status is cached; do not query it again.

Windows and Ubuntu each pass, on actual .NET11 and .NET10:

- Ordinary: 4,781 internal and 17,864 public cases per framework.
- Original stress: 13 internal and 406 public cases per framework.
- Explicit Release package installation, runtime, compatibility and mediated-file
  consumers on both targets. CI reports .NET11RC3 and .NET10.0.15.

All 16 test summaries and required step outcomes verify against the complete logs.
[Source proof](../source-proof.json) covers 585 production inputs, 4 build inputs
and the changed test file with explicit newline normalization. Both measured
images reconstruct exactly from the [source/image archive](../source-and-images.tar.xz).
This does not assert independent CI builds reproduce the measured binary hash.

The [focused correction and adverse diagnostic controls](../README.md) remain
unchanged evidence. No speedup, new CPython comparison or final regex-producer
qualification is claimed. Publication/tagging remain postponed. The canonical
activation redesign is separate unfinished work.

[Verified counts/consumers](qualification.json), [terminal state](ci-terminal.json),
[full logs](ci-logs.zip).
