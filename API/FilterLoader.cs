using Microsoft.Xna.Framework.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;

namespace MonoSound {
	/// <summary>
	/// The central class for loading filters
	/// </summary>
	public static class FilterLoader {
		/// <summary>
		/// Registers a Biquad Resonant filter.
		/// </summary>
		/// <param name="type">The filter type to use. Must either be <seealso cref="SoundFilterType.LowPass"/>, <seealso cref="SoundFilterType.BandPass"/> or <seealso cref="SoundFilterType.HighPass"/></param>
		/// <param name="strength">How strong the filter effect is. 0 = no effect, 1 = full effect</param>
		/// <param name="frequencyCap">The frequency parameter. Expected values are between 1000 and 8000</param>
		/// <param name="resonance">The resonance parameter. Expected values are between 2 and 20</param>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterBiquadResonantFilter(SoundFilterType type, float strength, float frequencyCap, float resonance) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var filterType = type switch {
				SoundFilterType.LowPass => BiquadResonantFilter.LOWPASS,
				SoundFilterType.BandPass => BiquadResonantFilter.BANDPASS,
				SoundFilterType.HighPass => BiquadResonantFilter.HIGHPASS,
				_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", nameof(type))
			};

			BiquadResonantFilter bqf = new BiquadResonantFilter();
			bqf.setParams(filterType, frequencyCap, resonance);
			bqf.SetStrength(strength);
			bqf.ID = MonoSoundLibrary.NextFilterID++;
			bqf.type = type;

			MonoSoundLibrary.customFilters.Add(bqf.ID, bqf);

			return bqf.ID;
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

			EchoFilter ech = new EchoFilter();
			ech.setParams(delay, decayFactor, filterStrength);
			ech.SetStrength(strength);
			ech.ID = MonoSoundLibrary.NextFilterID++;
			ech.type = SoundFilterType.Echo;

			MonoSoundLibrary.customFilters.Add(ech.ID, ech);

			return ech.ID;
		}

		/// <summary>
		/// Registers a Reverb filter
		/// </summary>
		/// <param name="filterStrength">How strong the filter effect is. 0 = no effect, 1 = full effect</param>
		/// <param name="lowFrequencyReverbStrength">How much the filter affects low frequencies. 0 = fast decaying, 1 = slow decaying. Defaults to 0.5</param>
		/// <param name="highFrequencyReverbStrength">How much the filter affects high frequencies. 0 = fast decaying, 1 = slow decaying. Defaults to 0.5</param>
		/// <param name="reverbStrength">How strong the reverb effect is. Expected values are between 0 and 1. Defaults to 1</param>
		/// <returns>The registered filter's ID</returns>
		public static int RegisterReverbFilter(float filterStrength, float lowFrequencyReverbStrength, float highFrequencyReverbStrength, float reverbStrength) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			FreeverbFilter rev = new FreeverbFilter();
			rev.setParams(0, lowFrequencyReverbStrength, highFrequencyReverbStrength, reverbStrength);
			rev.SetStrength(filterStrength);
			rev.ID = MonoSoundLibrary.NextFilterID++;
			rev.type = SoundFilterType.Reverb;

			MonoSoundLibrary.customFilters.Add(rev.ID, rev);

			return rev.ID;
		}

		/// <summary>
		/// Updates an already-existing Biquad Resonant filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="type">The new type.  If not <see langword="null"/>, it must be set to <see cref="SoundFilterType.LowPass"/>, <see cref="SoundFilterType.BandPass"/> or <see cref="SoundFilterType.HighPass"/></param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <param name="frequencyCap">The new frequency parameter.  If not <see langword="null"/>, it is expected to be between 1000 and 8000.</param>
		/// <param name="resonance">The new resonance.  If not <see langword="null"/>, it is expected to be between 2 and 20.</param>
		/// <exception cref="ArgumentException"/>
		public static void UpdateBiquadResonantFilter(int filterID, SoundFilterType? type = null, float? strength = null, float? frequencyCap = null, float? resonance = null) {
			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (!(filter is BiquadResonantFilter))
				throw new ArgumentException($"Filter {filterID} is not a Biquad Resonant filter", nameof(filterID));

			// Update the parameters
			if (strength is float s)
				filter.SetStrength(s);
			
			if (type is SoundFilterType t) {
				var filterType = t switch {
					SoundFilterType.LowPass => BiquadResonantFilter.LOWPASS,
					SoundFilterType.BandPass => BiquadResonantFilter.BANDPASS,
					SoundFilterType.HighPass => BiquadResonantFilter.HIGHPASS,
					_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", nameof(type))
				};

				filter.setFilterParameter(BiquadResonantFilter.TYPE, filterType);
			}

			if (frequencyCap is float f)
				filter.setFilterParameter(BiquadResonantFilter.FREQUENCY, f);

			if (resonance is float r)
				filter.setFilterParameter(BiquadResonantFilter.RESONANCE, r);
		}

		/// <summary>
		/// Updates an already-existing Echo filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <param name="delay">The new delay in seconds.  If not <see langword="null"/>, it must be greater than zero.</param>
		/// <param name="decayFactor">The new decay factor applied to successive echoes.  If not <see langword="null"/>, it must be greater than zero.</param>
		/// <param name="filterStrength">The new preference strength for old samples.  If not <see langword="null"/>, it must be greater than or equal to zero and less than one.</param>
		/// <exception cref="ArgumentException"/>
		public static void UpdateEchoFilter(int filterID, float? strength = null, float? delay = null, float? decayFactor = null, float? filterStrength = null) {
			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (!(filter is EchoFilter))
				throw new ArgumentException($"Filter {filterID} is not an Echo filter", nameof(filterID));

			// Update the parameters
			if (strength is float s)
				filter.SetStrength(s);

			if (delay is float dy) {
				if (dy <= 0)
					throw new ArgumentException("Delay must be positive", nameof(delay));

				filter.setFilterParameter(EchoFilter.DELAY, dy);
			}

			if (decayFactor is float dc) {
				if (dc <= 0)
					throw new ArgumentException("Decay factor must be positive", nameof(delay));

				filter.setFilterParameter(EchoFilter.DECAY, dc);
			}

			if (filterStrength is float f) {
				if (f < 0 || f >= 1.0f)
					throw new ArgumentException("Filter strength must be zero or a positive number less than one");

				filter.setFilterParameter(EchoFilter.FILTER, f);
			}
		}

		/// <summary>
		/// Updates an already-existing Reverb filter's parameters.<br/>
		/// This will affect all audio streams currently using the given filter, and any new <see cref="SoundEffect"/> objects returned by <see cref="EffectLoader"/>
		/// </summary>
		/// <param name="filterID">The registered filter ID</param>
		/// <param name="strength">The new strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <param name="lowFrequencyReverbStrength">The new low frequency modifier strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <param name="highFrequencyReverbStrength">The new high frequency modifier strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <param name="reverbStrength">The new reverb modifier strength.  If not <see langword="null"/>, it must be set to between 0 and 1.</param>
		/// <exception cref="ArgumentException"></exception>
		public static void UpdateReverbFilter(int filterID, float? strength = null, float? lowFrequencyReverbStrength = null, float? highFrequencyReverbStrength = null, float? reverbStrength = null) {
			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out var filter))
				throw new ArgumentException("Filter ID does not exist: " + filterID, nameof(filterID));

			if (!(filter is FreeverbFilter))
				throw new ArgumentException($"Filter {filterID} is not a Reverb filter", nameof(filterID));

			// Update the parameters
			if (strength is float s)
				filter.SetStrength(s);

			if (lowFrequencyReverbStrength is float lf) {
				if (lf < 0 || lf > 1)
					throw new ArgumentException("Low frequency modifier must be between 0 and 1", nameof(lowFrequencyReverbStrength));

				filter.setFilterParameter(FreeverbFilter.ROOMSIZE, lf);
			}

			if (highFrequencyReverbStrength is float hf) {
				if (hf < 0 || hf > 1)
					throw new ArgumentException("High frequency modifier must be between 0 and 1", nameof(highFrequencyReverbStrength));

				filter.setFilterParameter(FreeverbFilter.DAMP, hf);
			}

			if (reverbStrength is float r) {
				if (r < 0 || r > 1)
					throw new ArgumentException("Reverb modifier strength must be between 0 and 1", nameof(reverbStrength));

				filter.setFilterParameter(FreeverbFilter.WIDTH, r);
			}
		}
	}
}
