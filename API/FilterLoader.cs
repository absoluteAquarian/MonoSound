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
		/// <returns></returns>
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
		/// <returns></returns>
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
		/// <returns></returns>
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
	}
}
