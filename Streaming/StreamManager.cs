using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoSound.API;
using MonoSound.Audio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MonoSound.Streaming {
	internal static class StreamManager {
		private static ConcurrentDictionary<string, StreamPackage> streams;
		private static IEnumerator<KeyValuePair<string, StreamPackage>> _streamEnumerator;

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
			_streamEnumerator = streams.GetEnumerator();
		}

		public static StreamPackage InitializeXWBStream(string soundBankPath, string waveBankPath, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankPath, waveBankPath, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			streams.AddOrUpdate(name, package, (k, s) => s);
			_streamEnumerator = streams.GetEnumerator();

			return package;
		}

		public static StreamPackage InitializeXWBStream(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			streams.AddOrUpdate(name, package, (k, s) => s);
			_streamEnumerator = streams.GetEnumerator();

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
			while (_streamEnumerator.MoveNext()) {
				if (object.ReferenceEquals(package, _streamEnumerator.Current.Value))
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

			while (_streamEnumerator.MoveNext()) {
				var (key, stream) = _streamEnumerator.Current;
				if (object.ReferenceEquals(instance, stream.PlayingSound)) {
					stream.PlayingSound.Stop();
					stream.Dispose();
					streams.TryRemove(key, out _);
					_streamEnumerator = streams.GetEnumerator();
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

			while (_streamEnumerator.MoveNext()) {
				var (key, stream) = _streamEnumerator.Current;
				if (object.ReferenceEquals(instance, stream)) {
					streams.TryRemove(key, out _);
					_streamEnumerator = streams.GetEnumerator();
					break;
				}
			}

			instance.PlayingSound?.Stop();
			instance.Dispose();
			instance = null;
		}

		internal static void Initialize() {
			streams = new ConcurrentDictionary<string, StreamPackage>();
			_streamEnumerator = streams.GetEnumerator();

			_workToken = MonoSoundLibrary.GetCancellationToken();
			_workThread = new Task(HandleStreamBuffering, _workToken, TaskCreationOptions.LongRunning);
			_workThread.Start();
		}

		private static bool _areStreamsDisposed = false;
		private static StreamingThreadState _threadState;

		internal static void Deinitialize() {
			_workThread = null;

			// Ensure that the thread is not currently enumerating the streams
			_threadState.WaitToLock();

			// Free the streams
			foreach (var stream in streams.Values)
				stream.Dispose();

			_areStreamsDisposed = true;

			// Release the lock
			_threadState.Unlock();

			streams = null;
		}


		private static void HandleStreamBuffering() {
			// The CancellationToken will automatically force execution out of this loop when a cancellation is requested
			while (true) {
				// Wait for any locks to be released
				_threadState.WaitForUnlock();

				// Sanity check to ensure that the thread properly exits when the streams are disposed
				if (_areStreamsDisposed) {
					// Enumerator has to be destroyed here instead of Deinitialize() to avoid race conditions
					_streamEnumerator = null;
					break;
				}

				// Copy the object to a local in case the enumerator is recalculated during enumeration
				var enumerator = _streamEnumerator;
				Thread.MemoryBarrier();

				while (enumerator.MoveNext()) {
					var stream = enumerator.Current.Value;

					if (stream?.Metrics?.IsDisposed ?? true)
						continue;

					// Handle the streaming behavior
					if (MonoSoundLibrary.Game is Game game && stream.GetActualFocusBehavior() == StreamFocusBehavior.PauseOnLostFocus && stream.Metrics.State != SoundState.Stopped) {
						if (!game.IsActive) {
							// Pause the stream and indicate that it's an automatic pause
							stream.FocusPause();
						} else {
							// Resume the stream
							stream.FocusResume();
						}
					}

					if (stream.Metrics.State == SoundState.Playing)
						stream.PlayingSound.StrobeQueue();
				}

				// Release the lock
				_threadState.FreeToLock();

				// After the streams are processed, free the thread time for use by other threads
				Thread.Yield();
			}
		}

		private struct StreamingThreadState {
			private int _state;

			private const int WAITING = 0;
			private const int PROCESSING = 1;
			private const int LOCKED = 2;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void WaitForUnlock() {
				while (Interlocked.CompareExchange(ref _state, PROCESSING, WAITING) == LOCKED)
					Thread.Yield();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void FreeToLock() => Interlocked.Exchange(ref _state, WAITING);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void WaitToLock() {
				while (Interlocked.CompareExchange(ref _state, LOCKED, WAITING) == PROCESSING)
					Thread.Yield();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Unlock() => Interlocked.Exchange(ref _state, WAITING);
		}
	}
}
