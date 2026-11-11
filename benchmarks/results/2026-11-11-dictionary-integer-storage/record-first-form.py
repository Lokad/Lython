from pathlib import Path
import json,shutil
r=Path(__file__).parent
summary=json.loads((r/'b588/summary.json').read_text(encoding='utf-8'))
assert summary['jobs']==6
data=dict(source='R245 first form, preserved before one-line shared-record read refinement',retention='Pending refined whole-script decision; first-form benefits alone do not qualify the refinement',focused='3566 actual11/10; four independent CPython3.13.2 goldens',allocation='Text -14.90..-16.13%; CSV -3.13%; JSON-lines -4.20%; compilation flat',quiet=summary,jit='Actual fixed common desktop worker/reportAsync: shared-record Tier1 code422→562B and local stack48→64B; generic lazy integer branch was inlined into shared-record lookup although its values are already materialized. Structural evidence, not attribution of original desktop18..21% slowdown.',archive='Collection6jobs168.715s complete/goldens/balances/quiet/join/cleanup passed. Service exit1 from stale five-job archival assertion; original helper/journal/status retained. Fresh exclusive-lease recovery corrected only expected actual count, verified archive independently; no timings repeated, no RAM evidence lost.',limitations='Report quiet adverse desktop result does not recur but timing cause unresolved; quiet report controls/IQR limit precision. CSV control4.56%, no universal neutrality. No RSS/embedding or pending-I/O latency qualification.')
(r/'initial-assessment.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
p=Path('PLAN.md');shutil.copyfile(p,r/'PLAN-before-r245-refinement-status.md')
t=p.read_text(encoding='utf-8')
start=t.index('**Active uncommitted prototype, not a delivered gain.**')
end=t.index(' Current captured profiles',start)
t=t[:start]+'''**Active uncommitted refinement, not yet delivered.** First form3566focused
actual11/10 and4Python goldens pass. Six-script complete-job pair: text allocation
−14.90..−16.13%, CSV−3.13%, JSON-lines−4.20%, compile flat. Desktop reportasync
+17.53..+20.70% with2.63%control remains adverse. B588sixjobs168.715s: captured
sync−6.95..−4.94%, async−5.09..−4.46%, ordinary sync−8.12..−7.59% with supporting
widerCPU; CSV/report less conclusive. All measurement/ownership/quiet/join gates
pass. Archival failed on a stale five-job assertion after all users/RAM cleanup;
original failed helper/service/journal retained. Fresh exclusive-lease archive
recovery verifies all six persistent observations, without timing repeat/loss.
B588 terminal/cached, no VM users. Exact sources/cores/data frozen.

Source-backed one-line refinement reads the shared record's materialized array
directly. Initial JIT inlined an unnecessary allocation branch there (422→562B,
stack48→64B); structural finding, no attribution of original report timing.
Refined5092focused actual11/10 pass, including JSON. Full-script pair and bounded
four-job quiet confirmation pending; no final CPU/neutrality claim.
''' +t[end:]
t=t.replace('B576–B587 terminal/cached','B576–B588 terminal/cached')
p.write_text(t,encoding='utf-8')
print('Original six-job observations/failure/recovery and initial JIT assessment recorded; plan distinguishes refined development from qualified690.')
