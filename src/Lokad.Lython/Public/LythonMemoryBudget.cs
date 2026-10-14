using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython;

/// <summary>A standalone shared allowance with coarse ownership scopes and reservations.</summary>
/// <remarks>
/// Estimates are not CLR heap or process-RSS measurements. Keep headroom for minor
/// uninstrumented storage, runtime/native memory and delayed physical collection.
/// Admission is immediate. Scopes share the root limit; no available balance is
/// copied. Dispose owned lifetimes only after their users and cleanup have finished.
/// </remarks>
public sealed class LythonMemoryBudget : ILythonMemoryBudget
{
    private readonly State _state;
    private readonly Scope _root;

    /// <summary>Creates a shared allowance; null leaves it unbounded.</summary>
    public LythonMemoryBudget(long? limitBytes = null)
    {
        if (limitBytes is < 0) throw new ArgumentOutOfRangeException(nameof(limitBytes));
        _state = new State(limitBytes);
        _root = new Scope(_state, null);
    }

    /// <summary>Gets the root's configured limit, or null for an unbounded allowance.</summary>
    public long? LimitBytes => _state.Limit;
    /// <summary>Gets the current estimates retained by live reservations.</summary>
    public long ReservedBytes { get { lock (_state.Gate) return _state.Reserved; } }
    /// <summary>Gets the largest simultaneous total of admitted estimates.</summary>
    public long PeakBytes { get { lock (_state.Gate) return _state.Peak; } }
    /// <inheritdoc />
    public ILythonMemoryScope CreateScope() => _root.CreateScope();
    /// <inheritdoc />
    public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => _root.TryReserve(bytes, out reservation);

    private sealed class State(long? limit)
    {
        internal readonly object Gate = new();
        internal readonly long? Limit = limit;
        internal long Reserved;
        internal long Peak;
        internal bool Admit(long growth)
        {
            if (growth > (Limit ?? long.MaxValue) - Reserved) return false;
            Reserved += growth;
            Peak = Math.Max(Peak, Reserved);
            return true;
        }
    }

    private sealed class Scope(State state, Scope? parent) : ILythonMemoryScope
    {
        internal readonly State State = state;
        private readonly Scope? _parent = parent;
        private Scope? _firstChild;
        private Scope? _next;
        private Scope? _previous;
        internal Reservation? FirstReservation;
        private bool _disposed;

        internal void CheckAlive() => ObjectDisposedException.ThrowIf(_disposed, this);

        public ILythonMemoryScope CreateScope()
        {
            lock (State.Gate)
            {
                CheckAlive();
                var child = new Scope(State, this) { _next = _firstChild };
                if (_firstChild is not null) _firstChild._previous = child;
                _firstChild = child;
                return child;
            }
        }

        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);
            lock (State.Gate)
            {
                CheckAlive();
                reservation = null;
                if (!State.Admit(bytes)) return false;
                try
                {
                    var owned = new Reservation(this, bytes) { Next = FirstReservation };
                    if (FirstReservation is not null) FirstReservation.Previous = owned;
                    FirstReservation = owned;
                    reservation = owned;
                    return true;
                }
                catch { State.Reserved -= bytes; throw; }
            }
        }

        public void Dispose()
        {
            lock (State.Gate)
            {
                if (_disposed) return;
                // Walk existing parent/sibling links: disposal allocates no stack,
                // runs no guest callbacks and does not recurse on the CLR stack.
                var current = this;
                while (true)
                {
                    if (current._firstChild is { } child) { current = child; continue; }
                    while (current.FirstReservation is { } reservation) reservation.Release();
                    current._disposed = true;
                    var previousOwner = current._parent;
                    current.Detach();
                    if (ReferenceEquals(current, this)) return;
                    current = previousOwner!;
                }
            }
        }

        private void Detach()
        {
            if (_previous is null)
            {
                if (_parent is not null) _parent._firstChild = _next;
            }
            else _previous._next = _next;
            if (_next is not null) _next._previous = _previous;
            _next = null;
            _previous = null;
        }
    }

    private sealed class Reservation(Scope owner, long bytes) : ILythonMemoryReservation
    {
        private readonly State _state = owner.State;
        private Scope? _owner = owner;
        private long _bytes = bytes;
        internal Reservation? Next;
        internal Reservation? Previous;
        public long Bytes { get { lock (_state.Gate) return _bytes; } }

        public bool TryResize(long bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);
            lock (_state.Gate)
            {
                ObjectDisposedException.ThrowIf(_owner is null, this);
                var delta = bytes - _bytes;
                if (delta > 0 && !_state.Admit(delta)) return false;
                if (delta < 0) _state.Reserved += delta;
                _bytes = bytes;
                return true;
            }
        }

        public void Dispose() { lock (_state.Gate) Release(); }

        internal void Release()
        {
            if (_owner is null) return;
            _state.Reserved -= _bytes;
            _bytes = 0;
            if (Previous is null) _owner.FirstReservation = Next;
            else Previous.Next = Next;
            if (Next is not null) Next.Previous = Previous;
            Next = null;
            Previous = null;
            _owner = null;
        }
    }
}
