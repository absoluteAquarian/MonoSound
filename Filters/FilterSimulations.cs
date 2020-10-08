using MonoSound.Audio;
using MonoSound.Filters.Instances;
using System;

namespace MonoSound.Filters{
	internal static unsafe class FilterSimulations{
		public static BiquadResonantFilter bqrFilter;

		public static bool SimulateFilter(FormatWav wav, Filter filter){
			if(wav.ChannelCount != 1)
				throw new InvalidOperationException("Source WAV data contained invalid data. (Channels)");

			wav.DeconstructToFloatSamples(out float[] samples);

			switch(filter.type){
				case SoundFilterType.LowPass:
				case SoundFilterType.BandPass:
				case SoundFilterType.HighPass:
					//Need to make sure that any old handles don't stick around
					if(bqrFilter?.ID != filter.ID || bqrFilter.type != filter.type){
						bqrFilter?.Free();
						bqrFilter = filter as BiquadResonantFilter;
					}else
						bqrFilter.Reset();

					fixed(float* buffer = samples){
						bqrFilter.filter(buffer, (uint)samples.Length, 1, wav.SampleRate, 0);
					}
					break;
				default:
					return false;
			}

			wav.ReconstructFromFloatSamples(samples);
			return true;
		}
	}
}
