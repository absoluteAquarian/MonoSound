using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoSound.API;
using MonoSound.Audio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MonoSound.Streaming {
	internal static class StreamManager {
		private static ConcurrentDictionary<string, StreamPackage> streams;

		private static int StreamSourceCreationIndex;

		private static Task _workThread;
		private static CancellationToken _workToken;

		public static StreamPackage InitializeStream(string path, bool loopedSound, AudioType type) {
			StreamPackage package = type switch {
				AudioType.XNB => new XnbStream(path),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(path),
				AudioType.OGG => new OggStream(path),
				AudioType.MP3 => new Mp3Stream(path),
				AudioType.Custom => throw new ArgumentException("Custom audio format streams should be initialized via StreamManager.TryInitializeCustomStream()"),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			InitPackage(package, loopedSound, GetSafeName(path));
			return package;
		}

		public static StreamPackage InitializeStream(Stream sampleSource, bool loopedSound, AudioType type) {
			StreamPackage package = type switch {
				AudioType.XNB => new XnbStream(sampleSource),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(sampleSource),
				AudioType.OGG => new OggStream(sampleSource),
				AudioType.MP3 => new Mp3Stream(sampleSource),
				AudioType.Custom => throw new ArgumentException("Custom audio format streams should be initialized via StreamManager.TryInitializeCustomStream()"),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			InitPackage(package, loopedSound, GetStreamName(sampleSource));
			return package;
		}

		public static StreamPackage InitializeCustomStream(string path, CustomAudioFormat format, bool looping, object state) {
			StreamPackage package = format.CreateStream(path, state);
			InitPackage(package, looping, GetSafeName(path));
			return package;
		}

		public static StreamPackage InitializeCustomStream(Stream sampleSource, CustomAudioFormat format, bool looping, object state) {
			StreamPackage package = format.CreateStream(sampleSource, state)
				?? throw new ArgumentException("Audio stream was not valid for the given custom format");
			InitPackage(package, looping, GetStreamName(sampleSource));
			return package;
		}

		public static StreamPackage TryInitializeCustomStream(string path, bool loopedSound, object state) {
			string extension = Path.GetExtension(path);

			if (MonoSoundLibrary.customAudioFormats.TryGetValue(extension, out var audioFormat)) {
				StreamPackage package = audioFormat.CreateStream(path, state);
				InitPackage(package, loopedSound, GetSafeName(path));
				return package;
			}

			// Legacy API
			if (MonoSoundLibrary.registeredFormats.TryGetValue(extension, out var format)) {
				StreamPackage package = format.readStreamed(TitleContainer.OpenStream(path));
				InitPackage(package, loopedSound, GetSafeName(path));
				return package;
			}

			throw new InvalidOperationException($"Extension \"{extension}\" was not recognized by any custom audio formats");
		}

		public static StreamPackage TryInitializeCustomStream(Stream stream, bool loopedSound, object state) {
			return GetCustomAudioPackage(stream, loopedSound, state, MonoSoundLibrary.customAudioFormats, fmt => fmt.CreateStream)
				?? GetCustomAudioPackage(stream, loopedSound, state, MonoSoundLibrary.registeredFormats, fmt => fmt.RedirectReadStreamed)  // Legacy API
				?? throw new InvalidOperationException("Audio stream was not recognized by any custom audio formats");
		}

		private static StreamPackage GetCustomAudioPackage<T>(Stream stream, bool loopedSound, object state, Dictionary<string, T> formats, Func<T, Func<Stream, object, StreamPackage>> packageFactory) {
			foreach (var format in formats.Values) {
				long pos = stream.Position;

				StreamPackage package = packageFactory(format)(stream, state);

				if (package != null) {
					InitPackage(package, loopedSound, GetStreamName(stream));
					return package;
				}

				stream.Position = pos;
			}

			return null;
		}

		private static string GetStreamName(Stream stream) {
			if (stream is FileStream fs)
				return GetSafeName(fs.Name);

			return "$streamed_" + Interlocked.Increment(ref StreamSourceCreationIndex);
		}

		internal static void InitPackage(StreamPackage package, bool loopedSound, string packageName) {
			package.IsLooping = loopedSound;

			streams.AddOrUpdate(packageName, package, (k, s) => s);
		}

		public static StreamPackage InitializeXWBStream(string soundBankPath, string waveBankPath, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankPath, waveBankPath, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			streams.AddOrUpdate(name, package, (k, s) => s);

			return package;
		}

		public static StreamPackage InitializeXWBStream(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			streams.AddOrUpdate(name, package, (k, s) => s);

			return package;
		}

		private static string GetSafeName(string original) {
			//This method allows multiple streamed sounds from the same file to be present in "streams" at the same time
			if (!streams.ContainsKey(original))
				return original;

			//Isolate the file name
			original = Path.ChangeExtension(Path.GetFileName(original), null);

			//Keep iterating until a valid replacement is found
			int i = 0;
			while (streams.ContainsKey(original + ++i)) ;

			return original + i;
		}

		public static bool IsStreamActive(StreamPackage package) {
			foreach (var (_, stream) in streams) {
				if (object.ReferenceEquals(package, stream))
					return true;
			}

			return false;
		}

		[Obsolete("Will be removed in a future update")]
		public static void StopStreamingSound(ref SoundEffectInstance instance) {
			if (instance is null)
				return;
			if (instance.IsDisposed) {
				instance = null;
				return;
			}

			foreach (var (key, stream) in streams) {
				if (object.ReferenceEquals(instance, stream.PlayingSound)) {
					stream.PlayingSound.Stop();
					stream.Dispose();
					streams.TryRemove(key, out _);
					instance = null;
					return;
				}
			}

			instance.Dispose();
			instance = null;
		}

		
		public static void StopStreamingSound(ref StreamPackage instance) {
			if (instance is null)
				return;
			if (instance.Disposed) {
				instance = null;
				return;
			}

			foreach (var (key, stream) in streams) {
				if (object.ReferenceEquals(instance, stream)) {
					streams.TryRemove(key, out _);
					break;
				}
			}

			instance.PlayingSound?.Stop();
			instance.Dispose();
			instance = null;
		}

		internal static void Initialize() {
			streams = new ConcurrentDictionary<string, StreamPackage>();

			_workToken = MonoSoundLibrary.GetCancellationToken();
			_workThread = new Task(HandleStreamBuffering, _workToken, TaskCreationOptions.LongRunning);
			_workThread.Start();

			_canSafelyDestroyStreams = false;
		}

		internal static void Deinitialize() {
			_workThread = null;

			// Wait for the thread to finish
			while (!_canSafelyDestroyStreams)
				Thread.Yield();

			// Free the streams
			foreach (var stream in streams.Values)
				stream.Dispose();

			streams = null;
		}

		private static bool _canSafelyDestroyStreams = true;

		private static void HandleStreamBuffering() {
			try {
				while (true) {
					foreach (var (_, stream) in streams) {
						// Handle the streaming behavior
						if (MonoSoundLibrary.Game is Game game && stream.GetActualFocusBehavior() == StreamFocusBehavior.PauseOnLostFocus && stream.Metrics.State != SoundState.Stopped) {
							if (!game.IsActive) {
								if (!stream.focusPause) {
									// Pause the stream and indicate that it's an automatic pause
									stream.focusPause = true;
									stream.Pause();
								}
							} else if (stream.focusPause) {
								// Resume the stream
								stream.focusPause = false;
								stream.Resume();
							}
						}

						if (stream.Metrics.State == SoundState.Playing)
							stream.PlayingSound.StrobeQueue();
					}

					// After the streams are processed, free the thread time for use by other threads
					Thread.Yield();
				}
			} catch when (_workToken.IsCancellationRequested) {
				// Nothing special to do here
			} finally {
				_canSafelyDestroyStreams = true;
			}
		}
	}
}
