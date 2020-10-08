using Microsoft.Xna.Framework.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;
using System.Collections.Generic;

namespace MonoSound{
	/// <summary>
	/// The class where all sound filtering is handled through.
	/// </summary>
	public static class MonoSoundManager{
		private static Dictionary<int, Filter> customFilters;

		/// <summary>
		/// The next filter ID should one be registered.  Automatically assigned to new filters
		/// </summary>
		public static int NextFilterID{ get; private set; }

		private static bool initialized = false;

		/// <summary>
		/// Initializes MonoSound
		/// </summary>
		public static void Init(){
			SoundFilterManager.Init();

			customFilters = new Dictionary<int, Filter>();

			NextFilterID = 0;

			initialized = true;
		}

		/// <summary>
		/// Deinitializes MonoSound
		/// </summary>
		public static void DeInit(){
			SoundFilterManager.DeInit();

			FilterSimulations.bqrFilter?.Free();

			customFilters = null;

			NextFilterID = 0;

			initialized = false;
		}

		/// <summary>
		/// Sets how many filters can be loaded at once
		/// </summary>
		public static void SetMaxFilterCount(int count){
			if(count < 50)
				throw new ArgumentException("Value was too small.", "count");
			if(count > 1000)
				throw new ArgumentException("Value was too large.", "count");

			SoundFilterManager.Max_Filters_Loaded = count;
		}

		/// <summary>
		/// Clears any stored filters
		/// </summary>
		public static void ClearFilters() => SoundFilterManager.Clear();

		private static void ThrowIfNotInitialized(){
			if(!initialized)
				throw new InvalidOperationException("MonoSound has not initialized yet!");
		}

		/// <summary>
		/// Applies the wanted filter to the sound file.
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, int filterID){
			ThrowIfNotInitialized();

			if(!customFilters.ContainsKey(filterID))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", "filterID");

			return SoundFilterManager.CreateFilteredSFX(file, customFilters[filterID]);
		}

		/// <summary>
		/// Registers a Biquad Resonant filter.
		/// </summary>
		/// <param name="type">The filter type to use. Must either be <seealso cref="SoundFilterType.LowPass"/>, <seealso cref="SoundFilterType.BandPass"/> or <seealso cref="SoundFilterType.HighPass"/></param>
		/// <param name="strength">How strong the filter effect is. 0 = no effect, 1 = full effect</param>
		/// <param name="frequencyCap">The frequency parameter. Expected values are between 1000 and 8000</param>
		/// <param name="resonance">The resonance parameter. Expected values are between 2 and 20</param>
		/// <returns></returns>
		public static int RegisterBiquadResonantFilter(SoundFilterType type, float strength, float frequencyCap, float resonance){
			ThrowIfNotInitialized();

			int filterType;
			switch(type){
				case SoundFilterType.LowPass:
					filterType = BiquadResonantFilter.LOWPASS;
					break;
				case SoundFilterType.BandPass:
					filterType = BiquadResonantFilter.BANDPASS;
					break;
				case SoundFilterType.HighPass:
					filterType = BiquadResonantFilter.HIGHPASS;
					break;
				default:
					throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", "type");
			}

			BiquadResonantFilter bqf = new BiquadResonantFilter();
			bqf.setParams(filterType, frequencyCap, resonance);
			bqf.SetStrength(strength);
			bqf.ID = NextFilterID++;
			bqf.type = type;

			customFilters.Add(bqf.ID, bqf);

			return bqf.ID;
		}
	}
}
