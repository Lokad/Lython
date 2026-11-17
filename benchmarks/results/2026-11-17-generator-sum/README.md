# Native sum shares the existing generator batch lifetime

R475/B886,2026-11-17UTC. Qualified dc480d38/frozen5ecb2599 versus three-file
prototype/frozenca1a8239. **Retain this coherent change; full delivery pending.**
Same-command native integer/compensated-floating prefixes use the existing yield
destination, without a materialized batch or repeated generator frame/exception/
managed stack entry per value. General addition runs after ordinary advancement
leaves the frame; unsupported values are not replayed. Existing private-list
buffering shares the extracted entry. No new backend, owner, cache or iterator
framework. The private accumulator changes from a stack struct to one64B-admitted
object, held until its users finish. Nongenerator sum still uses ordinary pulls.
Tiny budgets can reject earlier; the substantial resource protections remain.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON numeric report | Warm | **−10.21..−6.14%** | **−9.69..−5.28%** | 2.68 / 1.81% |
| Same | Compile and execute once | **−17.59..−14.53%** | **−17.12..−13.73%** | 2.14 / 1.55% |
| Integer numeric report | Warm | −13.42..−4.76% | −13.75..−3.98% | 0.03 / **9.98%** |
| Helper text control | Warm | −0.55..+2.11% | −0.79..+1.33% | 2.20 / 0.47% |
| Floating report | Compilation | −0.52..+1.33% | −1.84..+1.86% | 1.81 / 0.04% |

The floating whole-script CPU improvement supports retention; it is distinct from
allocation. Integer magnitude remains uncertain despite favorable pairings, and
R471's earlier integer debt is not declared resolved. Text movement includes a
small adverse result with a moving reference; retain that uncertainty. No causal
attribution of all movement or additive phase subtraction. Natural-GC allocation
increases168B/integer run and192B/floating run (about0.03/0.04%); text flat.
Numeric accounted peak906,718B unchanged, all shared budgets restore baseline.

Five jobs193.92s on quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, natural
workstation GC/normal tiering. Four processes/job, interleaved6s full-request
warmups and five rotated approximately0.5s samples. Ranges pair all process
medians, not confidence intervals. Warm includes synchronous Run, mediated RAM
I/O, complete stdout/file goldens and prior-result cleanup; excludes compilation,
startup/reset/protocol/final cache teardown. Fresh adds compile/compiled-owner
disposal; external CPU is wider. No async, CPython, RSS or embedding-tail gain
inferred. External baseline/representative controls are a separate checkpoint.

Both actual Windows.NET11/10 pass102public/54internal focused checks,50existing
cancellation checks and30final targeted checks (overlapping counts). Four new
generator cancellation cases exercise actual integer/floating drains. Numeric
compensation, overflow, consumer errors outside suspended handlers, reentry,
delegation, active exception restoration, genuine pending I/O, terminal refusal,
awaited cleanup and budget reuse pass. Four independent CPython3.13.2programs
agree. Copied32file images pass actual-worker3case goldens for each source plus
13candidate goldens. Full585production snapshot/nonincremental build preserved;
post-freeze test-only additions preserved separately. Original test-type assertion
failure is retained in the procedure archive, with correction documented.

All image/golden/quiet/Closed/exit/join/archive checks pass; RAM removed and
terminal cached. Fresh UID/path/hash/link/all-alias proofs retire only6,448,719B
of committed withdrawn-core/evidence replicas, preserving current images,
toolchains/canonicals and254/252MiBstaging/final floors. Publication held.

[Summary](summary.json), [allocation](allocation-summary.json),
[raw](observations.tgz), [custody](archive-receipt.json),
[source snapshot](candidate-sources.tar.xz), [final tests](final-tests.tar.xz),
[prototype](prototype.patch.gz), [core](candidate-core.dll.gz),
[identity](identity.json), [validation](validation.json),
[checks/procedures](procedures-and-checks.tar.xz).
