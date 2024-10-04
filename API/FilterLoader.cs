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
		/// <param name="type">The filter type to use. Must either be <see cref="SoundFilterType.LowPass"/>, <see cref="SoundFilterType.BandPass"/> or <see cref="SoundFilterType.HighPass"/></param>
		/// <param name="strength">How strong the filter effect is. 0 = no effect, 1 = full effect</param>
		/// <param name="frequencyCap">The frequency parameter. Expected values are between 1000 and 8000</param>
		/// <param name="resonance">The resonance parameter. Expected values are between 2 and 20</param>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterBiquadResonantFilter(SoundFilterType type, float strength, float frequencyCap, float resonance) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var filterType = type switch {
				SoundFilterType.LowPass => BiquadResonantFilter.LOW_PASS,
				SoundFilterType.BandPass => BiquadResonantFilter.BAND_PASS,
				SoundFilterType.HighPass => BiquadResonantFilter.HIGH_PASS,
				_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", nameof(type))
			};

			BiquadResonantFilter bqf = new(strength, filterType, frequencyCap, resonance) {
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
		/// <param name="strength">How strong the filter effect is. 0 = no effect, 1 = full effect</param>
		/// <param name="delay">The initial delay in seconds before each echo starts</param>
		/// <param name="decayFactor">The factor applied to the volume of each successive echo.  Expected values are between 0 and 1</param>
		/// <param name="filterStrength">How strongly this filter will prefer using old samples over new samples when processing the sound.  Expected values are between 0 (no effect) and 1 (full effect)</param>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterEchoFilter(float strength, float delay, float decayFactor, float filterStrength) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			EchoFilter ech = new(strength, delay, decayFactor, filterStrength) {
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
		/// <param name="strength">
		/// <seealso cref="SoLoudFilterInstance.paramStrength"/>
		/// </param>
		/// <param name="feedback">How much the filter affects low frequencies. 0 = fast decaying, 1 = slow decaying. Defaults to 0.5</param>
		/// <param name="dampness">How much the filter affects high frequencies. 0 = fast decaying, 1 = slow decaying. Defaults to 0.5</param>
		/// <param name="stereoWidth">How strong the reverb effect is. Expected values are between 0 and 1. Defaults to 1</param>
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
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="type">The new type.  If not <see langword="null"/>, it must be <see cref="SoundFilterType.LowPass"/>, <see cref="SoundFilterType.BandPass"/> or <see cref="SoundFilterType.HighPass"/></param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="frequencyCap">The new frequency parameter.  If not <see langword="null"/>, it is expected to be between 1000 and 8000.</param>
		/// <param name="resonance">The new resonance.  If not <see langword="null"/>, it is expected to be between 2 and 20.</param>
		/// <exception cref="ArgumentException"/>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetBiquadResonantFilterSingleton() to get the instance.", error: true)]
		public static void UpdateBiquadResonantFilter(int filterID, SoundFilterType? type = null, float? strength = null, float? frequencyCap = null, float? resonance = null) {
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

			if (frequencyCap is float f)
				singleton.paramFrequency.Value = f;

			if (resonance is float r)
				singleton.paramResonance.Value = r;
		}

		/// <summary>
		/// Updates an already-existing Echo filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="delay">The new delay in seconds.  If not <see langword="null"/>, it must be greater than zero.</param>
		/// <param name="decayFactor">The new decay factor applied to successive echoes.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="filterStrength">The new preference strength for old samples.  If not <see langword="null"/>, it must be greater than or equal to zero and less than one.</param>
		/// <exception cref="ArgumentException"/>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetEchoFilterSingleton() to get the instance.", error: true)]
		public static void UpdateEchoFilter(int filterID, float? strength = null, float? delay = null, float? decayFactor = null, float? filterStrength = null) {
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

			if (decayFactor is float dc) {
				if (dc <= 0)
					throw new ArgumentException("Decay factor must be positive", nameof(delay));

				singleton.paramDecay.Value = dc;
			}

			if (filterStrength is float f) {
				if (f < 0 || f >= 1.0f)
					throw new ArgumentException("Filter strength must be zero or a positive number less than one");

				singleton.paramBias.Value = f;
			}
		}

		/// <summary>
		/// Updates an already-existing Reverb filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="lowFrequencyReverbStrength">The new low frequency modifier strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="highFrequencyReverbStrength">The new high frequency modifier strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <param name="reverbStrength">The new reverb modifier strength.  If not <see langword="null"/>, it must be between 0 and 1.</param>
		/// <exception cref="ArgumentException"></exception>
		[Obsolete("Parameters should be accessed via the filter's singleton instead.  Call GetReverbFilterSingleton() to get the instance.", error: true)]
		public static void UpdateReverbFilter(int filterID, float? strength = null, float? lowFrequencyReverbStrength = null, float? highFrequencyReverbStrength = null, float? reverbStrength = null) {
			if (!MonoSoundLibrary.singletonFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (filter is not FreeverbFilter rev)
				throw new ArgumentException($"Filter {filterID} is not a Reverb filter", nameof(filterID));

			var singleton = rev.Singleton;

			// Update the parameters
			if (strength is float s)
				singleton.paramStrength.Value = s;

			if (lowFrequencyReverbStrength is float lf) {
				if (lf < 0 || lf > 1)
					throw new ArgumentException("Low frequency modifier must be between 0 and 1", nameof(lowFrequencyReverbStrength));

				singleton.paramFeeback.Value = lf;
			}

			if (highFrequencyReverbStrength is float hf) {
				if (hf < 0 || hf > 1)
					throw new ArgumentException("High frequency modifier must be between 0 and 1", nameof(highFrequencyReverbStrength));

				singleton.paramDampness.Value = hf;
			}

			if (reverbStrength is float r) {
				if (r < 0 || r > 1)
					throw new ArgumentException("Reverb modifier strength must be between 0 and 1", nameof(reverbStrength));

				singleton.paramStereoWidth.Value = r;
			}
		}
	}
}
