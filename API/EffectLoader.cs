using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using System;
using System.Collections.Generic;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The central class for loading sound effects
	/// </summary>
	public static partial class EffectLoader {
		/// <summary>
		/// Retrieves a <see cref="SoundEffect"/> from a compiled .xnb file, a .wav file, an .ogg file, an .mp3 file or a registered custom format
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		public static SoundEffect GetEffect(string file) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			FormatWav wav = FormatWav.FromFile(file);
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
		/// Retrieves a <see cref="SoundEffect"/> from a file with a custom format
		/// </summary>
		/// <param name="file">The file to get the sound from</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		public static SoundEffect GetEffect(string file, CustomAudioFormat format, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var wav = format.ReadWav(file, state);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFilterTo(FormatWav.FromFile(file), file, GetSingleton(filterID));
		}
		
		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, CustomAudioFormat format, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFilterTo(format.ReadWav(file), file, GetSingleton(filterID));
		}

		/// <summary>
		/// Applies the wanted filter to the sound file
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(string file, CustomAudioFormat format, object state, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFilterTo(format.ReadWav(file, state), file, GetSingleton(filterID));
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.CreateFilteredSFX(file, GetSingletons(filterIDs));
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, CustomAudioFormat format, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFiltersTo(format.ReadWav(file), file, GetSingletons(filterIDs));
		}

		/// <summary>
		/// Applies the wanted filters to the sound file in the order requested
		/// </summary>
		/// <param name="file">The path to the sound file. Extension required.</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(string file, CustomAudioFormat format, object state, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFiltersTo(format.ReadWav(file, state), file, GetSingletons(filterIDs));
		}

		/// <summary>
		/// Retrieves a <see cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="fileType">The type of audio file the stream is supposed to represent</param>
		public static SoundEffect GetEffect(Stream stream, AudioType fileType) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			FormatWav wav = FormatWav.FromStream(stream, fileType);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a <see cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		public static SoundEffect GetEffect(Stream stream, CustomAudioFormat format) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var wav = format.ReadWav(stream);
			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		/// <summary>
		/// Retrieves a <see cref="SoundEffect"/> from a stream of data
		/// </summary>
		/// <param name="stream">The stream to retrieve the effect from</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		public static SoundEffect GetEffect(Stream stream, CustomAudioFormat format, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			var wav = format.ReadWav(stream, state);
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

			return FilterSimulations.ApplyFilterTo(FormatWav.FromStream(stream, type), nameIndicator, GetSingleton(filterID));
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

			return FilterSimulations.ApplyFilterTo(format.ReadWav(stream), nameIndicator, GetSingleton(filterID));
		}

		/// <summary>
		/// Applies the wanted filter to the sound stream
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterID">The ID of the filter to use.</param>
		public static SoundEffect GetFilteredEffect(Stream stream, CustomAudioFormat format, object state, string nameIndicator, int filterID) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFilterTo(format.ReadWav(stream, state), nameIndicator, GetSingleton(filterID));
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

			return FilterSimulations.ApplyFiltersTo(FormatWav.FromStream(stream, type), nameIndicator, GetSingletons(filterIDs));
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

			return FilterSimulations.ApplyFiltersTo(format.ReadWav(stream), nameIndicator, GetSingletons(filterIDs));
		}

		/// <summary>
		/// Applies the wanted filters to the sound stream in the order requested
		/// </summary>
		/// <param name="stream">The stream to retrieve the sound data from.  It is expected to be a full audio file</param>
		/// <param name="format">An object representing how the format will decode the audio data to the WAVE format</param>
		/// <param name="state">Extra information for the format to use</param>
		/// <param name="nameIndicator">A string used to represent the filtered sound effect</param>
		/// <param name="filterIDs">The list of filter IDs to use.</param>
		public static SoundEffect GetMultiFilteredEffect(Stream stream, CustomAudioFormat format, object state, string nameIndicator, params int[] filterIDs) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return FilterSimulations.ApplyFiltersTo(format.ReadWav(stream, state), nameIndicator, GetSingletons(filterIDs));
		}

		internal static SoLoudFilterInstance GetSingleton(int filterID) {
			var filter = FilterLoader.GetRegisteredFilter(filterID);
			filter.Singleton.ResetFilterState();
			return filter.Singleton;
		}

		internal static SoLoudFilterInstance[] GetSingletons(int[] filterIDs) {
			List<SoLoudFilterInstance> singletons = [];

			foreach (int id in filterIDs) {
				var filter = FilterLoader.GetRegisteredFilter(id);
				filter.Singleton.ResetFilterState();
				singletons.Add(filter.Singleton);
			}

			return [.. singletons];
		}
	}
}
