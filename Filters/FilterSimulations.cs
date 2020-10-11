﻿using MonoSound.Audio;
using MonoSound.Filters.Instances;
using System;

namespace MonoSound.Filters{
	internal static unsafe class FilterSimulations{
		public static BiquadResonantFilter bqrFilter;
		public static EchoFilter echFilter;
		public static FreeverbFilter revFilter;

		public static bool SimulateFilter(FormatWav wav, Filter filter){
			if(wav.ChannelCount != 1)
				throw new InvalidOperationException("Source WAV data contained invalid data. (Channels)");

			wav.DeconstructToFloatSamples(out float[] samples);

			switch(filter.type){
				case SoundFilterType.LowPass:
				case SoundFilterType.BandPass:
				case SoundFilterType.HighPass:
					HandleFilter(ref bqrFilter, filter, samples, wav);

					break;
				case SoundFilterType.Echo:
					//Due to the echo filter causing repetitions, a buffer of nothing will be used
					//This will let the full echo sound play
					double time = EchoTimeStretchFactor(wav, 0.075f, filter);
					Array.Resize(ref samples, samples.Length + (int)(wav.ByteRate * time + 1d));

					HandleFilter(ref echFilter, filter, samples, wav);

					break;
				case SoundFilterType.Reverb:
					HandleFilter(ref revFilter, filter, samples, wav);

					break;
				default:
					return false;
			}

			wav.ReconstructFromFloatSamples(samples);
			return true;
		}

		private static double EchoTimeStretchFactor(FormatWav wav, float targetVolume, Filter filter){
			//Calculate how many iterations it will take to get to the final volume
			//(decay)^x = (target)     x = log(decay, target)
			double decayIterations = Math.Log(targetVolume, filter.mParam[EchoFilter.DECAY]);

			//Then calculate the time it would take based on the above and the (delay) factor
			double time = decayIterations * filter.mParam[EchoFilter.DELAY];

			if(!MonoSoundManager.AllowEchoOversampling && time > 30d)
				throw new Exception("Echo filter contained parameters which would cause MonoSound to generate over 30 seconds' worth of samples." +
					$"\nDelay: {filter.mParam[EchoFilter.DELAY] :N3}s, Decay: {filter.mParam[EchoFilter.DECAY] :N3}x");

			return time;
		}

		private static void HandleFilter<T>(ref T filter, Filter newInstance, float[] samples, FormatWav wav) where T : Filter{
			//Need to make sure that any old handles don't stick around
			if(filter?.ID != newInstance.ID || filter.type != newInstance.type){
				filter?.Free();
				filter = newInstance as T;
			}else
				filter.Reset();

			fixed(float* buffer = samples){
				filter.filter(buffer, (uint)samples.Length, 1, wav.SampleRate, 0);
			}
		}
	}
}
