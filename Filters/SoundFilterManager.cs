using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters.Instances;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MonoSound.Filters{
	internal static class SoundFilterManager{
		public static int Max_Filters_Loaded = 200;
		private static List<FilterPackage> filters;

		private static readonly string[] validExtensions = new string[]{ ".xnb", ".wav", ".ogg", ".mp3" };

		public static void Init(){
			filters = new List<FilterPackage>();
		}

		public static void DeInit(){
			filters?.Clear();
			filters = null;
		}

		public static void Clear() => filters?.Clear();

		private static bool HasFilteredSFX(string path, out FilterPackage package, params Filter[] filtersToCheck){
			foreach(FilterPackage p in filters){
				bool allMatch = true;

				if(p.asset != path)
					continue;

				foreach(Filter f in filtersToCheck){
					if(!p.filterIDs.Contains(f.ID)){
						allMatch = false;
						break;
					}
				}

				if(allMatch){
					package = p;
					return true;
				}
			}

			package = null;
			return false;
		}

		private static void ThrowIfPathHasNoValidExtension(string path, out string extension){
			extension = Path.GetExtension(path);

			foreach(string ex in validExtensions)
				if(ex == extension)
					return;

			//If we've reached this line, either the file had an extension and it wasn't a valid one or the file didn't have an extension
			throw new ArgumentException($"The given path did not contain a valid extension: {extension}", "path");
		}

		internal static void GetWavAndMetadata(string path, out FormatWav wav, out PCMData metaData){
			wav = null;
			metaData = default;

			ThrowIfPathHasNoValidExtension(path, out string extension);

			switch(extension){
				case ".xnb":
					byte[] data = Decompressor.DecompressSoundEffectXNB(path, out metaData, out byte[] header);

					wav = FormatWav.FromDecompressorData(data, header);
					break;
				case ".wav":
					//Could've jumped here from the ".ogg" case.  Don't try and set the 'wav' variable if it was already set
					if(wav is null)
						wav = FormatWav.FromFileWAV(path);

					float duration = (int)((float)wav.DataLength / wav.ByteRate);

					//Ignore the loop fields because they probably aren't that important
					metaData = new PCMData(){
						bitsPerSample = wav.BitsPerSample,
						channels = (AudioChannels)wav.ChannelCount,
						duration = (int)(duration * 1000),
						sampleRate = wav.SampleRate
					};
					break;
				case ".ogg":
					wav = FormatWav.FromFileOGG(path);
					goto case ".wav";
				case ".mp3":
					wav = FormatWav.FromFileMP3(path);
					goto case ".wav";
				default:
					throw new InvalidOperationException("Path contained an invalid extension");
			}
		}

		public static SoundEffect CreateFilteredSFX(string path, params Filter[] filtersToApply){
			if(filtersToApply.Length == 0)
				throw new ArgumentException("Filters list was empty.", "filtersToApply");

			if(HasFilteredSFX(path, out FilterPackage package, filtersToApply))
				return package.effect;

			GetWavAndMetadata(path, out var wav, out var metaData);

			return ApplyFilters(wav, path, metaData, filtersToApply);
		}

		public static SoundEffect CreateBankFilteredSFX(FormatWav wav, string name, params Filter[] filtersToApply){
			float duration = (int)((float)wav.DataLength / wav.ByteRate);
			PCMData data = new PCMData(){
				bitsPerSample = wav.BitsPerSample,
				channels = (AudioChannels)wav.ChannelCount,
				duration = (int)(duration * 1000),
				sampleRate = wav.SampleRate
			};

			return ApplyFilters(wav, name, data, filtersToApply);
		}

		private static SoundEffect ApplyFilters(FormatWav wav, string path, PCMData metaData, Filter[] filtersToApply){
			FilterPackage cache = new FilterPackage(){
				asset = path,
				types = GetFilterTypes(filtersToApply),
				metaData = metaData,
				filterIDs = GetFilterIDs(filtersToApply)
			};

			bool success = false;
			foreach(Filter f in filtersToApply){
				success = FilterSimulations.SimulateFilter(wav, f);

				if(!success)
					return null;
			}

			if(MonoSoundManager.LogFilters){
				try{
					//Check that the path is a valid path
					string directory = MonoSoundManager.LogDirectory;
					string dummy = Path.GetFullPath(directory);

					//Get the new file name
					string file = Path.GetFileNameWithoutExtension(path);
					file += GetFileNameExtra(cache.types);
					file += ".wav";

					//Save the file
					wav.SaveToFile(Path.Combine(directory, file));
				}catch(Exception ex){
					throw new InvalidOperationException("Logging directory was set to an invalid path.", ex);
				}
			}

			if(success){
				SoundEffect effect = new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);

				cache.effect = effect;

				while(filters.Count >= Max_Filters_Loaded)
					filters.RemoveAt(0);

				filters.Add(cache);

				return effect;
			}

			return null;
		}

		private static SoundFilterType[] GetFilterTypes(Filter[] filtersToProcess){
			//LINQ bad
			SoundFilterType[] types = new SoundFilterType[filtersToProcess.Length];
			for(int i = 0; i < filtersToProcess.Length; i++)
				types[i] = filtersToProcess[i].type;
			return types;
		}

		private static int[] GetFilterIDs(Filter[] filtersToProcess){
			//LINQ bad
			int[] ids = new int[filtersToProcess.Length];
			for(int i = 0; i < filtersToProcess.Length; i++)
				ids[i] = filtersToProcess[i].ID;
			return ids;
		}

		private static string GetFileNameExtra(SoundFilterType[] types){
			string ret = " - ";
			for(int i = 0; i < types.Length; i++)
				ret += $"{types[i]}|";
			ret = ret.Substring(0, ret.Length - 1);
			return ret;
		}
	}
}
