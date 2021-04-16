using System;

namespace CookeRpc.AspNetCore.Utils
{
    public readonly struct Optional<T>
    {
        private readonly T _value;

        public Optional(T value)
        {
            _value = value;
            HasValue = true;
        }

        public T Value => HasValue ? _value : throw new InvalidOperationException("Optional does not have a value ");

        public static readonly Optional<T> Empty = new();

        public bool HasValue { get; }
    }
}