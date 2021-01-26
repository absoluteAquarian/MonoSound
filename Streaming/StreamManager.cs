using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MonoSound.Streaming{
	internal static class StreamManager{
		private static Dictionary<string, StreamPackage> streams = new Dictionary<string, StreamPackage>();
		private static readonly object modifyLock = new object();

		public static SoundEffectInstance InitializeXNBStream(string path, bool loopedSound){
			StreamPackage package = new StreamPackage(path, StreamType.XNB){
				looping = loopedSound
			};

			string name = GetSafeName(path);
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		public static SoundEffectInstance InitializeWAVStream(string path, bool loopedSound){
			StreamPackage package = new StreamPackage(path, StreamType.WAV){
				looping = loopedSound
			};

			string name = GetSafeName(path);
			lock(modifyLock){
				streams.Add(name, package);

				updateKeys = true;
			}

			return streams[name].sfx;
		}

		public static SoundEffectInstance InitializeXWBStream(string soundBankPath, string waveBankPath, string cueName, bool loopedSound){
			StreamPackage package = new StreamPackage(soundBankPath, waveBankPath, cueName){
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
		public static void HandleStreaming(object state){
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
