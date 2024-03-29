﻿using Microsoft.Xna.Framework;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Filters.Instances;
using MonoSound.Streaming;
using MonoSound.XACT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonoSound {
	public static class MonoSoundLibrary {
		internal static Dictionary<int, Filter> customFilters;

		internal static Dictionary<string, CustomFileFormat> registeredFormats;

		internal static Dictionary<string, CustomAudioFormat> customAudioFormats;

		internal static Dictionary<string, MonoWaveBank> waveBanks;
		internal static Dictionary<string, MonoWaveBank> streamedWaveBanks;
		internal static Dictionary<string, MonoSoundBank> soundBanks;

		private static bool initialized = false;

		internal static CancellationTokenSource cancelSource;

		/// <summary>
		/// Gets a cancellation token that can be watched for when MonoSound deinitializes
		/// </summary>
		/// <returns></returns>
		public static CancellationToken GetCancellationToken() => cancelSource.Token;

		/// <summary>
		/// The next filter ID should one be registered.  Automatically assigned to new filters
		/// </summary>
		public static int NextFilterID { get; internal set; }

		internal static Game Game { get; private set; }

		/// <summary>
		/// The version for MonoSound
		/// </summary>
		public static readonly string Version = "1.7.1.1";

		/// <inheritdoc cref="Init(Game)"/>
		[Obsolete("Use the overload with the Game parameter instead", error: true)]
		public static void Init() => Init(null);

		/// <summary>
		/// Initializes the MonoSound library
		/// </summary>
		/// <param name="game">The game instance.  This variable will be used by certain APIs.</param>
		public static void Init(Game game) {
			if (initialized)
				throw new InvalidOperationException("MonoSound has already been initialized");

			Game = game;

			cancelSource = new CancellationTokenSource();

			SoundFilterManager.Init();
			StreamManager.Initialize();

			customFilters = new Dictionary<int, Filter>();
			registeredFormats = new Dictionary<string, CustomFileFormat>();
			customAudioFormats = new Dictionary<string, CustomAudioFormat>();
			waveBanks = new Dictionary<string, MonoWaveBank>();
			streamedWaveBanks = new Dictionary<string, MonoWaveBank>();
			soundBanks = new Dictionary<string, MonoSoundBank>();

			NextFilterID = 0;

			initialized = true;

			try {
				if (Directory.Exists(Controls.LogDirectory))
					Directory.Delete(Controls.LogDirectory, true);
			} catch { }
		}

		/// <summary>
		/// Deinitializes MonoSound
		/// </summary>
		public static void DeInit() {
			if (!initialized)
				throw new InvalidOperationException("MonoSound has already been de-initialized");

			cancelSource.Cancel();

			SoundFilterManager.DeInit();
			StreamManager.Deinitialize();

			FilterSimulations.bqrFilter?.Free();
			FilterSimulations.echFilter?.Free();
			FilterSimulations.revFilter?.Free();

			customFilters = null;
			registeredFormats = null;
			customAudioFormats = null;
			waveBanks = null;
			streamedWaveBanks = null;
			soundBanks = null;

			NextFilterID = 0;

			initialized = false;
		}

		/// <summary>
		/// Assigns or overwrites how a custom file format is parsed into the <see cref="FormatWav"/> object format
		/// </summary>
		/// <param name="extension">The extension used to identify the file format</param>
		/// <param name="readFull">
		/// A function which generates a <see cref="FormatWav"/> object from a data stream.<br/>
		/// If the data stream is invalid, make the function return <see langword="null"/>.<br/>
		/// Disposing the <see cref="Stream"/> parameter is <b>NOT</b> recommended.
		/// </param>
		/// <param name="readStreamed">
		/// A function which generates a <see cref="StreamPackage"/> object from a data stream.<br/>
		/// If the data stream is invalid, make the function return <see langword="null"/>.<br/>
		/// Disposing the <see cref="Stream"/> parameter is <b>NOT</b> recommended.
		/// </param>
		[Obsolete("The CustomFileFormat API is deprecated.  Use the RegisterFormat overload that uses CustomAudioFormat instead")]
		public static CustomFileFormat RegisterFormat(string extension, Func<Stream, FormatWav> readFull, Func<Stream, StreamPackage> readStreamed) {
			ThrowIfNotInitialized();

			CustomFileFormat format = registeredFormats[extension] = new CustomFileFormat(extension, readFull, readStreamed);

			if (!SoundFilterManager.AllValidExtensions.Contains(extension))
				SoundFilterManager.AllValidExtensions.Add(extension);

			return format;
		}

		/// <summary>
		/// Registers a custom audio format for use in automatic file/stream handling
		/// </summary>
		/// <param name="format">The audio format object.  This object will handle reading from files, audio streams and indicating what file formats it supports.</param>
		/// <exception cref="ArgumentException"/>
		public static void RegisterFormat(CustomAudioFormat format) {
			ThrowIfNotInitialized();

			foreach (string ext in SoundFilterManager.AllValidExtensions.Concat(new string[] { ".xsb", ".xwb" })) {
				if (format.DoesExtensionApply(ext))
					throw new ArgumentException($"The audio format \"{ext}\" was already supported, cannot add another format that accepts it");
			}

			foreach (string extension in format.ValidExtensions) {
				customAudioFormats[extension] = format;

				if (!SoundFilterManager.AllValidExtensions.Contains(extension))
					SoundFilterManager.AllValidExtensions.Add(extension);
			}
		}

		/// <summary>
		/// Sets how many filters can be loaded at once
		/// </summary>
		public static void SetMaxFilterCount(int count) {
			ThrowIfNotInitialized();

			if (count < 50)
				throw new ArgumentException("Value was too small.", "count");
			if (count > 32000)
				throw new ArgumentException("Value was too large.", "count");

			SoundFilterManager.Max_Filters_Loaded = count;
		}

		/// <summary>
		/// Clears any stored filters
		/// </summary>
		public static void ClearFilters() {
			ThrowIfNotInitialized();

			SoundFilterManager.Clear();
		}

		internal static void ThrowIfNotInitialized() {
			if (!initialized)
				throw new InvalidOperationException("MonoSound has not initialized yet!");
		}

		internal static Filter[] GetFiltersFromIDs(int[] ids) => ids.Select(i => customFilters[i]).ToArray();

		internal static bool AllFiltersIDsExist(int[] ids) => ids.AsParallel().All(i => customFilters.ContainsKey(i));
	}
}
