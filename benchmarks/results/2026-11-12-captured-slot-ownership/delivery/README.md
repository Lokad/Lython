# R281 full delivery qualification

**9bca1337f02a9acc172c65c141ce626222b847bd** passes
[CI43295336048](https://github.com/Lokad/Lython/actions/runs/43295336048).
Both jobs finish successfully; terminal status is cached, never re-polled.

Windows and Ubuntu each pass actual .NET11/.NET10 ordinary Release tests
(4590internal/17448public per target), original Stress (13internal/406public per
target), and explicit installed Release package consumers on both targets.
No relaxed timeout, skipped original stress scale or new producer qualification.

All568 frozen measured production sources match this exact commit with explicit
newline normalization. The pre-dispatch proof, complete CI logs and terminal
state are archived. New R284 prototype worktree changes are **not** qualified by
this run. No unchanged delivery repeat or publication follows.

Retain [source-pair benefits and accepted debt](../README.md) and the independent
[CPython checkpoint](../../2026-11-12-captured-cpython-checkpoint/README.md).
