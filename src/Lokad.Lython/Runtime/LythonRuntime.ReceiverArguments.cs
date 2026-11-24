namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // A borrowed array window, never a new argument array. Compiled calls borrow
    // final executable slots; exposed bound methods borrow builtin object arrays.
    // The caller keeps storage funded until the receiver operation has completed.
    internal readonly struct ReceiverArguments
    {
        private readonly object? _storage;
        private readonly int _start;
        internal int Length { get; }

        // A completed native call can snapshot one evaluated value without
        // manufacturing an argument array or retaining a private span.
        internal static ReceiverArguments Single(object? value) => new(value);

        private ReceiverArguments(object? value)
        {
            _storage = value;
            _start = -1;
            Length = 1;
        }

        internal ReceiverArguments(ExecutableSlot[] storage, int start, int count)
            : this((object)storage, storage.Length, start, count) { }

        private ReceiverArguments(object storage, int capacity, int start, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(checked(start + count), capacity);
            _storage = storage;
            _start = start;
            Length = count;
        }

        internal object? this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length);
                if (_start < 0) return _storage;
                return _storage is ExecutableSlot[] slots ? slots[_start + index].Object
                    : ((object?[])_storage!)[_start + index];
            }
        }

        internal object? Optional(int index) => index < Length ? this[index] : null;

        internal ReceiverArguments Slice(int start, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(checked(start + count), Length);
            if (_start < 0) return count == 0 ? default : this;
            return new(_storage!, checked(_start + Length), checked(_start + start), count);
        }

        public static implicit operator ReceiverArguments(object?[] values)
            => new(values, values.Length, 0, values.Length);
    }
}
