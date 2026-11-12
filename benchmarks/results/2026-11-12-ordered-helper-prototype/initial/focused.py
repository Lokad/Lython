from pathlib import Path
import json,os,subprocess,xml.etree.ElementTree as ET
r=Path(__file__).resolve().parent
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['PATH']=env['DOTNET_ROOT']+';C:/Python313;'+env['PATH']
groups={
 'internal':('tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj',['CallBindingSubsystemTests','FunctionDefaultConstructionTests','GeneralBoundMethodDefaultLifetimeTests','SimpleFunctionExecutionBoundaryTests','DescriptorMethodBindingTests','OrdinaryFunctionTransportTests','ClassMethodExecutionBoundaryTests','CallArgumentEvaluationTests','StaticCallArgumentStateTests','NativeTruthEntryTests','ExactIntegerUnaryAdmissionTests','ExecutableStackAdmissionTests','ManagedStackGuardTests','NativeUnaryReentryTests','ExecutableCallFrameTests','NativeFunctionRefundTests','NativeClosureSequenceTests','StorageOwnershipBalanceTests','NestedFrameLifetimeTests','LocalAugmentedLifetimeTests','ExecutableAugmentedSubscriptTests','StringFormattingLifetimeTests','StarredDisplayExecutableTests']),
 'public':('tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj',['FunctionDefaultMetadataTests','FunctionBindingLifetimeTests','NamedFunctionBindingTests','FunctionAttributeDeletionTests','NativeCallbackArgumentsTests','NativeReceiverOperationTests','ClassMethodCompatibilityTests','FunctionDefinitionIsolationTests','ConstructorLifetimeTests','BindingPlanConstructionTests','LambdaExecutionTests','NativeReturnFunctionTests','NativeTruthEntryTests','NativeTruthLifetimeTests','NativeTruthinessTests','TruthTypeSlotCompatibilityTests','TruthTypeSlotDelayedTests','ExactIntegerUnaryTests','HelperContinuationLifetimeTests','AsyncFunctionHostingTests','AsyncMidFlightCancellationTests','CooperativeCancellationTests','RepresentativeProtocolCancellationTests','GeneratorCleanupCompositionTests','GeneratorExpressionBoundaryLifetimeTests','FunctionWrapperContainmentTests','FunctionWorkspaceCapacityTests','FunctionValueAccountingScenarioTests'])}
checks={}
for framework in ['net11.0','net10.0']:
 for label,(project,names)in groups.items():
  name='final-'+label+'-'+framework
  filter='('+'|'.join('FullyQualifiedName~'+s for s in names)+')&Category!=Stress'
  with(r/(name+'.log')).open('wb')as f:
   done=subprocess.run([env['DOTNET_ROOT']+'/dotnet.exe','test',project,'-c','Release','-f',framework,'--no-build','--filter',filter,'--nologo','--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')],env=env,stdout=f,stderr=subprocess.STDOUT)
  p=r/'tests'/(name+'.trx');c=ET.parse(p).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
  checks[name]=c;(r/'focused-checks.json').write_text(json.dumps(checks,indent=2)+'\n',encoding='utf-8')
  print(name,c['passed'],'pass',c['failed'],'failed',flush=True)
  assert done.returncode==0,(name,(r/(name+'.log')).read_text(encoding='utf-8'))
