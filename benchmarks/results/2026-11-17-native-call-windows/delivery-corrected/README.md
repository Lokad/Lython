# Historical qualification of the withdrawn prototype

Corrected R481 source abd3c788 passed [CI44072440304](https://github.com/Lokad/Lython/actions/runs/44072440304).
Both Windows and Ubuntu, actual .NET11/10, each pass 4820 internal / 18013 public
ordinary tests, original 14 / 406 stress tests and installed explicit Release
package consumers. All16 summaries,2382 committed inputs and585 production files
match the corrected source archive. Rebuilt binary equality is not inferred.

This is the discarded caller-window prototype, not current HEAD qualification
or evidence of a speedup. The first failed CI and both original and corrected
timings remain preserved. Stable corrected-source helper/text CPU regressions
and extra storage led to withdrawal. Current production restores qualified
d8611c85; its completed qualification is reused, with new retained regressions
checked on both actual runtimes. No repeated unchanged full qualification.

[Terminal](terminal.json), [verification](qualification.json),
[committed inputs](inputs.tar.xz), [logs](run-logs.zip).
Terminal and logs cached permanently; never requery this run. Publication held.
