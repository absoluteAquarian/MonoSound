using Microsoft.Xna.Framework.Audio;
using MonoSound.Filters;
using System;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The central class for loading sound effects
	/// </summary>
	public static partial class EffectLoader {
		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a compiled .xnb file, a .wav file, an .ogg file or an .mp3 file
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		public static SoundEffect GetEffect(string file) {
			MonoSound.ThrowIfNotInitialized();

			SoundFilterManager.GetWavAndMetadata(file, out var wav, out _);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, int filterID) {
			MonoSound.ThrowIfNotInitialized();

			if (!MonoSound.customFilters.ContainsKey(filterID))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", "filterID");

			return SoundFilterManager.CreateFilteredSFX(file, MonoSound.customFilters[filterID]);
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, params int[] filterIDs) {
			MonoSound.ThrowIfNotInitialized();

			if (!MonoSound.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", "filterIDs");

			return SoundFilterManager.CreateFilteredSFX(file, MonoSound.GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="fileType">The type of audio file the stream is supposed to represent</param>
		public static SoundEffect GetEffect(Stream stream, AudioType fileType) {
			MonoSound.ThrowIfNotInitialized();

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
		public static SoundEffect GetFilteredEffect(Stream stream, AudioType type, string nameIndicator, int filterID) {
			MonoSound.ThrowIfNotInitialized();

			if (!MonoSound.customFilters.ContainsKey(filterID))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", "filterID");

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, MonoSound.customFilters[filterID]);
		}

		/// <summary>
		/// Applies the wanted filters to the sound stream in the order requested
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="type">The type of audio file the stream is supposed to represent</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(Stream stream, AudioType type, string nameIndicator, params int[] filterIDs) {
			MonoSound.ThrowIfNotInitialized();

			if (!MonoSound.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", "filterIDs");

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, MonoSound.GetFiltersFromIDs(filterIDs));
		}
	}
}
