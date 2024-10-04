using System;
using System.Numerics;

namespace MonoSound.DataStructures {
	/// <summary>
	/// A structure that constrains a value to a specific range
	/// </summary>
	/// <typeparam name="T">The underlying type</typeparam>
	public struct ConstrainedValue<T> where T : struct, IComparisonOperators<T, T, bool> {
		private T _value;
		private readonly T _min;
		private readonly T _max;

		/// <summary>
		/// The minimum value.  <see cref="Value"/> for the current instance will never be less than this value
		/// </summary>
		public readonly T MinValue => _min;

		/// <summary>
		/// The maximum value.  <see cref="Value"/> for the current instance will never be greater than this value
		/// </summary>
		public readonly T MaxValue => _max;

		/// <summary>
		/// The current value
		/// </summary>
		public T Value {
			readonly get => _value;
			set => _value = value < _min ? _min : value > _max ? _max : value;
		}

		/// <summary>
		/// Creates a new <see cref="ConstrainedValue{T}"/> with the specified initial value, minimum, and maximum
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public ConstrainedValue(T initialValue, T min, T max) {
			if (min > default(T))
				throw new ArgumentOutOfRangeException(nameof(min), "Minimum value must be less than or equal to the default value of the type");
			if (max < default(T))
				throw new ArgumentOutOfRangeException(nameof(max), "Maximum value must be greater than or equal to the default value of the type");
			if (min > max)
				throw new ArgumentException("Minimum value must be less than or equal to the maximum value");

			_min = min;
			_max = max;
			Value = initialValue;
		}

		/// <summary>
		/// Constrains the given value to the range of this <see cref="ConstrainedValue{T}"/>
		/// </summary>
		public readonly T Constrain(T value) => value < _min ? _min : value > _max ? _max : value;

		/// <summary>
		/// Creates a new <see cref="ConstrainedValue{T}"/> with the specified initial value, but the same minimum and maximum as this <see cref="ConstrainedValue{T}"/>
		/// </summary>
		public readonly ConstrainedValue<T> With(T value) => new ConstrainedValue<T>(value, _min, _max);

		/// <summary>
		/// Converts the given constrained value to its underlying value
		/// </summary>
		public static implicit operator T(ConstrainedValue<T> value) => value.Value;
	}
}
