using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace MonoSound.Streaming {
	internal static class StreamManager {
		private static Dictionary<string, StreamPackage> streams = new Dictionary<string, StreamPackage>();
		private static readonly object modifyLock = new object();

		private static int StreamSourceCreationIndex;

		public static StreamPackage InitializeStream(string path, bool loopedSound, AudioType type) {
			StreamPackage package = type switch {
				AudioType.XNB => new XnbStream(path),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(path),
				AudioType.OGG => new OggStream(path),
				AudioType.MP3 => new Mp3Stream(path),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			string name = GetSafeName(path);

			InitPackage(package, loopedSound, name);

			return streams[name];
		}

		public static StreamPackage InitializeStream(Stream sampleSource, bool loopedSound, AudioType type) {
			StreamPackage package = type switch {
				AudioType.XNB => new XnbStream(sampleSource),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(sampleSource),
				AudioType.OGG => new OggStream(sampleSource),
				AudioType.MP3 => new Mp3Stream(sampleSource),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			string name = sampleSource is FileStream fs ? GetSafeName(fs.Name) : "$streamed_" + StreamSourceCreationIndex++;

			InitPackage(package, loopedSound, name);

			return streams[name];
		}

		public static StreamPackage TryInitializeCustomStream(string path, bool loopedSound) {
			string extension = Path.GetExtension(path);

			if (MonoSound.registeredFormats.TryGetValue(extension, out var format)) {
				StreamPackage package = format.readStreamed(TitleContainer.OpenStream(path));

				InitPackage(package, loopedSound, path);

				return package;
			}

			return null;
		}

		public static StreamPackage TryInitializeCustomStream(Stream stream, bool loopedSound) {
			foreach (var format in MonoSound.registeredFormats.Values) {
				long pos = stream.Position;

				StreamPackage package = format.readStreamed(stream);

				if (package != null) {
					string name = stream is FileStream fs ? GetSafeName(fs.Name) : "$streamed_" + StreamSourceCreationIndex++;

					InitPackage(package, loopedSound, name);

					return package;
				}

				stream.Position = pos;
			}

			return null;
		}

		internal static void InitPackage(StreamPackage package, bool loopedSound, string packageName) {
			package.IsLooping = loopedSound;

			lock (modifyLock) {
				streams.Add(packageName, package);
			}
		}

		public static StreamPackage InitializeXWBStream(string soundBankPath, string waveBankPath, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankPath, waveBankPath, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			lock (modifyLock) {
				streams.Add(name, package);
			}

			return streams[name];
		}

		public static StreamPackage InitializeXWBStream(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool loopedSound) {
			StreamPackage package = new WavebankStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName) {
				IsLooping = loopedSound
			};

			string name = GetSafeName(cueName);
			lock (modifyLock) {
				streams.Add(name, package);
			}

			return streams[name];
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

		[Obsolete("Will be removed in a future update")]
		public static void StopStreamingSound(ref SoundEffectInstance instance) {
			lock (modifyLock) {
				string package = null;
				foreach (var stream in streams) {
					if (object.ReferenceEquals(instance, stream.Value.PlayingSound)) {
						package = stream.Key;
						break;
					}
				}

				if (package != null) {
					streams[package].PlayingSound.Stop();
					streams[package].Dispose();
					streams.Remove(package);

					instance = null;
				}
			}
		}

		public static void StopStreamingSound(ref StreamPackage instance) {
			lock (modifyLock) {
				string package = null;
				foreach (var stream in streams) {
					if (object.ReferenceEquals(instance, stream.Value)) {
						package = stream.Key;
						break;
					}
				}

				if (package != null) {
					streams[package].PlayingSound.Stop();
					streams[package].Dispose();
					streams.Remove(package);

					instance = null;
				}
			}
		}

		private static CancellationToken token;

		internal static void HandleStreaming(object state) {
			Stopwatch watch = new Stopwatch();
			watch.Start();

			token = MonoSound.GetCancellationToken();

			try {
				while (true) {
					lock (modifyLock) {
						streams.Values.AsParallel().AsUnordered().ForAll(stream => {
							//If the stream has stopped before the sound has finished streaming, reset the counters and stream
							if (stream.PlayingSound.State == SoundState.Stopped && stream.SecondsRead > 0 && !stream.IsLooping && !stream.FinishedStreaming)
								stream.Reset();
						});
					}
				}
			} catch when (token.IsCancellationRequested) {
				// Free the streams
				foreach (var stream in streams.Values)
					stream.Dispose();

				streams = null;
			}
		}
	}
}
