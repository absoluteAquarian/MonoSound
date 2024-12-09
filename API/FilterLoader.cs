using Microsoft.Xna.Framework.Audio;
using MonoSound.Default;
using MonoSound.Filters;
using System;

namespace MonoSound {
	/// <summary>
	/// The central class for loading filters
	/// </summary>
	public static class FilterLoader {
		/// <summary>
		/// Registers a <see cref="SoLoudFilter"/> object.  The ID can be retrieved via this method's return value or <see cref="SoLoudFilter.ID"/> on the object.
		/// </summary>
		/// <param name="filter">The filter to register</param>
		public static int RegisterFilter(SoLoudFilter filter) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			filter.ID = MonoSoundLibrary.NextFilterID++;
			MonoSoundLibrary.singletonFilters.Add(filter.ID, filter);

			return filter.ID;
		}

		/// <summary>
		/// Gets a registered filter by its ID.  If the ID does not exist, an exception is thrown.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static SoLoudFilter GetRegisteredFilter(int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			return filter;
		}

		/// <summary>
		/// Registers a Biquad Resonant filter.
		/// </summary>
		/// <inheritdoc cref="BiquadResonantFilter(float, int, float, float)"/>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterBiquadResonantFilter(SoundFilterType type, float strength, float frequency, float resonance) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var filterType = type switch {
				SoundFilterType.LowPass => BiquadResonantFilter.LOW_PASS,
				SoundFilterType.BandPass => BiquadResonantFilter.BAND_PASS,
				SoundFilterType.HighPass => BiquadResonantFilter.HIGH_PASS,
				_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", nameof(type))
			};

			BiquadResonantFilter bqf = new(strength, filterType, frequency, resonance) {
				ID = MonoSoundLibrary.NextFilterID++
			};

			MonoSoundLibrary.singletonFilters.Add(bqf.ID, bqf);

			return bqf.ID;
		}

		/// <summary>
		/// Gets the singleton instance assigned to a registered Biquad Resonant filter.<br/>
		/// All new instances created from the parent filter will use the singleton's parameters.
		/// </summary>
		/// <param name="filterID">The registered ID of the filter from calling <see cref="RegisterBiquadResonantFilter"/></param>
		/// <exception cref="ArgumentException"/>
		public static BiquadResonantFilterInstance GetBiquadResonantFilterSingleton(int filterID) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not BiquadResonantFilter bqr)
				throw new ArgumentException($"Filter {filterID} is not a Biquad Resonant filter", nameof(filterID));

			return bqr.Singleton;
		}

		/// <summary>
		/// Registers an Echo filter.
		/// </summary>
		/// <inheritdoc cref="EchoFilter(float, float, float, float)"/>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterEchoFilter(float strength, float delay, float decay, float bias) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			EchoFilter ech = new(strength, delay, decay, bias) {
				ID = MonoSoundLibrary.NextFilterID++
			};

			MonoSoundLibrary.singletonFilters.Add(ech.ID, ech);

			return ech.ID;
		}

		/// <summary>
		/// Gets the singleton instance assigned to a registered Echo filter.<br/>
		/// All new instances created from the parent filter will use the singleton's parameters.
		/// </summary>
		/// <param name="filterID">The registered ID of the filter from calling <see cref="RegisterEchoFilter"/></param>
		/// <exception cref="ArgumentException"/>
		public static EchoFilterInstance GetEchoFilterSingleton(int filterID) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not EchoFilter echo)
				throw new ArgumentException($"Filter {filterID} is not an Echo filter", nameof(filterID));

			return echo.Singleton;
		}

		/// <summary>
		/// Registers a Reverb filter
		/// </summary>
		/// <inheritdoc cref="FreeverbFilter(float, float, float, float)"/>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterReverbFilter(float strength, float feedback, float dampness, float stereoWidth) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			FreeverbFilter rev = new(strength, feedback, dampness, stereoWidth) {
				ID = MonoSoundLibrary.NextFilterID++
			};

			MonoSoundLibrary.singletonFilters.Add(rev.ID, rev);

			return rev.ID;
		}

		/// <summary>
		/// Gets the singleton instance assigned to a registered Reverb filter.<br/>
		/// All new instances created from the parent filter will use the singleton's parameters.
		/// </summary>
		/// <param name="filterID">The registered ID of the filter from calling <see cref="RegisterReverbFilter"/></param>
		/// <exception cref="ArgumentException"/>
		public static FreeverbFilterInstance GetReverbFilterSingleton(int filterID) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not FreeverbFilter rev)
				throw new ArgumentException($"Filter {filterID} is not a Reverb filter", nameof(filterID));

			return rev.Singleton;
		}

		/// <summary>
		/// Updates an already-existing Biquad Resonant filter's parameters.<br/>
		/// If a parameter is <see langword="null"/>, then the corresponding filter parameter will not be updated.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="type">
		/// The type of Biquad Resonant filter to use: Low Pass, High Pass, or Band Pass.<br/>
		/// <i>Low Pass</i> reduces the amplitude of higher frequencies than the set frequency.<br/>
		/// <i>High Pass</i> reduces the amplitude of lower frequencies than the set frequency.<br/>
		/// <i>Band Pass</i> reduces the amplitude of frequencies not within close proximity to the set frequency.
		/// </param>
		/// <param name="strength">The strength of the filter, with a minimum of 0% and a maximum of 100%.</param>
		/// <param name="frequency">
		/// The frequency cutoff for the filter.<br/>
		/// <i>Low Pass</i>: frequencies above this value are attenuated.<br/>
		/// <i>High Pass</i>: frequencies below this value are attenuated.<br/>
		/// <i>Band Pass</i>: frequencies not within close proximity to this value are attenuated.<br/>
		/// Range is 10 to 8000 Hz.
		/// </param>
		/// <param name="resonance">
		/// The resonance of the filter.  Low resonance results in a smoother attenuation and more subtle filtering, whereas high resonance results in more aggressive filtering.<br/>
		/// Range is 0.1 to 20.
		/// </param>
		/// <exception cref="ArgumentException"/>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetBiquadResonantFilterSingleton() to get the instance.", error: true)]
		public static void UpdateBiquadResonantFilter(int filterID, SoundFilterType? type = null, float? strength = null, float? frequency = null, float? resonance = null) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not BiquadResonantFilter bqr)
				throw new ArgumentException($"Filter {filterID} is not a Biquad Resonant filter", nameof(filterID));

			var singleton = bqr.Singleton;

			// Update the parameters
			if (strength is float s)
				singleton.paramStrength.Value = s;

			if (type is SoundFilterType t) {
				var filterType = t switch {
					SoundFilterType.LowPass => BiquadResonantFilter.LOW_PASS,
					SoundFilterType.BandPass => BiquadResonantFilter.BAND_PASS,
					SoundFilterType.HighPass => BiquadResonantFilter.HIGH_PASS,
					_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", nameof(type))
				};

				singleton.paramType.Value = filterType;
			}

			if (frequency is float f)
				singleton.paramFrequency.Value = f;

			if (resonance is float r)
				singleton.paramResonance.Value = r;
		}

		/// <summary>
		/// Updates an already-existing Echo filter's parameters.<br/>
		/// If a parameter is <see langword="null"/>, then the corresponding filter parameter will not be updated.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.</param>
		/// <param name="delay">The delay of the echo in seconds, with a minimum of 0.0001 seconds.  Default is 0.3 seconds.</param>
		/// <param name="decay">The decay factor of the echo, with a minimum of 0x and a maximum of 1x.  Default is 0.7x.</param>
		/// <param name="bias">The influence of earlier samples on the echo, with a minimum of 0% and a maximum of 100%.  Default is 0%.</param>
		/// <exception cref="ArgumentException"/>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetEchoFilterSingleton() to get the instance.", error: true)]
		public static void UpdateEchoFilter(int filterID, float? strength = null, float? delay = null, float? decay = null, float? bias = null) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not EchoFilter echo)
				throw new ArgumentException($"Filter {filterID} is not an Echo filter", nameof(filterID));

			var singleton = echo.Singleton;

			// Update the parameters
			if (strength is float s)
				singleton.paramStrength.Value = s;

			if (delay is float dy) {
				if (dy <= 0)
					throw new ArgumentException("Delay must be positive", nameof(delay));

				singleton.paramDelay.Value = dy;
			}

			if (decay is float dc) {
				if (dc <= 0)
					throw new ArgumentException("Decay factor must be positive", nameof(delay));

				singleton.paramDecay.Value = dc;
			}

			if (bias is float b) {
				if (b < 0 || b >= 1.0f)
					throw new ArgumentException("Filter strength must be zero or a positive number less than one");

				singleton.paramBias.Value = b;
			}
		}

		/// <summary>
		/// Updates an already-existing Reverb filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.</param>
		/// <param name="feedback">
		/// The room size of the reverb, with larger values creating a longer reverb time.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </param>
		/// <param name="dampness">
		/// The damping factor of the reverb.  High damping results in reduced sharpness of the reverb and a more muted sound, whereas low damping results in a sharper and more aggressive reverb.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </param>
		/// <param name="stereoWidth">
		/// The width of the stereo reverb.  A value of 0 is mono reverb, 1 is full stereo reverb.<br/>
		/// The default is 1.
		/// </param>
		/// <exception cref="ArgumentException"></exception>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetReverbFilterSingleton() to get the instance.", error: true)]
		public static void UpdateReverbFilter(int filterID, float? strength = null, float? feedback = null, float? dampness = null, float? stereoWidth = null) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not FreeverbFilter rev)
				throw new ArgumentException($"Filter {filterID} is not a Reverb filter", nameof(filterID));

			var singleton = rev.Singleton;

			// Update the parameters
			if (strength is float s)
				singleton.paramStrength.Value = s;

			if (feedback is float f) {
				if (f < 0 || f > 1)
					throw new ArgumentException("Feedback modifier must be between 0 and 1", nameof(feedback));

				singleton.paramFeedback.Value = f;
			}

			if (dampness is float d) {
				if (d < 0 || d > 1)
					throw new ArgumentException("Dampness modifier must be between 0 and 1", nameof(dampness));

				singleton.paramDampness.Value = d;
			}

			if (stereoWidth is float w) {
				if (w < 0 || w > 1)
					throw new ArgumentException("Stereo width must be between 0 and 1", nameof(stereoWidth));

				singleton.paramStereoWidth.Value = w;
			}
		}
	}
}
