# Original native-for qualification: failed Windows golden fixtures

Run43560252672 qualifies exact `3d78d429`/frozen `8306699f` and remains **failed**.
Windows passes4738white-box and17700public cases, but five new public goldens
inherit CRLF from checkout while the runtime correctly emits LF. Later Windows
checkpoint stages are skipped. Ubuntu completes actual11/10, original stress and
installed Release consumer steps successfully. Logs and all581normalized frozen
source/commit matches are preserved; this is not full cross-platform qualification.

Only expected-output fixtures change to escaped LF literals generated from the
original independent Python controls. Actual output comparisons remain exact.
Forced nonincremental builds of physically CRLF source pass all12for-helper cases
per actual11/10. Those local checks use the active R346prototype, so full corrected
R345/descendant qualification remains pending. Batch it at the next retained cursor
checkpoint; do not rewrite this original failure or repeat unchanged timing.
Publication remains held.

[Terminal jobs/steps](ci-terminal.json), [source/log assessment](partial-qualification.json),
[correction](golden-correction.json), [raw logs, original fixtures and CRLF checks](failed-qualification-and-correction.tar.xz).
