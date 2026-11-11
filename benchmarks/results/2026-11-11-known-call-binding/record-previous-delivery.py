from pathlib import Path
import json,shutil
r=Path(__file__).parent;prior=r.parent/'native-closure-sequence-r243'
receipt=json.loads((prior/'ci-terminal.json').read_text(encoding='utf-8'))
assert receipt['headSha'].startswith('6904093e')and receipt['status']=='completed'and receipt['conclusion']=='success'
assert len(receipt['jobs'])==2
for job in receipt['jobs']:
 assert job['conclusion']=='success'
 for name in ['Test Release (.NET 11)','Test Release (.NET 10 checkpoint)','Verify Release package consumer','Test original stress scales']:
  assert next(s for s in job['steps']if s['name']==name)['conclusion']=='success'
for name in ['2026-11-11-native-closure-sequences','2026-11-11-captured-helper-cpython-checkpoint','2026-11-11-current-native-call-costs']:
 out=Path('benchmarks/results')/name
 shutil.copyfile(prior/'ci-terminal.json',out/'delivery-ci-status.json')
 p=out/'README.md';t=p.read_text(encoding='utf-8')
 t+='\nSubsequent exact-`6904093e` full delivery is **GREEN** on Windows/Ubuntu, actual\n.NET 11/10, original stress and installed Release consumers. The\n[terminal receipt](delivery-ci-status.json) is cached. Original collection-time\nstatus and adverse observations remain unchanged; this qualifies the retained\nsource, not later prototypes.\n'
 p.write_text(t,encoding='utf-8')
p=Path('docs/BENCHMARKS.md');t=p.read_text(encoding='utf-8')
t=t.replace('The six refreshed scripts use stock CPython 3.13.16 on the same VM; exact-source\nfull delivery qualification is recorded in the linked checkpoint.','The six refreshed scripts use stock CPython 3.13.16 on the same VM. Exact-source\nWindows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers\nare GREEN; the linked checkpoint preserves the terminal receipt.')
t=t.replace('Retained `6904093e` versus qualified `5f9059fa`; full delivery tracked separately.','Fully qualified `6904093e` versus `5f9059fa`.')
p.write_text(t,encoding='utf-8')
p=Path('PLAN.md');t=p.read_text(encoding='utf-8')
start=t.index('| Retained, committed/pushed runtime');end=t.index('| R243 measurements retained',start)
t=t[:start]+'''| Fully qualified runtime `6904093e` | Exact-source `43058223560` terminal GREEN: Windows/Ubuntu actual .NET 11/10, original stress and installed Release consumers. Cached receipt; never query again. 4,742 focused checks and ten Python goldens also pass. Earlier5f/b584/fe receipts cached. |
| Active R244 prototype, uncommitted | Compiler-owned source-slot facts guard on actual immutable signature identity; current native/owned binder consumes them, defaults remain live. No runtime cache/new owner. Initial123 and45 overlapping focused checks pass; wider correctness, ownership/cancellation and bounded source pair still required. |
'''+t[end:]
t=t.replace('| Next design | R244 source gate below. No new runtime prototype or predicted speedup. Current profiles replace old-offset assumptions. |','| R244 status | Source gate supports the bounded prototype. No timing/CPU claim yet; current profiles replace old-offset assumptions. |')
t=t.replace('Current full690 run43058223560 needs paced observation; prior terminal receipts\nare cached.','Full690 run43058223560 is terminal GREEN cached; no further query. Prior receipts\nare also cached.')
p.write_text(t,encoding='utf-8')
print('Exact690 complete platform/stress/package receipt recorded; R244 remains explicitly uncommitted/unmeasured.')
