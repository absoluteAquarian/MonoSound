using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using System;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The central class for loading sound effects
	/// </summary>
	public static partial class EffectLoader {
		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a compiled .xnb file, a .wav file, an .ogg file, an .mp3 file or a registered custom format
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		public static SoundEffect GetEffect(string file) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			SoundFilterManager.GetWavAndMetadata(file, out var wav, out _);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a <see cref="SoundEffect"/> from a file with a custom format
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		public static SoundEffect GetEffect(string file, CustomAudioFormat format) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var wav = format.ReadWav(file);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out Filter value))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", nameof(filterID));

			return SoundFilterManager.CreateFilteredSFX(file, value);
		}
		
		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, CustomAudioFormat format, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out Filter value))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", nameof(filterID));

			var wav = format.ReadWav(file);
			return SoundFilterManager.ApplyFilters(wav, file, wav.GetMetadata(), value);
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", nameof(filterIDs));

			return SoundFilterManager.CreateFilteredSFX(file, MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, CustomAudioFormat format, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", nameof(filterIDs));

			var wav = format.ReadWav(file);
			return SoundFilterManager.ApplyFilters(wav, file, wav.GetMetadata(), MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="fileType">The type of audio file the stream is supposed to represent</param>
		public static SoundEffect GetEffect(Stream stream, AudioType fileType) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			SoundFilterManager.GetWavAndMetadata(stream, fileType, out var wav, out _);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a <seealso cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		public static SoundEffect GetEffect(Stream stream, CustomAudioFormat format) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var wav = format.ReadWav(stream);
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
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out Filter value))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", nameof(filterID));

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, value);
		}

		/// <summary>
		/// Applies the wanted filter to the sound stream
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(Stream stream, CustomAudioFormat format, string nameIndicator, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.customFilters.TryGetValue(filterID, out Filter value))
				throw new ArgumentException("Given Filter ID does not correspond to a registered sound filter.", nameof(filterID));

			var wav = format.ReadWav(stream);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, value);
		}

		/// <summary>
		/// Applies the wanted filters to the sound stream in the order requested
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="type">The type of audio file the stream is supposed to represent</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(Stream stream, AudioType type, string nameIndicator, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", nameof(filterIDs));

			SoundFilterManager.GetWavAndMetadata(stream, type, out var wav, out _);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}

		/// <summary>
		/// Applies the wanted filters to the sound stream in the order requested
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(Stream stream, CustomAudioFormat format, string nameIndicator, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (!MonoSoundLibrary.AllFiltersIDsExist(filterIDs))
				throw new ArgumentException("One of the given Filter IDs does not correspond to a registered sound filter.", nameof(filterIDs));

			var wav = format.ReadWav(stream);
			return SoundFilterManager.CreateFilteredSFX(wav, nameIndicator, MonoSoundLibrary.GetFiltersFromIDs(filterIDs));
		}
	}
}
