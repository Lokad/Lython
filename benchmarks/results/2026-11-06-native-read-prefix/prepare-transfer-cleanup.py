from pathlib import Path
import hashlib,json,re,subprocess
repo=Path('D:/code/Lython');root=Path(__file__).parent
paths=subprocess.check_output(['rg','--files','.git/agent-notes/review-20261031','-g','launch.py'],cwd=repo,text=True).splitlines()
rows=[]
for name in paths:
    if 'retrieved' in name:continue
    launch=repo/name;text=launch.read_text(encoding='utf-8')
    payload=re.search(r'/home/lokad/lython-bench-checks/(b(\d+)-payload\.tgz)',text)
    unit=re.search(r'--unit=(lython-[^\s\'\"]+)',text)
    local=launch.parent/'payload.tgz'
    if not payload or not unit or not local.is_file() or not 290<=int(payload[2])<=326:continue
    if payload.start()>unit.start():continue
    rows.append(dict(name=payload[1],unit=unit[1],localCopy=str(local),sha256=hashlib.sha256(local.read_bytes()).hexdigest(),bytes=local.stat().st_size))
rows=sorted({r['name']:r for r in rows}.values(),key=lambda r:r['bytes'],reverse=True)[:16]
current=root/'payload.tgz'
rows.insert(0,dict(name='b336-payload.tgz',unit='lython-b336-native-read-prefix-20261106',localCopy=str(current),sha256=hashlib.sha256(current.read_bytes()).hexdigest(),bytes=current.stat().st_size))
(root/'transfer-cleanup-plan.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
script='''from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks')
rows=PLAN
record=dict(beforeFreeBytes=shutil.disk_usage(root).free,targetFreeBytes=64*1024*1024,removed=[],retained=[])
for row in rows:
    if record['removed'] and shutil.disk_usage(root).free>=record['targetFreeBytes']:break
    path=root/row['name']
    if not path.is_file():record['retained'].append(dict(name=row['name'],reason='already absent'));continue
    assert not path.is_symlink() and path.resolve()==Path('/home/lokad/lython-bench-checks')/row['name']
    if hashlib.sha256(path.read_bytes()).hexdigest()!=row['sha256']:
        record['retained'].append(dict(name=row['name'],reason='remote differs from retained local copy'));continue
    status=subprocess.check_output(['systemctl','show',row['unit'],'--property=LoadState,ActiveState,MainPID,Result'],text=True)
    stopped='MainPID=0' in status and any(s in status for s in ['LoadState=not-found','ActiveState=inactive','ActiveState=failed'])
    if not stopped:record['retained'].append(dict(name=row['name'],reason='owner not terminal',service=status));continue
    row.update(path=str(path),service=status)
    path.unlink()
    record['removed'].append(row)
record['afterFreeBytes']=shutil.disk_usage(root).free
print(json.dumps(record,indent=2))
'''.replace('PLAN',repr(rows))
(root/'reclaim-owned-transfers.py').write_text(script,encoding='utf-8')
print(json.dumps(dict(candidates=len(rows),maximumBytes=sum(r['bytes'] for r in rows))))
