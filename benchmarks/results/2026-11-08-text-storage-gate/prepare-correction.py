from pathlib import Path
import json
root=Path(__file__).resolve().parent
p=root/'prepare.py';s=p.read_text();s=s.replace('text-allocation-b431-20261108','text-allocation-b432-20261108');p.write_text(s,encoding='utf-8',newline='\n')
s=(root/'launch.py').read_text();s=s.replace('text-allocation-b431-20261108','text-allocation-b432-20261108').replace('lython-b431','lython-b432')
a=s.index(' reclaimed=[]');b=s.index(' with tarfile.open(fileobj=',a)
s=s[:a]+''' reclaimed=[dict(priorFailedSetup='B431 failed before root creation or collector startup:free281833472 required282652672. Original source/log retained. Removed only archive-verified redundant extracts/core links. Corrected staging borrows identical already-frozen core instead of extracting a duplicate.',paths=['text-cost-b419-20261107/profiles','returned-record-b422-20261107/profiles','linear-keyword-b425-20261107/candidate/Lokad.Lython.dll','linear-keyword-b425-20261107/current-core.dll','csv-call-debt-b426-20261108/current/Lokad.Lython.dll'])]
 for name in reclaimed[0]['paths']:assert not (base/name).exists()
''' +s[b:]
s=s.replace(" shutil.copytree(reference,root/'candidate',copy_function=os.link)"," shutil.copytree(reference,root/'candidate',copy_function=os.link)\n os.link(reference/'Lokad.Lython.dll',root/'current-core.dll')")
(root/'launch-corrected.py').write_text(s,encoding='utf-8',newline='\n');compile(s,'corrected','exec')
p=Path('PLAN.md');s=p.read_text().replace('3072 complete','1024 complete').replace('first status no earlier00:55:26.','CI42431587731 is running; status observations at least5minutes apart.').replace('R75/B431 has not launched yet.','B431 staging stopped before collection on the disk-floor gate. B432 uses the identical frozen core without a redundant extracted copy; no changed runtime/source or discarded timing. Bounded live status is recorded privately.')
p.write_text(s,encoding='utf-8',newline='\n')
