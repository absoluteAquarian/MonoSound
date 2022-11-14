using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.XACT;
using System;
using System.IO;

namespace MonoSound {
	partial class EffectLoader {
		internal static void VerifyThatBanksExistInDictionary(string soundBankFile, string waveBankFile, out string soundBank, out string waveBank, bool setStreaming = false) {
			if (Path.GetExtension(soundBankFile) != ".xsb")
				throw new ArgumentException($"Path provided was invalid: {soundBankFile}", "soundBankFile");
			if (Path.GetExtension(waveBankFile) != ".xwb")
				throw new ArgumentException($"Path provided was invalid: {waveBankFile}", "waveBankFile");

			//Get the file names without the extensions
			soundBank = Path.ChangeExtension(Path.GetFileName(soundBankFile), null);
			waveBank = Path.ChangeExtension(Path.GetFileName(waveBankFile), null);

			if (!MonoSoundLibrary.soundBanks.ContainsKey(soundBankFile))
				MonoSoundLibrary.soundBanks[soundBankFile] = MonoSoundBank.FromXNA(soundBankFile);

			//Wave bank needs to be loaded so that the sound bank can use it
			if (!MonoSoundLibrary.waveBanks.ContainsKey(waveBankFile) && !MonoSoundLibrary.streamedWaveBanks.ContainsKey(waveBankFile))
				MonoWaveBank.LoadFromXWBFile(waveBankFile, setStreaming);
		}

		internal static void VerifyThatBanksExistInDictionary(Stream soundBank, string soundBankIdentifier, Stream waveBank, string waveBankIdentifier, out string soundBankName, out string waveBankName, bool setStreaming = false) {
			if (Path.GetExtension(soundBankIdentifier) != ".xsb")
				throw new ArgumentException($"Path provided was invalid: {soundBankIdentifier}", "soundBankIdentifier");
			if (Path.GetExtension(waveBankIdentifier) != ".xwb")
				throw new ArgumentException($"Path provided was invalid: {waveBankIdentifier}", "waveBankIdentifier");

			//Get the file names without the extensions
			soundBankName = Path.ChangeExtension(Path.GetFileName(soundBankIdentifier), null);
			waveBankName = Path.ChangeExtension(Path.GetFileName(waveBankIdentifier), null);

			if (!MonoSoundLibrary.soundBanks.ContainsKey(soundBankIdentifier))
				MonoSoundLibrary.soundBanks[soundBankIdentifier] = MonoSoundBank.FromXNA(soundBank);

			//Wave bank needs to be loaded so that the sound bank can use it
			if (!MonoSoundLibrary.waveBanks.ContainsKey(waveBankIdentifier) && !MonoSoundLibrary.streamedWaveBanks.ContainsKey(waveBankIdentifier))
				MonoWaveBank.LoadFromXWBStream(waveBank, waveBankIdentifier, setStreaming);
		}

		/// <summary>
		/// Loads a sound effect directly from the given sound bank and wave bank
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s contructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the sound ("cue") to get. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		public static SoundEffect GetEffectFromBank(string soundBankFile, string waveBankFile, string cueName) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			FormatWav wav = MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s constructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetBankFilteredEffect(string soundBankFile, string waveBankFile, string cueName, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName), cueName, MonoSoundLibrary.customFilters[filterID]);
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankFile">The path to the sound bank. Use the same path you would use in <seealso cref="SoundBank"/>'s constructor.</param>
		/// <param name="waveBankFile">The path to the wave bank. Use the same path you would use in <seealso cref="WaveBank"/>'s constructor.</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetBankMultiFilteredEffect(string soundBankFile, string waveBankFile, string cueName, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankFile, waveBankFile, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName), cueName, MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Retrieves a given sound using the requested sound bank and wave bank, then applies the wanted filter to it.
		/// </summary>
		/// <param name="soundBankSource">A stream representing the sound bank's data</param>
		/// <param name="soundBankIdentifier">A string used to identify the sound bank</param>
		/// <param name="waveBankSource">A stream representing the wave bank's data</param>
		/// <param name="waveBankIdentifier">A string used to identify the wave bank</param>
		/// <param name="cueName">The name of the cue. Use the same name you would use in <seealso cref="SoundBank.GetCue(string)"/>.</param>
		public static SoundEffect GetBankEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			FormatWav wav = MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName);
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
		public static SoundEffect GetBankFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName), cueName, MonoSoundLibrary.customFilters[filterID]);
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
		public static SoundEffect GetBankMultiFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			VerifyThatBanksExistInDictionary(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, out string soundBank, out _);

			return SoundFilterManager.CreateFilteredSFX(MonoSoundLibrary.soundBanks[soundBank].GetAudio(cueName), cueName, MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}
	}
}
