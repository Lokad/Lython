from pathlib import Path
import shutil
r=Path(__file__).parent;p=Path('PLAN.md')
shutil.copyfile(p,r/'PLAN-before-r247-confirmation.md')
t=p.read_text(encoding='utf-8')
t=t.replace('Focused actual11/10 and controlled source pair pending; no performance or qualification claim.', '3,046 focused actual11/10 checks pass. Desktop runtime/compile allocation flat; capturedAsync elapsed+7.48..+8.35% with0.81% unchanged control, other rows conflicting. B592five-job quiet confirmation active under180sowner/240sobserve; no retention or general CPU claim. Source pairs frozen before further edits.')
anchor='The investigation stops at a usable bounded observation or a concrete obstacle;'
t=t.replace(anchor, 'Desktop complete-script comparison passes output/files/balances/cleanup. Its\n'+
 'tight-control capturedAsync adversity justifies one quiet confirmation rather\n'+
 'than automatic rejection or promotion. Initial disk inventory refused before\n'+
 'mutation; original refusal preserved, reserve unchanged. Exact committed\n'+
 'duplicate profile maps make sufficient space under a fresh idle/alias proof.\n'+
 'B592ack20:07:44UTC; observe only after20:11:44UTC. No live CI polling.\n\n'+anchor)
p.write_text(t,encoding='utf-8')
print('Active prototype, passed focused checks, adverse desktop evidence and bounded quiet confirmation recorded separately from qualified runtime.')
