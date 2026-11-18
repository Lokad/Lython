# Direct keyword overflow delivery

GitHub Actions run `44376873862` passed at `045eb7f4428a302caf39016d99f4a833f45cb85b`.
Windows and Ubuntu passed Release on .NET 10 and .NET 11, including the
installed-package consumer. The ordinary white-box and public suites passed
4,881 and 18,072 cases per target and platform. The original-scale stress
suites passed 14 and 406 cases.

Applying the archived B991 source patch to `2f98d36db` reconstructs the
delivered 589 core production files, tests and comparison worker sources.
The CI artifacts and performance workers were compiled independently.

[CI run](https://github.com/Lokad/Lython/actions/runs/44376873862) ·
[Verified summaries](qualification.json) · [Source proof](source-proof.json) ·
[Official logs](run-logs.zip)
