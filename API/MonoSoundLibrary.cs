using Microsoft.Xna.Framework;
using MonoSound.Audio;
using MonoSound.Default;
using MonoSound.Filters;
using MonoSound.Streaming;
using MonoSound.XACT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MonoSound {
	/// <summary>
	/// The central class for general sound management
	/// </summary>
	public static class MonoSoundLibrary {
		private static readonly string[] validExtensions = [ ".xnb", ".wav", ".ogg", ".mp3" ];

		internal static List<string> AllValidExtensions = [ .. validExtensions ];

		internal static Dictionary<int, SoLoudFilter> singletonFilters;

#pragma warning disable CS0618 // Type or member is obsolete
		internal static Dictionary<string, CustomFileFormat> registeredFormats;
#pragma warning restore CS0618 // Type or member is obsolete

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

		private static Thread _mainThread;

		/// <summary>
		/// Gets whether the current thread is the main thread, i.e. the thread that <see cref="Init(Game)"/> was called on
		/// </summary>
		public static bool IsMainThread => Thread.CurrentThread == _mainThread;

		/// <summary>
		/// The next filter ID should one be registered.  Automatically assigned to new filters
		/// </summary>
		public static int NextFilterID { get; internal set; }

		internal static Game Game { get; private set; }

		internal const string VersionLiteral_FilterOverhaul = "1.8";

		/// <inheritdoc cref="Version"/>
		public const string VersionLiteral = "1.8";

		/// <summary>
		/// The version for MonoSound
		/// </summary>
		public static readonly string Version = VersionLiteral;

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

			StreamManager.Initialize();

			singletonFilters = [];
			registeredFormats = [];
			customAudioFormats = [];
			waveBanks = [];
			streamedWaveBanks = [];
			soundBanks = [];

			_mainThread = Thread.CurrentThread;

			NextFilterID = 0;

			initialized = true;

			// Built-in custom formats
			RegisterFormat(new PcmFormat());

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

			StreamManager.Deinitialize();

			singletonFilters = null;
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

			if (!AllValidExtensions.Contains(extension))
				AllValidExtensions.Add(extension);

			return format;
		}

		/// <summary>
		/// Registers a custom audio format for use in automatic file/stream handling
		/// </summary>
		/// <param name="format">The audio format object.  This object will handle reading from files, audio streams and indicating what file formats it supports.</param>
		/// <exception cref="ArgumentException"/>
		public static void RegisterFormat(CustomAudioFormat format) {
			ThrowIfNotInitialized();

			foreach (string ext in AllValidExtensions.Concat([ ".xsb", ".xwb" ])) {
				if (format.DoesExtensionApply(ext))
					throw new ArgumentException($"The audio format \"{ext}\" was already supported, cannot add another format that accepts it");
			}

			foreach (string extension in format.ValidExtensions) {
				customAudioFormats[extension] = format;

				if (!AllValidExtensions.Contains(extension))
					AllValidExtensions.Add(extension);
			}
		}

		/// <summary>
		/// Sets how many filters can be loaded at once
		/// </summary>
		[Obsolete("The filter cap has been removed since v" + VersionLiteral_FilterOverhaul, error: true)]
		public static void SetMaxFilterCount(int count) {
			ThrowIfNotInitialized();

			// No longer does anything
		}

		/// <summary>
		/// Clears any stored filters
		/// </summary>
		[Obsolete("Filtered effects are no longer cached since v" + VersionLiteral_FilterOverhaul, error: true)]
		public static void ClearFilters() {
			ThrowIfNotInitialized();

			// No longer does anything
		}

		internal static void ThrowIfNotInitialized() {
			if (!initialized)
				throw new InvalidOperationException("MonoSound has not initialized yet!");
		}
	}
}
