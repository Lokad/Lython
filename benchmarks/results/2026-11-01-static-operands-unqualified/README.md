# Static operand layout and linear execution: unqualified, removed (B66)

2026-11-01. **Prototype removed.** The single comparison hit its90-second
service deadline. Its aggregate output is empty, with no final machine gate or
normal managed-cleanup result. No timing gain, regression or CPython ratio is
established; there is no retiming or smaller retained optimization fragment.
An independently demonstrated native-call admission correction remains.

## Design and semantic checks

[Plan before code](pre-code-plan.md). Replace runtime operand growth and per-frame
block-entry depth arrays with compiler-checked operand shapes, maximum capacity
and handler-entry depths. Replace retained block graphs and the nested driver
with one instruction array and absolute control ranges. Keep the same executable
engine, stable lexical frames, native helpers, simple-return optimization,
genuine suspension, current-token checkpoints and awaited temporary disposal.
Whole-frame preflight can deny earlier, including unused branches and large
unstarted generator workspaces. No new allowance, pool, tracing or backend.

The [exact patch](source.patch.gz) plus [new compiler source](ExecutableStackLayout.cs)
preserve the complete candidate. Initial model failures exposed that stored blocks
complete rather than fall through; the corrected model passes587 focused native
checks. The first fixed-buffer public selection exposed the generator constructor's
remaining old capacity hint; corrected before freezing. No language tests were
weakened. Final candidate596 native/5,725 public checks pass per actual.NET11RC3/
10.0.8, including protected control, generators, ownership and cancellation.

## One incomplete comparison

[Frozen collection](pre-collection-plan.md) separates warm CSV/text/JSON/numeric
reports, CSV/text compilation and fresh configuration processing. It uses
original/candidate/identical-original images with shared CoreLib, a hash witness,
ordinary GC/tiering, maintained inputs and full stdout/file checks. The five
source forms match isolated pinned CPython3.13.16 output bytes before collection.
That is semantic evidence, not a performance baseline.

Sourceac9cabaa plus the recorded patch/new file; fresh nonincremental build and
equal core copies. Candidate SHA256
`5ae871f8ed4ef5156b9419f6e44b5de6bbc75f000a7e7a9e4a88525f5142bd06`,
original`526b0a185fd4e3d9a960ea2734f4175c6f1cd58ba7e0d829955156254de8ce99`.
Auxiliary host/adapter bits agree. Dedicated Ubuntu24.04.4 Azure F4as_v7, four
EPYC9V45 vCPUs/16GiB, .NET11RC3. Initial quiet gate passes.

The owned service runs17:49:29–17:50:59UTC. It is terminal with
`Result=timeout`, `ExecMainStatus=15`, MainPID0 and an empty control group; its
lease is free. **The shell's EXIT trap wrote0 during termination; that file is
not a successful result.** Authoritative systemd state takes precedence.
The driver reports its observations only at the end, so the completed prefix's
individual records are unavailable. Do not infer which timing gates passed.
The three remaining temporary fixture trees were explicitly validated/removed
after termination. That is not proof of normal managed cleanup during the trial.

## Independent correction retained

Large native-call argument packets previously depended on incidental operand
growth for admission. Prime one wide call, fill the allowance while preserving
operand capacity, then attempt another. Both APIs invoke the consumer on the
original runtime: the strengthened regression fails twice. With separate working
admission before packet allocation, both refuse before the consumer, retain the
first structured origin and return private storage through cleanup. The existing
suspended-consumer test preserves funding through actual completion.

The retained correction reserves conservative capacity/metadata through the call
and its awaited cleanup. Existing tiny cleared pools remain control headroom.
Construction observes cancellation, with no new fuel/count policy. No public API
change or speedup claim. Runtime documentation describes this admission contract.

All static-layout/linear-execution changes and architecture-test adaptations are
removed. A fresh nonincremental rebuild matches all core copies; only the two
call-packet source files differ from the original source inventory. Same596/
5,725 focused checks pass per actual runtime. [Restored identity](restored-identity.json).
The first full local.NET11 check passes all15,607 public cases and3,964/3,966
native cases. The two native failures require zero reserved bytes while the consumer
is running, an obsolete bookkeeping condition now that its packet stays funded.
Remove only that assertion: live sync/async balances and every semantic assertion
remain, and AssertUnwound still requires zero reservation after each call. All44
class-method/admission controls pass on both targets. Full platform/stress/package
qualification belongs to the correction's delivery checkpoint, independently of
this incomplete performance trial.

[Summary](summary.json), [all validation logs](validation-logs.tar.gz), readable
driver/collector and [raw records](raw-results.tar.gz), SHA256
`e287a06204308ce34a12a401c57c9d31f903bb16602eb70ca12a3abe92ebf850`, preserve
the original protocol, terminal state, five reference goldens, missing results
and explicit post-terminal cleanup. Stop this candidate without retiming it.
