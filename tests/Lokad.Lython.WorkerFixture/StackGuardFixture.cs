using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

// A fatal regression stays inside this owned fixture process. Host I/O really
// yields; a successful async prefix alone does not exercise continuation stacks.
internal static class StackGuardFixture
{
    internal static async Task<int> RunAsync(string libraryPath, string scenario)
    {
        var actual = typeof(LythonEngine).Assembly.Location;
        var libraryHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(actual)));
        if (libraryHash != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(libraryPath))))
            throw new InvalidOperationException("Fixture and requested library bytes differ.");
        const string suspension = "with open('/ready', 'r') as stream:\n stream.read()\n";
        const string lists = "x = 7\ny = 7\nfor _ in range(512):\n x = [x]\n y = [y]\n";
        var body = scenario switch
        {
            "dataclass-hash-cycle" => "from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Node:\n value:object\nfailures=0\nfor _ in range(3):\n node=Node(0)\n node.value=node\n try:hash(node)\n except RecursionError:failures+=1\n node.value=7\n assert hash(node)==hash(Node(7))\nreturn failures\n",
            "dataclass-hash-depth" => "from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Node:\n value:object\nnode=Node(0)\nfor _ in range(1024):node=Node(node)\nfailures=0\nfor _ in range(3):\n try:hash(node)\n except (RecursionError,RuntimeError):failures+=1\n assert hash(Node(7))==hash(Node(7))\nreturn failures\n",
            "function" => "def loop():return loop()\nfailures=0\nfor _ in range(3):\n try:loop()\n except RuntimeError:failures+=1\n assert len([1])==1\nreturn failures\n",
            "function-finally" => "cleanup=0\ndef loop():\n global cleanup\n try:return loop()\n finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:loop()\n except RuntimeError:failures+=1\n assert len([1])==1\nassert cleanup>0\nreturn failures\n",
            "comparison" => lists + "failures = 0\nfor _ in range(3):\n try:\n  assert x == y\n except RecursionError:\n  failures += 1\n assert [[1, 2]] == [[1, 2]]\nreturn failures\n",
            "callback" => "class Loop:\n def __bool__(self): return bool(self)\nfailures = 0\nfor _ in range(3):\n try:\n  bool(Loop())\n except (RuntimeError, RecursionError):\n  failures += 1\n assert bool([1])\nreturn failures\n",
            "copyhook" => "import copy\nclass Loop:\n def __deepcopy__(self, memo): return copy.deepcopy(self, memo)\nfailures = 0\nfor _ in range(3):\n try:\n  copy.deepcopy(Loop())\n except (RuntimeError, RecursionError):\n  failures += 1\n assert copy.deepcopy([1]) == [1]\nreturn failures\n",
            "copyhook-finally" => "import copy\ncleanup=0\nclass Loop:\n def __deepcopy__(self,memo):\n  global cleanup\n  try:return copy.deepcopy(self,memo)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:copy.deepcopy(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert copy.deepcopy([1])==[1]\nassert cleanup>0\nreturn failures\n",
            "copyhook-helper" => "import copy\nclass Loop:\n def __deepcopy__(self,memo):return self.again(memo)\n def again(self,memo):return copy.deepcopy(self,memo)\nfailures=0\nfor _ in range(3):\n try:copy.deepcopy(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert copy.deepcopy([1])==[1]\nreturn failures\n",
            "copyhook-catch" => "import copy\nclass Loop:\n def __deepcopy__(self,memo):\n  try:return copy.deepcopy(self,memo)\n  except (RuntimeError,RecursionError):return 7\ncompleted=0\nfor _ in range(3):\n assert copy.deepcopy(Loop())==7\n assert copy.deepcopy([1])==[1]\n completed+=1\nreturn completed\n",
            "callback-finally" => "cleanup=0\nclass Loop:\n def __bool__(self):\n  global cleanup\n  try:return bool(self)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:bool(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert bool([1])\nassert cleanup>0\nreturn failures\n",
            "length" => "class Loop:\n def __len__(self):return len(self)\nfailures=0\nfor _ in range(3):\n try:len(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert len([1])==1\nreturn failures\n",
            "length-finally" => "cleanup=0\nclass Loop:\n def __len__(self):\n  global cleanup\n  try:return len(self)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:len(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert len([1])==1\nassert cleanup>0\nreturn failures\n",
            "integer-finally" => "cleanup=0\nclass Loop:\n def __int__(self):\n  global cleanup\n  try:return int(self)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:int(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert int('7')==7\nassert cleanup>0\nreturn failures\n",
            "integer-base-finally" => "cleanup=0\nclass Loop:\n def __index__(self):\n  global cleanup\n  try:return int('1',self)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:int('1',Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert int('1',2)==1\nassert cleanup>0\nreturn failures\n",
            "float-finally" => "cleanup=0\nclass Loop:\n def __float__(self):\n  global cleanup\n  try:return float(self)\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:float(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert float('7')==7.0\nassert cleanup>0\nreturn failures\n",
            "float-index-finally" => "cleanup=0\nclass Loop:\n def __index__(self):\n  global cleanup\n  try:return int(float(self))\n  finally:cleanup+=1\nfailures=0\nfor _ in range(3):\n try:float(Loop())\n except (RuntimeError,RecursionError):failures+=1\n assert float('7')==7.0\nassert cleanup>0\nreturn failures\n",
            "cancellation" => lists + "while True:\n try:\n  equal = x == y\n except RecursionError:\n  pass\n",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var script = new LythonEngine().Compile(suspension + body);
        if (!script.IsValid) throw new InvalidOperationException("Stack fixture source did not compile.");
        var host = new DelayedLythonHost();
        host.SeedFile("/ready", "ready");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        if (scenario == "cancellation") host.OnTextReadCompleted = () => cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        var options = new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 2 * 1024 * 1024,
            CancellationToken = scenario == "cancellation" ? cancellation.Token : CancellationToken.None,
        };
        var result = await script.RunAsync(host, options);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            scenario, result.Success,
            value = result.ReturnValue?.ToString(),
            error = result.Failure?.ExceptionType,
            message = result.Failure?.Message,
            completedAsynchronously = host.CompletedAsynchronously,
            libraryHash, processId = Environment.ProcessId,
        }));
        if (host.CompletedAsynchronously == 0) return 2;
        if (scenario == "cancellation")
            return !result.Success && result.Failure?.Message == "execution canceled" ? 0 : 3;
        if (!result.Success) return 4;
        var count = int.Parse(result.ReturnValue!.ToString()!);
        return scenario == "comparison" ? count is >= 0 and <= 3 ? 0 : 5 : count == 3 ? 0 : 6;
    }
}
