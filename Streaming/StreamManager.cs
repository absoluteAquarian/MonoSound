using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MonoSound.Streaming{
	internal static class StreamManager{
		private static Dictionary<string, StreamPackage> streams = new Dictionary<string, StreamPackage>();
		private static readonly object modifyLock = new object();

		private static int StreamSourceCreationIndex;

		public static SoundEffectInstance InitializeStream(string path, bool loopedSound, AudioType type){
			StreamPackage package = type switch{
				AudioType.XNB => new XnbStream(path),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(path),
				AudioType.OGG => new OggStream(path),
				AudioType.MP3 => new Mp3Stream(path),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			package.looping = loopedSound;

			string name = GetSafeName(path);
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		public static SoundEffectInstance InitializeStream(Stream sampleSource, bool loopedSound, AudioType type){
			StreamPackage package = type switch{
				AudioType.XNB => new XnbStream(sampleSource),
				AudioType.XWB => throw new ArgumentException("XWB streams should be initialized via StreamManager.InitializeXWBStream()"),
				AudioType.WAV => new WavStream(sampleSource),
				AudioType.OGG => new OggStream(sampleSource),
				AudioType.MP3 => new Mp3Stream(sampleSource),
				_ => throw new ArgumentException("Unknown stream type requested")
			};

			package.looping = loopedSound;

			string name = sampleSource is FileStream fs ? GetSafeName(fs.Name) : "$streamed_" + StreamSourceCreationIndex++;
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		public static SoundEffectInstance InitializeXWBStream(string soundBankPath, string waveBankPath, string cueName, bool loopedSound){
			StreamPackage package = new WavebankStream(soundBankPath, waveBankPath, cueName){
				looping = loopedSound
			};

			string name = GetSafeName(cueName);
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		public static SoundEffectInstance InitializeXWBStream(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool loopedSound){
			StreamPackage package = new WavebankStream(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName){
				looping = loopedSound
			};

			string name = GetSafeName(cueName);
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		private static string GetSafeName(string original){
			//This method allows multiple streamed sounds from the same file to be present in "streams" at the same time
			if(!streams.ContainsKey(original))
				return original;

			//Isolate the file name
			original = Path.ChangeExtension(Path.GetFileName(original), null);
			
			//Keep iterating until a valid replacement is found
			int i = 0;
			while(streams.ContainsKey(original + ++i));

			return original + i;
		}

		public static void StopStreamingSound(ref SoundEffectInstance instance){
			lock(modifyLock){
				string package = null;
				foreach(var stream in streams){
					if(object.ReferenceEquals(instance, stream.Value.sfx)){
						package = stream.Key;
						break;
					}
				}

				if(package != null){
					streams[package].sfx.Stop();
					streams[package].Dispose();
					streams.Remove(package);

					updateKeys = true;

					instance = null;
				}
			}
		}

		private static bool updateKeys = true;
		private static string[] keys;

#pragma warning disable IDE0060
		internal static void HandleStreaming(object state){
#pragma warning restore IDE0060
			Stopwatch watch = new Stopwatch();
			watch.Start();

			while(true){
				lock(modifyLock){
					//Keep asking if we should stop the streams to allow the sound engines to stop early enough before the hardware is lost
					if(stopStreaming){
						foreach(var stream in streams.Values)
							stream.Dispose();

						streams = null;

						return;
					}

					UpdateKeys();
				
					if(keys != null){
						for(int i = 0; i < keys.Length; i++){
							StreamPackage stream = streams[keys[i]];

							//If the stream has stopped before the sound has finished streaming, reset the counters and stream
							if(stream.sfx.State == SoundState.Stopped && stream.secondsRead > 0 && !stream.looping && !stream.FinishedStreaming){
								stream.Reset();
								continue;
							}
						}
					}
				}
			}
		}

		private static void UpdateKeys(){
			if(updateKeys){
				keys = new string[streams.Keys.Count];
				streams.Keys.CopyTo(keys, 0);

				updateKeys = false;
			}
		}

		private static bool stopStreaming;

		public static void SignalStop() => stopStreaming = true;
	}
}
