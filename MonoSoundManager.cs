using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using MonoSound.Streaming;
using MonoSound.XACT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MonoSound{
	/// <summary>
	/// The class where all sound filtering is handled through.
	/// </summary>
	public static class MonoSoundManager{
		private static Dictionary<int, Filter> customFilters;

		internal static Dictionary<string, MonoWaveBank> waveBanks;
		internal static Dictionary<string, MonoSoundBank> soundBanks;

		/// <summary>
		/// The next filter ID should one be registered.  Automatically assigned to new filters
		/// </summary>
		public static int NextFilterID{ get; private set; }

		/// <summary>
		/// Whether the Echo filter is allowed to generate over 30 seconds' worth of samples for a sound, which can happen when using high Delay and low Decay parameters.
		/// </summary>
		public static bool AllowEchoOversampling{ get; set; }

		/// <summary>
		/// If enabled, this folder path will be where filtered sounds are saved to. This property should be set after <seealso cref="Init"/> is called.
		/// </summary>
		public static string LogDirectory{ get; set; }

		/// <summary>
		/// Enables or disables the saving of filtered sounds.  Set <seealso cref="LogDirectory"/> to where the filtered sounds will be saved to
		/// </summary>
		public static bool LogFilters{ get; set; }

		private static bool initialized = false;

		/// <summary>
		/// The version for MonoSound
		/// </summary>
		public static readonly string Version = "1.5.2";

		/// <summary>
		/// Initializes MonoSound
		/// </summary>
		public static void Init(){
			if(initialized)
				throw new InvalidOperationException("MonoSound has already been initialized");

			SoundFilterManager.Init();

			customFilters = new Dictionary<int, Filter>();
			waveBanks = new Dictionary<string, MonoWaveBank>();
			soundBanks = new Dictionary<string, MonoSoundBank>();

			NextFilterID = 0;

			initialized = true;

			if(Directory.Exists(LogDirectory))
				Directory.Delete(LogDirectory, true);

			ThreadPool.QueueUserWorkItem(StreamManager.HandleStreaming);
		}

		/// <summary>
		/// Deinitializes MonoSound
		/// </summary>
		public static void DeInit(){
			if(!initialized)
				throw new InvalidOperationException("MonoSound has already been de-initialized");

			SoundFilterManager.DeInit();
			StreamManager.SignalStop();

			FilterSimulations.bqrFilter?.Free();
			FilterSimulations.echFilter?.Free();
			FilterSimulations.revFilter?.Free();

			customFilters = null;
			waveBanks = null;
			soundBanks = null;

			NextFilterID = 0;

			initialized = false;
		}

		/// <summary>
		/// Sets how many filters can be loaded at once
		/// </summary>
		public static void SetMaxFilterCount(int count){
			ThrowIfNotInitialized();

			if(count < 50)
				throw new ArgumentException("Value was too small.", "count");
			if(count > 1000)
				throw new ArgumentException("Value was too large.", "count");

			SoundFilterManager.Max_Filters_Loaded = count;
		}

		/// <summary>
		/// Clears any stored filters
		/// </summary>
		public static void ClearFilters(){
			ThrowIfNotInitialized();

			SoundFilterManager.Clear();
		}

		private static void ThrowIfNotInitialized(){
			if(!initialized)
				throw new InvalidOperationException("MonoSound has not initialized yet!");
		}

		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a compiled .xnb file, a .wav file, an .ogg file or an .mp3 file
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		public static SoundEffect GetEffect(string file){
			ThrowIfNotInitialized();

			SoundFilterManager.GetWavAndMetadata(file, out var wav, out _);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Applies the wanted filter to the sound file
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
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, params int[] filterIDs){
			ThrowIfNotInitialized();

			if(!AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", "filterIDs");

			return SoundFilterManager.CreateFilteredSFX(file, GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="fileType">The type of audio file the stream is supposed to represent</param>
		public static SoundEffect GetEffect(Stream stream, AudioType fileType){
			ThrowIfNotInitialized();

			SoundFilterManager.GetWavAndMetadata(stream, fileType, out var wav, out _);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Applies the wanted filter to the sound stream
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="type">The type of audio file the stream is supposed to represent</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(Stream stream, AudioType type, string nameIndicator, int filterID){
			ThrowIfNotInitialized();

			if(!customFilters.ContainsKey(filterID))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", "filterID");

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, customFilters[filterID]);
		}

		/// <summary>
		/// Applies the wanted filters to the sound stream in the order requested
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="type">The type of audio file the stream is supposed to represent</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(Stream stream, AudioType type, string nameIndicator, params int[] filterIDs){
			ThrowIfNotInitialized();

			if(!AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", "filterIDs");

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, GetFiltersFromIDs(filterIDs));
		}

		internal static void VerifyThatBanksExistInDictionary(string soundBankFile, string waveBankFile, out string soundBank, out string waveBank, bool setStreaming = false){
			if(Path.GetExtension(soundBankFile) != ".xsb")
				throw new ArgumentException($"Path provided was invalid: {soundBankFile}", "soundBankFile");
			if(Path.GetExtension(waveBankFile) != ".xwb")
				throw new ArgumentException($"Path provided was invalid: {waveBankFile}", "waveBankFile");

			//Get the file names without the extensions
			soundBank = Path.ChangeExtension(Path.GetFileName(soundBankFile), null);
			waveBank = Path.ChangeExtension(Path.GetFileName(waveBankFile), null);

			if(!soundBanks.ContainsKey(soundBankFile))
				soundBanks[soundBankFile] = MonoSoundBank.FromXNA(soundBankFile);
			
			//Wave bank needs to be loaded so that the sound bank can use it
			if(!waveBanks.ContainsKey(waveBankFile))
				waveBanks[waveBankFile] = MonoWaveBank.FromXNA(waveBankFile, setStreaming);
		}

		internal static void VerifyThatBanksExistInDictionary(Stream soundBank, string soundBankIdentifier, Stream waveBank, string waveBankIdentifier, out string soundBankName, out string waveBankName, bool setStreaming = false){
			if(Path.GetExtension(soundBankIdentifier) != ".xsb")
				throw new ArgumentException($"Path provided was invalid: {soundBankIdentifier}", "soundBankIdentifier");
			if(Path.GetExtension(waveBankIdentifier) != ".xwb")
				throw new ArgumentException($"Path provided was invalid: {waveBankIdentifier}", "waveBankIdentifier");

			//Get the file names without the extensions
			soundBankName = Path.ChangeExtension(Path.GetFileName(soundBankIdentifier), null);
			waveBankName = Path.ChangeExtension(Path.GetFileName(waveBankIdentifier), null);

			if(!soundBanks.ContainsKey(soundBankIdentifier))
				soundBanks[soundBankIdentifier] = MonoSoundBank.FromXNA(soundBank);
			
			//Wave bank needs to be loaded so that the sound bank can use it
			if(!waveBanks.ContainsKey(waveBankIdentifier))
				waveBanks[waveBankIdentifier] = MonoWaveBank.FromXNA(waveBank, waveBankIdentifier, setStreaming);
		}

		/// <summary>
		/// Loads a sound effect directly from the given sound bank and wave bank
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s contructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the sound ("cue") to get. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		public static SoundEffect GetEffectFromBank(string soundBankFile, string waveBankFile, string cueName){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			FormatWav wav = soundBanks[soundBank].GetAudio(cueName);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s constructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetBankFilteredEffect(string soundBankFile, string waveBankFile, string cueName, int filterID){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(soundBanks[soundBank].GetAudio(cueName), cueName, customFilters[filterID]);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s constructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetBankMultiFilteredEffect(string soundBankFile, string waveBankFile, string cueName, params int[] filterIDs){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(soundBanks[soundBank].GetAudio(cueName), cueName, GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankSource">A stream representing the sound bank's data</param>
		/// <param name="soundBankIdentifier">A string used to identify the sound bank</param>
		/// <param name="waveBankSource">A stream representing the wave bank's data</param>
		/// <param name="waveBankIdentifier">A string used to identify the wave bank</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		public static SoundEffect GetBankEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			FormatWav wav = soundBanks[soundBank].GetAudio(cueName);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankSource">A stream representing the sound bank's data</param>
		/// <param name="soundBankIdentifier">A string used to identify the sound bank</param>
		/// <param name="waveBankSource">A stream representing the wave bank's data</param>
		/// <param name="waveBankIdentifier">A string used to identify the wave bank</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetBankFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, int filterID){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(soundBanks[soundBank].GetAudio(cueName), cueName, customFilters[filterID]);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankSource">A stream representing the sound bank's data</param>
		/// <param name="soundBankIdentifier">A string used to identify the sound bank</param>
		/// <param name="waveBankSource">A stream representing the wave bank's data</param>
		/// <param name="waveBankIdentifier">A string used to identify the wave bank</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetBankMultiFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, params int[] filterIDs){
			ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(soundBanks[soundBank].GetAudio(cueName), cueName, GetFiltersFromIDs(filterIDs));
		}

		private static Filter[] GetFiltersFromIDs(int[] ids){
			//LINQ bad
			Filter[] filters = new Filter[ids.Length];
			for(int i = 0; i < ids.Length; i++)
				filters[i] = customFilters[ids[i]];
			return filters;
		}

		private static bool AllFiltersIDsExist(int[] ids){
			for(int i = 0; i < ids.Length; i++){
				if(!customFilters.ContainsKey(ids[i]))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="filePath">The path to the sound file. Must refer to a compiled .xnb file, a .wav file, an .ogg file or an .mp3 file.</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static SoundEffectInstance GetStreamedSound(string filePath, bool looping){
			ThrowIfNotInitialized();

			string extension = Path.GetExtension(filePath);

			return extension switch{
				".xnb" => StreamManager.InitializeStream(filePath, looping, AudioType.XNB),
				".wav" => StreamManager.InitializeStream(filePath, looping, AudioType.WAV),
				".ogg" => StreamManager.InitializeStream(filePath, looping, AudioType.OGG),
				".mp3" => StreamManager.InitializeStream(filePath, looping, AudioType.MP3),
				_ => throw new ArgumentException($"Input file must be a compiled XNB file, a WAVE file, an OGG Vorbis file or an MPEG Audio Layer III file: \"{filePath}\""),
			};
		}

		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="sampleSource">The stream where the samples will be read from. It is expected to contain a full audio file's data</param>
		/// <param name="fileIdentifier">An enumeration value indicating what type of audio <paramref name="sampleSource"/> contains.  Cannot be <seealso cref="AudioType.XWB"/></param>
		/// <param name="looping">Whether the sound should loop</param>
		public static SoundEffectInstance GetStreamedSound(Stream sampleSource, AudioType fileIdentifier, bool looping){
			ThrowIfNotInitialized();

			return fileIdentifier != AudioType.XWB
				? StreamManager.InitializeStream(sampleSource, looping, fileIdentifier)
				: throw new ArgumentException("XWB streams must be created via MonoSoundManager.GetStreamedXACTSound()");
		}

		/// <summary>
		/// Gets a streamed sound effect from an XACT wave bank
		/// </summary>
		/// <param name="soundBankPath">The path to the sound bank</param>
		/// <param name="waveBankPath">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static SoundEffectInstance GetStreamedXACTSound(string soundBankPath, string waveBankPath, string cueName, bool looping){
			ThrowIfNotInitialized();

			return StreamManager.InitializeXWBStream(soundBankPath, waveBankPath, cueName, looping);
		}

		/// <summary>
		/// Gets a streamed sound effect from an XACT wave bank
		/// </summary>
		/// <param name="soundBankSource">A stream representing the sound bank's data</param>
		/// <param name="soundBankIdentifier">A string used to identify the sound bank</param>
		/// <param name="waveBankSource">A stream representing the wave bank's data</param>
		/// <param name="waveBankIdentifier">A string used to identify the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static SoundEffectInstance GetStreamedXACTSound(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool looping){
			ThrowIfNotInitialized();

			return StreamManager.InitializeXWBStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, looping);
		}

		/// <summary>
		/// Stops streaming for a certain sound effect, stops playing it and then disposes it
		/// </summary>
		/// <param name="instance">The sound effect instance</param>
		public static void FreeStreamedSound(ref SoundEffectInstance instance){
			ThrowIfNotInitialized();

			StreamManager.StopStreamingSound(ref instance);
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

			var filterType = type switch{
				SoundFilterType.LowPass => BiquadResonantFilter.LOWPASS,
				SoundFilterType.BandPass => BiquadResonantFilter.BANDPASS,
				SoundFilterType.HighPass => BiquadResonantFilter.HIGHPASS,
				_ => throw new ArgumentException("Given type wasn't a valid Biquad Resonant Filter type.", "type")
			};
			
			BiquadResonantFilter bqf = new BiquadResonantFilter();
			bqf.setParams(filterType, frequencyCap, resonance);
			bqf.SetStrength(strength);
			bqf.ID = NextFilterID++;
			bqf.type = type;

			customFilters.Add(bqf.ID, bqf);

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
		public static int RegisterEchoFilter(float strength, float delay, float decayFactor, float filterStrength){
			ThrowIfNotInitialized();

			EchoFilter ech = new EchoFilter();
			ech.setParams(delay, decayFactor, filterStrength);
			ech.SetStrength(strength);
			ech.ID = NextFilterID++;
			ech.type = SoundFilterType.Echo;

			customFilters.Add(ech.ID, ech);

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
		public static int RegisterReverbFilter(float filterStrength, float lowFrequencyReverbStrength, float highFrequencyReverbStrength, float reverbStrength){
			ThrowIfNotInitialized();

			FreeverbFilter rev = new FreeverbFilter();
			rev.setParams(0, lowFrequencyReverbStrength, highFrequencyReverbStrength, reverbStrength);
			rev.SetStrength(filterStrength);
			rev.ID = NextFilterID++;
			rev.type = SoundFilterType.Reverb;

			customFilters.Add(rev.ID, rev);

			return rev.ID;
		}
	}
}
