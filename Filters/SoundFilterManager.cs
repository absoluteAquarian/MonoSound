using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Filters.Instances;
using System;
using System.Collections.Generic;
using System.IO;

namespace MonoSound.Filters{
	internal static class SoundFilterManager{
		public static int Max_Filters_Loaded = 200;
		private static List<FilterPackage> filters;

		public static void Init(){
			filters = new List<FilterPackage>();
		}

		public static void DeInit(){
			filters?.Clear();
			filters = null;
		}

		public static void Clear() => filters?.Clear();

		private static bool HasFilteredSFX(string path, Filter filter, out FilterPackage package){
			foreach(FilterPackage p in filters){
				if(p.asset == path && p.filterID == filter.ID){
					package = p;
					return true;
				}
			}

			package = null;
			return false;
		}

		public static SoundEffect CreateFilteredSFX(string path, Filter filter){
			FormatWav wav;
			PCMData metaData;

			string extension = Path.GetExtension(path);
			if(!Path.HasExtension(path) || (extension != ".wav" && extension != ".xnb"))
				throw new ArgumentException($"The given path did not contain a valid extension: {extension}", "path");

			if(HasFilteredSFX(path, filter, out FilterPackage package))
				return package.effect;

			if(Path.GetExtension(path) != ".wav"){
				string xnbPath = Path.ChangeExtension(path, ".xnb");

				if(HasFilteredSFX(xnbPath, filter, out package))
					return package.effect;

				byte[] data = Decompressor.DecompressSoundEffectXNB(xnbPath, out metaData, out byte[] header);

				path = xnbPath;

				wav = FormatWav.FromDecompressorData(data, header);
			}else{
				wav = FormatWav.FromFile(path);

				float duration = (int)((float)wav.DataLength / wav.ByteRate);

				//Ignore the loop fields because they probably aren't that important
				metaData = new PCMData(){
					bitsPerSample = wav.BitsPerSample,
					channels = (AudioChannels)wav.ChannelCount,
					duration = (int)(duration * 1000),
					sampleRate = wav.SampleRate
				};
			}

			FilterPackage cache = new FilterPackage(){
				asset = path,
				type = filter.type,
				metaData = metaData,
				filterID = filter.ID
			};

			if(FilterSimulations.SimulateFilter(wav, filter)){
				SoundEffect effect = new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);

				cache.effect = effect;

				while(filters.Count >= Max_Filters_Loaded)
					filters.RemoveAt(0);

				filters.Add(cache);

				return effect;
			}

			return null;
		}
	}
}
