using MonoSound.DataStructures;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MonoSound.Filters {
	/// <summary>
	/// A port of the Filter class from SoLoud
	/// </summary>
	public abstract class SoLoudFilter {
		/// <summary>
		/// An object representing a parameter for a filter instance
		/// </summary>
		public abstract class Parameter : IDisposable {
			/// <summary>
			/// The index of the parameter
			/// </summary>
			public readonly int ID;

			/// <summary>
			/// Whether the parameter is valid.  If this is <see langword="false"/>, the parameter has been disposed.
			/// </summary>
			public bool Valid { get; private set; } = true;

			/// <summary>
			/// The filter instance that owns this parameter
			/// </summary>
			public readonly SoLoudFilterInstance owner;

			/// <summary>
			/// Creates a new <see cref="Parameter"/> instance
			/// </summary>
			protected Parameter(SoLoudFilterInstance owner) {
				this.owner = owner;
				ID = owner.ReserveParameterIndex();
			}

			/// <summary>
			/// Copy any values from <paramref name="other"/> to this parameter here
			/// </summary>
			/// <param name="other">The other parameter instance</param>
			protected abstract void CopyFrom(Parameter other);

			/// <summary>
			/// Copies from this parameter to <paramref name="other"/>
			/// </summary>
			/// <param name="other">The other parameter instance</param>
			/// <exception cref="ObjectDisposedException"/>
			public void CopyTo(Parameter other) {
				if (!Valid)
					throw new ObjectDisposedException(nameof(Parameter));
				if (!other.Valid)
					throw new ObjectDisposedException(nameof(other));

				other.CopyFrom(this);
			}

			/// <inheritdoc cref="IDisposable.Dispose"/>
			public void Dispose() {
				Valid = false;
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			/// <inheritdoc cref="IDisposable.Dispose"/>
			protected virtual void Dispose(bool disposing) { }

			/// <summary/>
			~Parameter() {
				Valid = false;
				Dispose(false);
			}
		}

		/// <summary/>
		public abstract class __GenericParameter(SoLoudFilterInstance owner) : Parameter(owner) {
			/// <summary>
			/// Returns the value assigned to this parameter as an object
			/// </summary>
			public abstract object GetBoxedValue();

			/// <summary>
			/// Sets the value of this parameter from a boxed object
			/// </summary>
			/// <param name="value">The boxed value</param>
			public abstract void SetValueFromBoxed(object value);
		}

		/// <inheritdoc cref="Parameter"/>
		/// <typeparam name="T">The type of the underlying value</typeparam>
		public sealed class Parameter<T> : __GenericParameter where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
			private ConstrainedValue<T> _constraint;

			/// <summary>
			/// The value for the current instance
			/// </summary>
			public T Value {
				get => Valid ? _constraint.Value : throw new ObjectDisposedException(nameof(Parameter<T>));
				set {
					T old = _constraint.Value;
					_constraint.Value = value;

					Fader.Reset();  // Assignment stops the fader if it was active

					if (old != _constraint.Value)
						owner.MarkChanged(this);
				}
			}

			/// <summary>
			/// The minimum allowed value for this parameter
			/// </summary>
			public T MinValue => Valid ? _constraint.MinValue : throw new ObjectDisposedException(nameof(Parameter<T>));

			/// <summary>
			/// The maximum allowed value for this parameter
			/// </summary>
			public T MaxValue => Valid ? _constraint.MaxValue : throw new ObjectDisposedException(nameof(Parameter<T>));

			private SoLoudFader<T> _fader;
			/// <summary>
			/// The fader for the current instance
			/// </summary>
			public SoLoudFader<T> Fader {
				get => Valid ? _fader : throw new ObjectDisposedException(nameof(Parameter<T>));
				private set => _fader = value;
			}

			internal Parameter(SoLoudFilterInstance owner, T initialValue, T min, T max) : base(owner) {
				_constraint = new(initialValue, min, max);
				Fader = new(this);
			}

			internal void UpdateFader(double time) {
				if (!Valid)
					throw new ObjectDisposedException(nameof(Parameter<T>));

				// Ensure that the the parameter's fader is actually owned by the current filter instance
				if (!object.ReferenceEquals(this, Fader.owner))
					Fader = Fader.Clone(newOwner: this);

				if (Fader.Update(time)) {
					_constraint.Value = Fader.Current;
					owner.MarkChanged(this);
				}
			}

			/// <inheritdoc cref="__GenericParameter.GetBoxedValue"/>
			public override object GetBoxedValue() => Value;

			/// <inheritdoc cref="__GenericParameter.SetValueFromBoxed"/>
			public override void SetValueFromBoxed(object value) {
				if (value is not T val)
					throw new ArgumentException("Value type mismatch, could not set value to provided object", nameof(value));

				Value = val;
			}

			/// <summary>
			/// Copies the value and clones the fader from another parameter
			/// </summary>
			protected override void CopyFrom(Parameter other) {
				if (other is not Parameter<T> otherAs)
					throw new ArgumentException("Parameter type mismatch, could not copy from provided parameter", nameof(other));

				_constraint = otherAs._constraint;
				Fader.CopyFrom(otherAs.Fader);
			}

			/// <inheritdoc cref="Parameter.Dispose(bool)"/>
			protected override void Dispose(bool disposing) {
				Fader = null;
			}

			/// <summary>
			/// Converts the parameter to its underlying value
			/// </summary>
			public static implicit operator T(Parameter<T> parameter) => parameter.Value;
		}

		/// <summary>
		/// A parameter for a filter instance that is a boolean value
		/// </summary>
		public sealed class BoolParameter : Parameter {
			private bool _value;
			/// <summary>
			/// The value for the current instance
			/// </summary>
			public bool Value {
				get => Valid ? _value : throw new ObjectDisposedException(nameof(BoolParameter));
				set {
					bool old = _value;
					_value = value;

					if (old != _value)
						owner.MarkChanged(this);
				}
			}

			internal BoolParameter(SoLoudFilterInstance owner, bool initialValue) : base(owner) => _value = initialValue;

			/// <inheritdoc cref="Parameter.CopyFrom"/>
			protected override void CopyFrom(Parameter other) {
				if (other is not BoolParameter otherAs)
					throw new ArgumentException("Parameter type mismatch, could not copy from provided parameter", nameof(other));

				_value = otherAs._value;
			}

			/// <summary>
			/// Converts the parameter to its underlying value
			/// </summary>
			public static implicit operator bool(BoolParameter parameter) => parameter.Value;
		}

		/// <summary>
		/// The reserved ID of this filter for use by legacy <see cref="EffectLoader"/> methods and indirectly accessing the <see cref="Singleton"/> instance.<br/>
		/// This property will be <c>-1</c> if the filter was not registered with <see cref="FilterLoader"/>.
		/// </summary>
		public int ID { get; internal set; } = -1;

		/// <summary>
		/// The singleton instance of this filter.  This instance is where the default parameters for the filter are stored.
		/// </summary>
		public SoLoudFilterInstance Singleton { get; }

		/// <summary>
		/// Creates a new <see cref="SoLoudFilter"/> instance
		/// </summary>
		/// <param name="strength">The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.</param>
		protected SoLoudFilter(float strength) {
			Singleton = NewInstance();
			Singleton.IsSingleton = true;
			Singleton.paramStrength.Value = strength;
		}

		/// <summary>
		/// Creates an instanced version of this filter
		/// </summary>
		protected abstract SoLoudFilterInstance NewInstance();

		/// <inheritdoc cref="NewInstance"/>
		public SoLoudFilterInstance CreateInstance() {
			SoLoudFilterInstance instance = NewInstance();
			Singleton.CopyParametersTo(instance);
			instance.ResetFilterState();
			return instance;
		}
	}

	/// <inheritdoc cref="SoLoudFilter"/>
	/// <typeparam name="T">The instance type associated with this filter</typeparam>
	public abstract class SoLoudFilter<T>(float strength) : SoLoudFilter(strength) where T : SoLoudFilterInstance {
		/// <inheritdoc cref="SoLoudFilter.Singleton"/>
		public new T Singleton => (T)base.Singleton;

		/// <inheritdoc cref="SoLoudFilter.CreateInstance"/>
		public new T CreateInstance() => (T)base.CreateInstance();
	}

	/// <summary>
	/// An instance of a <see cref="SoLoudFilter"/>
	/// </summary>
	public abstract class SoLoudFilterInstance : IDisposable {
		private int _nextID;
		private ulong _changed;

		private readonly SoLoudFilter.Parameter[] _parameters = new SoLoudFilter.Parameter[64];

		/// <summary>
		/// The filter that created this instance
		/// </summary>
		public SoLoudFilter Parent { get; internal set; }

		/// <summary>
		/// Whether this filter instance is the singleton tied to the parent filter
		/// </summary>
		public bool IsSingleton { get; internal set; }

		/// <summary>
		/// Whether any parameters have changed.  Reading this property will reset the flags.
		/// </summary>
		public bool HasAnyParameterChanged => Interlocked.Exchange(ref _changed, 0) != 0uL;

		/// <summary>
		/// The number of parameters in this filter instance
		/// </summary>
		public int ParameterCount => _nextID;

		/// <summary>
		/// The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramStrength;

		/// <summary>
		/// Creates a new <see cref="SoLoudFilterInstance"/> instance with the specified parent filter
		/// </summary>
		/// <param name="parent"></param>
		protected internal SoLoudFilterInstance(SoLoudFilter parent) {
			Parent = parent;
			paramStrength = CreateParameter(1f, 0f, 1f);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int ReserveParameterIndex() => _nextID++;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void MarkChanged(in SoLoudFilter.Parameter parameter) => _changed |= 1uL << parameter.ID;

		/// <summary>
		/// Creates a new filter parameter
		/// </summary>
		/// <typeparam name="T">The type of the parameter</typeparam>
		/// <param name="initialValue">The initial value for the parameter</param>
		/// <param name="min">The minimum possible value</param>
		/// <param name="max">The maximum possible value</param>
		protected SoLoudFilter.Parameter<T> CreateParameter<T>(T initialValue, T min, T max) where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
			var parameter = new SoLoudFilter.Parameter<T>(this, initialValue, min, max);
			_parameters[parameter.ID] = parameter;
			return parameter;
		}

		/// <inheritdoc cref="CreateParameter{T}(T, T, T)"/>
		protected SoLoudFilter.BoolParameter CreateParameter(bool initialValue) {
			var parameter = new SoLoudFilter.BoolParameter(this, initialValue);
			_parameters[parameter.ID] = parameter;
			return parameter;
		}

		/// <summary>
		/// Gets whether the parameter with the specified ID is a <see cref="SoLoudFilter.Parameter{T}"/>
		/// </summary>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		public bool IsGenericParameter(int parameterID) {
			if (parameterID < 0 || parameterID >= _nextID)
				throw new ArgumentOutOfRangeException(nameof(parameterID), parameterID, "Parameter ID was out of range");

			return _parameters[parameterID] is SoLoudFilter.__GenericParameter;
		}

		/// <summary>
		/// Gets the value of a <see cref="SoLoudFilter.Parameter{T}"/>.<br/>
		/// If the parameter ID is out of range or the parameter's underlying value was not a <typeparamref name="T"/>, an exception will be thrown.
		/// </summary>
		/// <typeparam name="T">The type of the parameter's underlying value</typeparam>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		/// <returns>The underlying value for the parameter</returns>
		public T GetParameter<T>(int parameterID) where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
			if (!IsGenericParameter(parameterID))
				throw new ArgumentException($"Parameter of ID {parameterID} was not a generic parameter, cannot retrieve value with this method", nameof(parameterID));

			if (_parameters[parameterID] is not SoLoudFilter.Parameter<T> parameter)
				throw new ArgumentException($"Parameter of ID {parameterID} was not a {typeof(T).Name}", nameof(parameterID));

			return parameter.Value;
		}

		/// <summary>
		/// Gets the value of a parameter as a boxed object.<br/>
		/// If the parameter ID is out of range or the parameter's type was not a known type, an exception will be thrown.
		/// </summary>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public object GetParameter(int parameterID) {
			if (parameterID < 0 || parameterID >= _nextID)
				throw new ArgumentOutOfRangeException(nameof(parameterID), parameterID, "Parameter ID was out of range");

			var parameter = _parameters[parameterID];

			if (parameter is SoLoudFilter.__GenericParameter gen)
				return gen.GetBoxedValue();
			else if (parameter is SoLoudFilter.BoolParameter boolean)
				return boolean.Value;

			throw new ArgumentException($"Parameter of ID {parameterID} could not be resolved to a known type", nameof(parameterID));
		}

		/// <summary>
		/// Attempts to set the value of a <see cref="SoLoudFilter.Parameter{T}"/> to the specified value.<br/>
		/// The value will be constrained to the parameter's minimum and maximum values.<br/>
		/// If the parameter ID is out of range or the parameter's underlying value was not a <typeparamref name="T"/>, an exception will be thrown.
		/// </summary>
		/// <typeparam name="T">The type of the parameter's underlying value</typeparam>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <param name="value">The value to set the parameter to</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public void SetParameter<T>(int parameterID, in T value) where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
			if (!IsGenericParameter(parameterID))
				throw new ArgumentException($"Parameter of ID {parameterID} was not a generic parameter, cannot set value with this method", nameof(parameterID));

			if (_parameters[parameterID] is not SoLoudFilter.Parameter<T> parameter)
				throw new ArgumentException($"Parameter of ID {parameterID} was not a {typeof(T).Name}", nameof(parameterID));

			parameter.Value = value;
		}

		/// <summary>
		/// Attempts to set the value of a parameter to the specified value.<br/>
		/// The value will be constrained to the parameter's minimum and maximum values if it is a <see cref="SoLoudFilter.Parameter{T}"/>.<br/>
		/// If the parameter ID is out of range or the parameter's type was not a known type, an exception will be thrown.
		/// </summary>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <param name="value">The value to set the parameter to</param>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public void SetParameter(int parameterID, object value) {
			if (parameterID < 0 || parameterID >= _nextID)
				throw new ArgumentOutOfRangeException(nameof(parameterID), parameterID, "Parameter ID was out of range");

			var parameter = _parameters[parameterID];

			if (parameter is SoLoudFilter.__GenericParameter gen)
				gen.SetValueFromBoxed(value);
			else if (parameter is SoLoudFilter.BoolParameter boolean)
				boolean.Value = value is bool val ? val : throw new ArgumentException("Value type mismatch, could not set value to provided object", nameof(value));
			else
				throw new ArgumentException($"Parameter of ID {parameterID} could not be resolved to a known type", nameof(parameterID));
		}

		/// <summary>
		/// Gets the fader for a <see cref="SoLoudFilter.Parameter{T}"/>.<br/>
		/// If the parameter ID is out of range or the parameter's underlying value was not a <typeparamref name="T"/>, an exception will be thrown.
		/// </summary>
		/// <typeparam name="T">The type of the parameter's underlying value</typeparam>
		/// <param name="parameterID">The ID of the parameter</param>
		/// <returns>The fader assigned to the parameter</returns>
		/// <exception cref="ArgumentOutOfRangeException"/>
		/// <exception cref="ArgumentException"/>
		public SoLoudFader<T> GetParameterFader<T>(int parameterID) where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
			if (!IsGenericParameter(parameterID))
				throw new ArgumentException($"Parameter of ID {parameterID} was not a generic parameter, cannot retrieve fader with this method", nameof(parameterID));

			if (_parameters[parameterID] is not SoLoudFilter.Parameter<T> parameter)
				throw new ArgumentException($"Parameter of ID {parameterID} was not a {typeof(T).Name}", nameof(parameterID));

			return parameter.Fader;
		}

		/// <summary>
		/// Update the faders within this filter instance here.  By default, updates the strength parameter's fader.
		/// </summary>
		/// <param name="time">The current time in seconds</param>
		protected internal virtual void UpdateParameterFaders(double time) => paramStrength.UpdateFader(time);

		/// <summary>
		/// Copy the parameters from this filter instance to another instance here
		/// </summary>
		protected internal virtual void CopyParametersTo(SoLoudFilterInstance other) => paramStrength.CopyTo(other.paramStrength);

		/// <summary>
		/// Runs before filtering starts.  Use this method to initialize any variables needed for filtering.
		/// </summary>
		/// <param name="channelCount">The number of channels in the audio</param>
		/// <param name="channelSize">The number of samples in the audio for one channel</param>
		/// <param name="sampleRate">The sample rate of the audio</param>
		protected internal virtual void BeginFiltering(int channelCount, int channelSize, int sampleRate) { }

		/// <summary>
		/// Apply the filter to each channel in the sample data here.  By default, this method splits the samples into per-channel segments and applies the filter to each channel individually.<br/>
		/// Default slice implementation:<br/>
		/// <c>ApplyFilter(<paramref name="uninterleavedSamples"/>.Slice(channel * <paramref name="channelSize"/> + <paramref name="offset"/>, <paramref name="sampleCount"/>), channel, <paramref name="sampleRate"/>)</c><br/>
		/// where <c>channel</c> is the loop iteration variable
		/// </summary>
		/// <param name="uninterleavedSamples">The full sample data</param>
		/// <param name="offset">The offset into the per-channel sample data to start the slice at</param>
		/// <param name="sampleCount">How large the sample slice should be</param>
		/// <param name="channelCount">The number of channels in the audio</param>
		/// <param name="channelSize">Shorthand for: <paramref name="uninterleavedSamples"/>.Length / <paramref name="channelCount"/></param>
		/// <param name="sampleRate">The sample rate of the audio</param>
		protected internal virtual void ApplyFilteringToAllChannels(Span<float> uninterleavedSamples, int offset, int sampleCount, int channelCount, int channelSize, int sampleRate) {
			for (int c = 0; c < channelCount; c++)
				ApplyFilter(uninterleavedSamples.Slice(c * channelSize + offset, sampleCount), c, sampleRate);
		}

		/// <summary>
		/// Modify the sample data for the specified channel here
		/// </summary>
		/// <param name="samples">The sample data to modify</param>
		/// <param name="channel">
		/// Which channel's samples are being modified.<br/>
		/// Always 0 for Mono audio.<br/>
		/// 0 or 1 for Stereo audio.
		/// </param>
		/// <param name="sampleRate">The sample rate of the audio</param>
		protected internal abstract void ApplyFilter(Span<float> samples, int channel, int sampleRate);

		/// <summary>
		/// Used to reset the filter singleton before applying it to a new audio stream.  Reset or reinitialize any variables here.
		/// </summary>
		protected internal abstract void ResetFilterState();

		/// <inheritdoc cref="IDisposable.Dispose"/>
		public void Dispose() {
			Dispose(true);
			paramStrength.Dispose();
			Parent = null;
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc cref="IDisposable.Dispose"/>
		/// <param name="disposing">Whether managed resources should be disposed</param>
		protected virtual void Dispose(bool disposing) { }

		/// <summary/>
		~SoLoudFilterInstance() {
			Dispose(false);
			Parent = null;
		}
	}
}
