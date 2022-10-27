using Microsoft.Xna.Framework.Audio;
using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The central class for loading streamed sounds
	/// </summary>
	public static class StreamLoader {
		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="filePath">The path to the sound file.</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedSound(string filePath, bool looping) {
			MonoSound.ThrowIfNotInitialized();

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
				default:
					instance = StreamManager.TryInitializeCustomStream(filePath, looping);

					if (instance is null)
						throw new InvalidOperationException("Audio stream is not supported by any of the registered custom formats");

					break;
			}

			return instance;
		}

		/// <summary>
		/// Gets a streamed sound effect
		/// </summary>
		/// <param name="sampleSource">The stream where the samples will be read from. It is expected to contain a full audio file's data</param>
		/// <param name="fileIdentifier">An enumeration value indicating what type of audio <paramref name="sampleSource"/> contains.  Cannot be <seealso cref="AudioType.XWB"/></param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedSound(Stream sampleSource, AudioType fileIdentifier, bool looping) {
			MonoSound.ThrowIfNotInitialized();

			if (fileIdentifier == AudioType.XWB)
				throw new ArgumentException("XWB streams must be created via StreamLoader.GetStreamedXACTSound()");

			StreamPackage instance;

			if (fileIdentifier != AudioType.Custom)
				instance = StreamManager.InitializeStream(sampleSource, looping, fileIdentifier);
			else {
				instance = StreamManager.TryInitializeCustomStream(sampleSource, looping);

				if (instance is null)
					throw new InvalidOperationException("Audio stream is not supported by any of the registered custom formats");
			}

			return instance;
		}

		/// <summary>
		/// Gets a streamed sound effect from an XACT wave bank
		/// </summary>
		/// <param name="soundBankPath">The path to the sound bank</param>
		/// <param name="waveBankPath">The path to the wave bank</param>
		/// <param name="cueName">The name of the sound cue to stream</param>
		/// <param name="looping">Whether the sound should loop</param>
		public static StreamPackage GetStreamedXACTSound(string soundBankPath, string waveBankPath, string cueName, bool looping) {
			MonoSound.ThrowIfNotInitialized();

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
			MonoSound.ThrowIfNotInitialized();

			return StreamManager.InitializeXWBStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, looping);
		}

		/// <summary>
		/// Stops streaming for a certain sound effect, stops playing it and then disposes it
		/// </summary>
		/// <param name="instance">The sound effect instance</param>
		public static void FreeStreamedSound(ref StreamPackage instance) {
			MonoSound.ThrowIfNotInitialized();

			StreamManager.StopStreamingSound(ref instance);
		}

		/// <summary>
		/// Stops streaming for a certain sound effect, stops playing it and then disposes it
		/// </summary>
		/// <param name="instance">The sound effect instance</param>
		[Obsolete("Will be removed in a future update")]
		public static void FreeStreamedSound(ref SoundEffectInstance instance) {
			MonoSound.ThrowIfNotInitialized();

			StreamManager.StopStreamingSound(ref instance);
		}
	}
}
