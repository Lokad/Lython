namespace Lokad.Lython.Runtime;

/// <summary>Represents either one present value or its explicit absence.</summary>
internal readonly struct OptionalValue<T>
{
    private readonly T _value;

    private OptionalValue(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static OptionalValue<T> Missing => default;

    public static OptionalValue<T> Present(T value) => new(value);

    public bool HasValue { get; }

    public T Value => HasValue
        ? _value
        : throw new InvalidOperationException("A missing optional value has no value.");
}
