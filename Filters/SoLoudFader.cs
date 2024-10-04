using System;
using System.Numerics;

namespace MonoSound.Filters {
	/// <summary>
	/// A modified port of the Fader class from SoLoud
	/// </summary>
	public sealed class SoLoudFader<T> where T : struct, INumberBase<T>, IComparisonOperators<T, T, bool> {
		/// <summary>
		/// The value to start fading from
		/// </summary>
		public T from;  // "mFrom"

		/// <summary>
		/// The value to fade to
		/// </summary>
		public T to;  // "mTo"

		private double _delta;  // "mDelta"
		private double _duration;  // "mTime"
		private double _startTime;  // "mStartTime"
		private double _currentTime;

		/// <summary>
		/// The most recently calculated value of this fader
		/// </summary>
		public T Current { get; private set; }  // "mCurrent"

		private FaderMode _mode;  // "mActive"

		/// <summary>
		/// Whether the fader should be updated
		/// </summary>
		public bool enabled;

		private bool _expired;

		/// <summary>
		/// The filter instance that owns this fader's target parameter
		/// </summary>
		public readonly SoLoudFilter.Parameter<T> owner;

		internal SoLoudFader(SoLoudFilter.Parameter<T> owner) => this.owner = owner;

		internal SoLoudFader<T> Clone(SoLoudFilter.Parameter<T> newOwner) {
			SoLoudFader<T> clone = new(newOwner);
			clone.CopyFrom(this);
			return clone;
		}

		internal void CopyFrom(SoLoudFader<T> other) {
			from = other.from;
			to = other.to;
			_delta = other._delta;
			_duration = other._duration;
			_startTime = other._startTime;
			_currentTime = other._currentTime;
			Current = other.Current;
			_mode = other._mode;
			enabled = other.enabled;
			_expired = other._expired;
		}

		/// <summary>
		/// Initializes the fader with a linear fade (mode = <see cref="FaderMode.Fade"/>)
		/// </summary>
		/// <param name="to">The value to fade to</param>
		/// <param name="startTime">The time at which the fade starts</param>
		/// <param name="duration">How long it takes to fade from the parameter's current value to <paramref name="to"/></param>
		public void Initialize(T to, double startTime, double duration) {
			// Fader::set()
			_mode = FaderMode.Fade;
			from = owner.Value;
			this.to = to;
			_startTime = startTime;
			_duration = duration;
			_delta = double.CreateChecked(to - from);
			enabled = true;
			_expired = false;
		}

		/// <summary>
		/// Initializes the fader with a low-frequency oscillator fade (mode = <see cref="FaderMode.FadeLFO"/>)
		/// </summary>
		/// <param name="from">The value to oscillate from</param>
		/// <param name="to">The value to oscillate to</param>
		/// <param name="startTime">The time at which the fade starts</param>
		/// <param name="period">The period of the LFO in seconds</param>
		public void InitializeLFO(T from, T to, double startTime, double period) {
			// Fader::setLFO()
			_mode = FaderMode.FadeLFO;
			this.from = from;
			this.to = to;
			_startTime = startTime;
			_delta = double.CreateChecked(T.Abs(to - from)) / 2d;
			_duration = Math.Tau / period;
			enabled = true;
			_expired = false;
		}

		/// <summary>
		/// Resets the fader to an uninitialized state
		/// </summary>
		public void Reset() {
			from = default;
			to = default;
			_delta = 0;
			_duration = 0;
			_startTime = 0;
			Current = default;
			_mode = FaderMode.Inactive;
			enabled = false;
			_expired = false;
		}

		/// <summary>
		/// Updates <see cref="Current"/> and returns whether the fader is currently active
		/// </summary>
		/// <exception cref="InvalidOperationException"/>
		public bool Update(double time) {
			_currentTime = time;

			if (_mode == FaderMode.Inactive) {
				// Fader has not been initialized
				enabled = false;
				return false;
			} else if (_expired) {
				if (time < _startTime) {
					// Time has looped back around, so the fader can be reused
					_expired = false;
					Current = from;
				} else {
					// Fader has finished fading
					enabled = false;
					return false;
				}
			} else if (!enabled)
				return false;  // Fader has been paused, so don't update the value

			if (_currentTime < _startTime)
				return false;  // Fade hasn't started yet, so don't update the value

			// Fader::get()
			switch (_mode) {
				case FaderMode.Fade:
					if (_currentTime >= _startTime + _duration) {
						_currentTime = _startTime + _duration;
						Current = to;
						_expired = true;
						return true;
					}

					Current = from + T.CreateChecked(_delta * (_currentTime - _startTime) / _duration);
					break;
				case FaderMode.FadeLFO:
					Current = T.CreateChecked((Math.Sin((_currentTime - _startTime) * _duration) + 1d) * _delta) + from;
					break;
				default:
					throw new InvalidOperationException("Invalid fader mode: " + _mode);
			}

			return true;
		}
	}

	/// <summary>
	/// An enumeration of all modes for a <see cref="SoLoudFader{T}"/> instance
	/// </summary>
	public enum FaderMode {
		/// <summary>
		/// The fader has not been initialized or has finished fading
		/// </summary>
		Inactive,
		/// <summary>
		/// A linear fade from <see cref="SoLoudFader{T}.from"/> to <see cref="SoLoudFader{T}.to"/>
		/// </summary>
		Fade,
		/// <summary>
		/// A low-frequency oscillator fade from <see cref="SoLoudFader{T}.from"/> to <see cref="SoLoudFader{T}.to"/>
		/// </summary>
		FadeLFO
	}
}
