# Direct UTF-8 materialization of regex groups

Match group reads now use the same admitted UTF-8 constructor as other
string operations. Short captures stay inline. A partial capture owns its
own immutable string and a full capture can still return the original
subject. The regex replacement renderer reads the same byte span.

Compared with `78ad4127d`, a local Windows .NET 11 Release diagnostic on
4,096 mixed-width scalars allocates 11.53% less when reading one-scalar
groups and 1.03% less when reading overlapping 512-scalar groups. The
unchanged position-only control is within 0.003%. Peak accounted memory
matches across all three scripts and cleanup restores the allowance.
Both complete worker images match the CPython 3.13.2 goldens using
the existing optional adapter. The two group-read scripts derive from
the [B963 coordinate workloads](../2026-11-18-regex-capture-count/comparison/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 578 focused Release cases,
including retained ranges, cancellation, terminal admission refusal
and budget reuse. The diagnostic measures allocation. Producer
qualification and elapsed timing are separate.

[Allocation](allocation-summary.json) · [Checks and procedures](checks-and-procedures.tar.xz).
