using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters;
using MonoSound.Streaming;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The central class for loading streamed sounds
	/// </summary>
	public static class StreamLoader {
		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="filePath">The path to the sound file</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedSound(string filePath, bool looping) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			string extension = Path.GetExtension(filePath);

			StreamPackage instance;

			switch (extension) {
				case ".xnb":
					instance = StreamManager.InitializeStream(filePath, looping, AudioType.XNB);
					break;
				case ".wav":
					instance = StreamManager.InitializeStream(filePath, looping, AudioType.WAV);
					break;
				case ".ogg":
					instance = StreamManager.InitializeStream(filePath, looping, AudioType.OGG);
					break;
				case ".mp3":
					instance = StreamManager.InitializeStream(filePath, looping, AudioType.MP3);
					break;
				case ".xwb":
					throw new ArgumentException("XWB streams must be created via StreamLoader.GetStreamedXACTSound()");
				default:
					if (MonoSoundLibrary.AllValidExtensions.Contains(extension))
						throw new ArgumentException("Custom streams cannot be created using this overload of GetStreamedSound");
					throw new ArgumentException($"Extension \"{extension}\" was not recognized by any known format");
			}

			return instance;
		}

		/// <summary>
		/// Gets a streamed sound effect from one of the registered custom audio formats
		/// </summary>
		/// <param name="filePath">The path to the sound file</param>
		/// <param name="looping">Whether the sound should loop</param>
		/// <param name="state">Extra information for the format to use</param>
		public static StreamPackage GetStreamedSound(string filePath, bool looping, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return StreamManager.TryInitializeCustomStream(filePath, looping, state);
		}

		/// <summary>
		/// Gets a streamed sound effect using a custom audio format
		/// </summary>
		/// <param name="filePath">The path to the sound file</param>
		/// <param name="format">An object representing how the format will decode the audio stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		/// <param name="state">Extra information for the format to use</param>
		public static StreamPackage GetStreamedSound(string filePath, CustomAudioFormat format, bool looping, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return StreamManager.InitializeCustomStream(filePath, format, looping, state);
		}

		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="sampleSource">The stream where the samples will be read from. It is expected to contain a full audio file's data</param>
		/// <param name="fileIdentifier">An enumeration value indicating what type of audio <paramref name="sampleSource"/> contains.  Cannot be <see cref="AudioType.XWB"/> nor <see cref="AudioType.Custom"/></param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedSound(Stream sampleSource, AudioType fileIdentifier, bool looping) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return fileIdentifier switch {
				AudioType.XWB => throw new ArgumentException("XWB streams must be created via StreamLoader.GetStreamedXACTSound()"),
				AudioType.Custom => throw new ArgumentException("Custom streams cannot be created using this overload of GetStreamedSound"),
				_ => StreamManager.InitializeStream(sampleSource, looping, fileIdentifier),
			};
		}

		/// <summary>
		/// Gets a streamed sound effect from one of the registered custom audio formats
		/// </summary>
		/// <param name="sampleSource">The stream where the samples will be read from. It is expected to contain a full audio file's data</param>
		/// <param name="looping">Whether the sound should loop</param>
		/// <param name="state">Extra information for the format to use</param>
		public static StreamPackage GetStreamedSound(Stream sampleSource, bool looping, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return StreamManager.TryInitializeCustomStream(sampleSource, looping, state);
		}

		/// <summary>
		/// Gets a streamed sound effect using a custom audio format
		/// </summary>
		/// <param name="sampleSource">The stream where the samples will be read from. It is expected to contain a full audio file's data</param>
		/// <param name="format">An object representing how the format will decode the audio stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		/// <param name="state">Extra information for the format to use</param>
		public static StreamPackage GetStreamedSound(Stream sampleSource, CustomAudioFormat format, bool looping, object state) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return StreamManager.InitializeCustomStream(sampleSource, format, looping, state);
		}

		/// <summary>
		/// Gets a streamed sound effect from an XACT wave bank
		/// </summary>
		/// <param name="soundBankPath">The path to the sound bank</param>
		/// <param name="waveBankPath">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedXACTSound(string soundBankPath, string waveBankPath, string cueName, bool looping) {
			MonoSoundLibrary.ThrowIfNotInitialized();

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
		public static StreamPackage GetStreamedXACTSound(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool looping) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			return StreamManager.InitializeXWBStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, looping);
		}

		/// <summary>
		/// Returns whether a streaming package is currently streaming
		/// </summary>
		/// <param name="package">The audio stream</param>
		public static bool IsStreaming([MaybeNullWhen(false)] StreamPackage package) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			if (package is null || package.Disposed)
				return false;

			return StreamManager.IsStreamActive(package);
		}

		/// <summary>
		/// Stops the streamed sound, disposes it, removes it from the tracked streams collection and then sets <paramref name="instance"/> to <see langword="null"/>
		/// </summary>
		/// <param name="instance">The stream instance</param>
		public static void FreeStreamedSound(ref StreamPackage instance) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			StreamManager.StopStreamingSound(ref instance);
		}

		/// <inheritdoc cref="FreeStreamedSound(ref StreamPackage)"/>
		public static void FreeStreamedSound<T>(ref T instance) where T : StreamPackage {
			MonoSoundLibrary.ThrowIfNotInitialized();

			// Need to redirect to a local since "ref T" can't be converted to "ref StreamPackage"
			StreamPackage redirect = instance;
			StreamManager.StopStreamingSound(ref redirect);
			instance = null;
		}

		/// <summary>
		/// Stops the streamed sound, disposes it, removes it from the tracked streams collection and then sets <paramref name="instance"/> to <see langword="null"/>
		/// </summary>
		/// <param name="instance">The stream instance</param>
		[Obsolete("Will be removed in a future update")]
		public static void FreeStreamedSound(ref SoundEffectInstance instance) {
			MonoSoundLibrary.ThrowIfNotInitialized();

			StreamManager.StopStreamingSound(ref instance);
		}
	}
}
