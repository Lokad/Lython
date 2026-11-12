from pathlib import Path
import ast,fcntl,hashlib,io,json,os,shutil,stat,subprocess,sys,tarfile
config={'root': '/home/lokad/lython-bench-checks/output-cpython-b609-20261112', 'unit': 'lython-b609-output-cpython-20261112', 'payloadBytes': 47670, 'payloadSHA256': 'fb6c77e7abe6d315292a45c82987517f4ddf9771e360b197fea165848d6496e5', 'requiredFreeBytes': 265547776, 'retire': [{'kind': 'exact', 'knownPath': 'text-batches-b608-20261112/observations.tgz', 'bytes': 1989536, 'sha256': '3439b0eaac3f75a7601e2008ec4b66ea196a842f7c14cb9484359ceb1b27f72f', 'canonicalStorage': 'benchmarks/results/2026-11-12-text-output-batches/refined/b608/observations.tgz', 'reason': 'Only terminalexact duplicate; committed983bab96 canonicalGitSHA verified, originaladverseforms preserved. Currentb2cb/f4cores protected. Freshidle/UID/path/inode/allaliasesproof; unchangedfloors.'}, {'kind': 'exact', 'knownPath': 'text-batches-b608-20261112/representative.json', 'bytes': 1244731, 'sha256': '3dd0b7bd7e339ccc5f5c06b22729b3160fa2e197a5715aabada51dbe1364846e', 'canonicalStorage': 'benchmarks/results/2026-11-12-text-output-batches/refined/b608/observations.tgz', 'reason': 'Only terminalexact duplicate; committed983bab96 canonicalGitSHA verified, originaladverseforms preserved. Currentb2cb/f4cores protected. Freshidle/UID/path/inode/allaliasesproof; unchangedfloors.', 'canonicalEntry': 'representative.json', 'canonicalArchiveSHA256': '3439b0eaac3f75a7601e2008ec4b66ea196a842f7c14cb9484359ceb1b27f72f'}, {'kind': 'exact', 'knownPath': 'text-batches-b608-20261112/confirmation/confirmation.json', 'bytes': 938316, 'sha256': '40e53c07225ae23d618a221a49790ce4002cd3842a9cd938b511aeb159fbe45e', 'canonicalStorage': 'benchmarks/results/2026-11-12-text-output-batches/refined/b608/observations.tgz', 'reason': 'Only terminalexact duplicate; committed983bab96 canonicalGitSHA verified, originaladverseforms preserved. Currentb2cb/f4cores protected. Freshidle/UID/path/inode/allaliasesproof; unchangedfloors.', 'canonicalEntry': 'confirmation/confirmation.json', 'canonicalArchiveSHA256': '3439b0eaac3f75a7601e2008ec4b66ea196a842f7c14cb9484359ceb1b27f72f'}], 'identity': {'sourceRevision': '983bab966f13d4575b514eb20dd237a12159bc65; exact568 frozenproductionfiles/coreb2cbfdb6, fullCI43165101428pending. UTF8/UTF16reports,capturedtext,ordinarytextcontrol vsCPython3.13.16. Compile/warm/compile-run separate. No attribution tosinglechange; originaladversity preserved.', 'coreSHA256': {'candidate': 'b2cbfdb6ebec288a356233356e81b72e9dfccdd57065ec53f668acd4ad1597fe', 'candidate-control': 'b2cbfdb6ebec288a356233356e81b72e9dfccdd57065ec53f668acd4ad1597fe'}, 'auxiliaryFiles': {'Azure.Core.dll': '1d6df436afdc73c30d476be938f6513c90ef8fc884a7bae280091a9bed0a5490', 'Azure.Identity.dll': '31574a3d35ffeb0e25a00ebb317937222fe5283b91af608613f2e3bf0f26c50a', 'BenchmarkDotNet.Annotations.dll': '97974c60500f53a3c7d142088b22d815aae7aac729de69f4b497132cc436594f', 'BenchmarkDotNet.dll': '599ded314fe77dbc17318557e787cf660c1fcb0d69be39994ea462ca95745896', 'CommandLine.dll': '8a55501cd1a1590a4bd93a17c6fdd2c01a0ed5bff1aea9036bdc78d98c9a3fad', 'deniszykov.WebSocketListener.dll': 'c86cfb93e32192ae12fb1a132b4ae785a7d2f325e8583c03c99abf8e832d25d6', 'Dia2Lib.dll': '6c4cac68010fe032218efe5e9fcf46eef9f77bfaa5f3bd33f03c5ff77d5a8fac', 'Gee.External.Capstone.dll': '6dfda67784598de9677173b89d0692593935f1a4024d36040c95dea48c1b5965', 'Iced.dll': 'f058c01321b4cdfc9b9151879e88e59571c8d0ba176fbcf95bc936a8f3d073ed', 'Lokad.Lython.Benchmarks.deps.json': 'b47f293f779ce46eefa92c6eb775842efe363be212c35bf7d155d0b89734d450', 'Lokad.Lython.Benchmarks.dll': 'fec79afdb8425f18aa962d5ff9a3e51ae52f519072ea9d9db8ab69a0c52b69a6', 'Lokad.Lython.Benchmarks.runtimeconfig.json': 'c2ecf2430d6785354d9099b35eef9d6ac3d72f2089dc3dc9fd723558265b83d0', 'Lokad.Lython.Utf8Regex.dll': '9f7282ade883f103956efaf7e46182f7705fb7675f1f46963b9c42ae5aae7f94', 'Lokad.Utf8Regex.dll': 'af5fb1e550cf23559bc7e26ef3d895b45e5ef98d57bcf46b414785a6e24cc857', 'Lokad.Utf8Regex.PythonRe.dll': '96e45b20c1db361a9b3b94bd557405b413510801bdb1488ae5b15d6aae7aedfe', 'Microsoft.Bcl.AsyncInterfaces.dll': 'bf58b47923b2f520c766ca905f68a1a65d217c4d828f05db538c41129cacf9d9', 'Microsoft.CodeAnalysis.CSharp.dll': '1af1de8a162d2312eb2f6b781f5edbe8cec7d5cd268c7e4de24396225e54260f', 'Microsoft.CodeAnalysis.dll': '42c9ce7891470f430267e2dc02d03571f9d046a7e7e121107754bee58d344613', 'Microsoft.Diagnostics.FastSerialization.dll': 'e5bdad179ce97180646f37f357cb7b449f5baaf76f185eb09510eeafc5b773e1', 'Microsoft.Diagnostics.NETCore.Client.dll': 'e2cc9e1f6c97b3c4ee45ebabaa92408eb9b2fc81cb08ceeb342fe48bf79db15f', 'Microsoft.Diagnostics.Runtime.dll': '52d5f113180bb0f70b1a9b10608aef93b187364ac689885d2d3c734102d850a2', 'Microsoft.Diagnostics.Tracing.TraceEvent.dll': '92401392ce1887c3483a3c0085e6cc3656ee39da1be539943096ebabbad6ea21', 'Microsoft.Identity.Client.dll': '5795027e9ae95eececb310a4fed3c502bb368039d186ab0884f64b7792b84717', 'Microsoft.Identity.Client.Extensions.Msal.dll': '07109a2f30df782300077a567ade47cbd0a0502c75cd7c8a62192f7d64bb004e', 'Microsoft.IdentityModel.Abstractions.dll': '24e97b54b6b5c234a124a336757e3116617ac6c30b655cdae74517dc2ecb5746', 'Perfolizer.dll': '4a5508174e2b90a651a29e1de0d2ad88236daaaae01070cd6fc8ca3c36beaa36', 'Pragmastat.dll': '7217e206e47de8a0335f58513019d5805a6e52ca2e8f127e2cc3944f1742bb2e', 'System.ClientModel.dll': 'dec5e22b71a1685c5e6e97cd28b629f0aa6703fb87b6950df28e79fc1804290c', 'System.Memory.Data.dll': '2c1035b0106dbb2de7913d151e06071ed617bb58bbb3d6cd46a2dd0f6a03617b', 'System.Security.Cryptography.ProtectedData.dll': '80566f0839ece5946e66bf9f00d723e59e371ba1341a18e00c7c7a7c49298e1d', 'TraceReloggerLib.dll': '6830c0f1d3f3c17719181e73dd5cfa8ccbb3f3c575329d2120576545103467b5'}, 'catalogSHA256': '51f7c6b9f933fee0757d8d31380e8455c4bd2827606d3c3ec2a79b2e3126ff72'}}
inventoryOnly=False
base=Path('/home/lokad/lython-bench-checks').resolve();root=Path(config['root']);source=base/'text-batches-b608-20261112/candidate'
assert root.parent==base and not root.exists()
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={source/'Lokad.Lython.dll':config['identity']['coreSHA256']['candidate'],base/'cell-authority-b606-20261112/candidate/Lokad.Lython.dll':'09ad301e790ce664ed044088f80e8e144c5d502de70f656ccb55eeb235e0e463'}
with(base/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 users=subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[c]omplete-jobs.dll|[p]ython3.*cpython-worker.py'],capture_output=True)
 assert users.returncode in(0,1) and not[line for line in users.stdout.decode().splitlines()if int(line.split()[0])!=os.getpid()]
 for p,h in protected.items():assert digest(p)==h,p
 for name,h in config['identity']['auxiliaryFiles'].items():assert digest(source/name)==h,name
 protocol=base/'prepared-call-layout-b384-20261107/cpython-worker.py'
 assert digest(protocol)=='02ca606af6b937a5bec8da744da36e6dbea45ef14ee7207732f34da052ad32f1'
 before=shutil.disk_usage(base).free;proofs=[];reclaim=0
 if before<=config['requiredFreeBytes']:
  for spec in config['retire']:
   first=base/spec['knownPath'];s=first.lstat()
   assert first.resolve(strict=True)==first and first.is_relative_to(base) and stat.S_ISREG(s.st_mode) and s.st_uid==os.getuid()
   assert s.st_size==spec['bytes']and digest(first)==spec['sha256'],(spec['knownPath'],s.st_size)
   aliases=[]
   for folder,dirs,files in os.walk(base):
    for name in files:
     p=Path(folder)/name;q=p.lstat()
     if(q.st_dev,q.st_ino)!=(s.st_dev,s.st_ino):continue
     assert p.resolve(strict=True)==p and p.is_relative_to(base)and stat.S_ISREG(q.st_mode)and q.st_uid==os.getuid()
     aliases.append(p.relative_to(base).as_posix())
   assert len(aliases)==s.st_nlink and spec['knownPath']in aliases and spec['sha256']not in protected.values()
   assert spec['kind']!='exact'or s.st_nlink==1
   proof=dict(spec);proof.update(paths=aliases,device=s.st_dev,inode=s.st_ino,allocatedBytes=s.st_blocks*512,allAliasesVerified=True)
   proofs.append(proof);reclaim+=s.st_blocks*512
  assert before+reclaim>config['requiredFreeBytes'],(before,reclaim,config['requiredFreeBytes'])
 if inventoryOnly:
  print(json.dumps(dict(freshIdleLease=True,freeBytes=before,requiredFreeBytes=config['requiredFreeBytes'],reclaimableBytes=reclaim,proofs=proofs,protectedImagesUnchanged=True)));sys.exit(0)
 payload=sys.stdin.buffer.read(config['payloadBytes']+1)
 assert len(payload)==config['payloadBytes']and hashlib.sha256(payload).hexdigest()==config['payloadSHA256']
 for proof in proofs:
  for name in proof['paths']:
   p=base/name;s=p.lstat();assert(s.st_dev,s.st_ino)==(proof['device'],proof['inode'])and s.st_size==proof['bytes']and s.st_uid==os.getuid();p.unlink()
 for p,h in protected.items():assert digest(p)==h
 assert shutil.disk_usage(base).free>config['requiredFreeBytes']
 with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz')as t:root.mkdir();t.extractall(root,filter='data')
 (root/'space-recovery.json').write_text(json.dumps(dict(beforeFreeBytes=before,afterExtractionFreeBytes=shutil.disk_usage(base).free,requiredFreeBytes=config['requiredFreeBytes'],files=proofs,canonicalStorage='Exact canonical Git evidence independently verified locally before launch; exact path/bytes/SHA recorded in files',freshIdleLease=True,osFloorMiB=240,evidenceHeadroomMiB=12,protectedImagesUnchanged=True),indent=2)+'\n')
 os.link(source/'Lokad.Lython.dll',root/'current-core.dll')
 shutil.copytree(source,root/'candidate',copy_function=os.link)
 assert digest(root/'candidate/Lokad.Lython.dll')==config['identity']['coreSHA256']['candidate']
 shutil.copyfile(protocol,root/'cpython-worker.py')
 assert digest(root/'representative.json')==config['identity']['catalogSHA256']
 for name in ['collect.py','archive-owned.py']:ast.parse((root/name).read_text())
 subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
 fixture=root/'guest-fixtures';fixture.mkdir()
 subprocess.run(['sudo','-n','mount','-t','tmpfs','-o','size=128m,mode=0700,uid=lokad,gid=lokad','lython-b609-fixtures',str(fixture)],check=True)
 setup=dict(freeBytes=shutil.disk_usage(base).free,sourceAndImagesVerified=True,jobs=12,ownerSeconds=390,stopSeconds=30,marginSeconds=30)
 assert setup['freeBytes']>252*1024*1024
 (root/'setup.json').write_text(json.dumps(setup,indent=2)+'\n');print(json.dumps(setup),flush=True)
try:
 subprocess.run(['sudo','-n','systemd-run','--unit='+config['unit'],'--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=390','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
except BaseException:
 subprocess.run(['sudo','-n','umount',str(fixture)],check=True);fixture.rmdir();raise
live=subprocess.check_output(['systemctl','show',config['unit']+'.service','--property=ActiveState,MainPID,RuntimeMaxUSec'],text=True)
assert'ActiveState=active'in live and'MainPID=0'not in live;print(live,flush=True)
