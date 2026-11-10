using System.Diagnostics.CodeAnalysis;
using System.Text;
using Lokad.Lython;
using Lokad.Lython.Utf8Regex;
using Xunit;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CursorAdmissionTests
{
    [Fact]
    public void NativeRefusalUnwindsWithoutChargingTheProgramOwner()
    {
        var program = new LythonMemoryBudget();
        var working = new LythonMemoryBudget();
        using (var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), program, default))
        {
            var retained = program.ReservedBytes;
            var rejecting = new RefuseGrowthBudget(working);
            using (var subject = pattern.Prepare("a"u8.ToArray(), new(0,1,0,1), rejecting, default))
            {
                var error = Assert.Throws<LythonRegexException>(() =>
                {
                    using var cursor = subject.Find(default);
                    cursor.TryNext(new LythonRegexCapture?[pattern.CaptureSlotCount], out _, default);
                });
                Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
                Assert.True(error.RequestedBytes > 0);
                Assert.True(rejecting.Refused);
                Assert.Equal(0, working.ReservedBytes);
                Assert.Equal(retained, program.ReservedBytes);
            }
            using var next = pattern.Prepare("a"u8.ToArray(), new(0,1,0,1), working, default);
            using var recovery = next.Find(default);
            Assert.True(recovery.TryNext(new LythonRegexCapture?[pattern.CaptureSlotCount], out _, default));
            Assert.False(recovery.TryNext(new LythonRegexCapture?[pattern.CaptureSlotCount], out _, default));
            Assert.Equal(0, working.ReservedBytes);
        }
        Assert.Equal(0, program.ReservedBytes);
    }

    [Fact]
    public void ManyMatchesUseBoundedWorkingCapacityAndCurrentTokens()
    {
        var program = new LythonMemoryBudget();
        var working = new LythonMemoryBudget(256 * 1024);
        var input = Encoding.UTF8.GetBytes(new string('a', 16384));
        using (var inputStorage = AssertReserve(working, input.Length))
        using (var pattern = Utf8RegexProvider.Instance.Compile("(a)"u8.ToArray(), new(32,false), program, default))
        {
            var retained = program.ReservedBytes;
            using var old = new CancellationTokenSource();
            using (var subject = pattern.Prepare(input, new(0,input.Length,0,input.Length), working, default))
            using (var cursor = subject.Find(old.Token))
            {
                old.Cancel();
                var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
                for (var i=0;i<input.Length;i++)
                {
                    Assert.True(cursor.TryNext(captures, out _, default));
                    Assert.Equal(i,captures[0]!.Value.StartByte);
                    Assert.Equal(i+1,captures[1]!.Value.EndByte);
                    Assert.True(working.ReservedBytes > input.Length);
                    Assert.Equal(retained,program.ReservedBytes);
                }
                Assert.False(cursor.TryNext(captures, out _, default));
                Assert.Equal(input.Length,working.ReservedBytes);
            }
        }
        Assert.Equal(0,working.ReservedBytes);
        Assert.Equal(0,program.ReservedBytes);
    }

    private static ILythonMemoryReservation AssertReserve(ILythonMemoryBudget budget,long bytes)
    {
        Assert.True(budget.TryReserve(bytes,out var reservation));
        return reservation;
    }

    private sealed class RefuseGrowthBudget(ILythonMemoryBudget inner) : ILythonMemoryBudget
    {
        public bool Refused;
        public ILythonMemoryScope CreateScope() => new Scope(inner.CreateScope(), this);
        public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? storage)
            => Wrap(inner,bytes,out storage);
        private bool Wrap(ILythonMemoryBudget owner,long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? storage)
        {
            if (!owner.TryReserve(bytes,out var actual)) {storage=null;return false;}
            storage=new Reservation(actual,this);return true;
        }
        private sealed class Scope(ILythonMemoryScope inner,RefuseGrowthBudget root) : ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope()=>new Scope(inner.CreateScope(),root);
            public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? storage)=>root.Wrap(inner,bytes,out storage);
            public void Dispose()=>inner.Dispose();
        }
        private sealed class Reservation(ILythonMemoryReservation inner,RefuseGrowthBudget root) : ILythonMemoryReservation
        {
            public long Bytes=>inner.Bytes;
            public bool TryResize(long bytes)
            {
                if(bytes>Bytes){root.Refused=true;return false;}
                return inner.TryResize(bytes);
            }
            public void Dispose()=>inner.Dispose();
        }
    }
}
