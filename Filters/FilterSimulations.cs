using Microsoft.Xna.Framework.Audio;
using MonoSound.Audio;
using MonoSound.Default;
using System;
using System.Diagnostics;
using System.IO;

namespace MonoSound.Filters {
	internal static unsafe class FilterSimulations {
		public static SoundEffect CreateFilteredSFX(string path, SoLoudFilterInstance instance) => ApplyFilterTo(FormatWav.FromFile(path), path, instance);

		public static SoundEffect CreateFilteredSFX(string path, params SoLoudFilterInstance[] filters) => ApplyFiltersTo(FormatWav.FromFile(path), path, filters);

		public static SoundEffect ApplyFilterTo(FormatWav wav, string fileIdentifier, SoLoudFilterInstance instance) {
			try {
				SimulateFilter(instance, wav);
			} catch (Exception ex) {
				// Swallow any exceptions
				Debug.WriteLine($"An exception was thrown when trying to apply a {instance.Parent.GetType().FullName} filter.");
				Debug.WriteLine($"Asset: {fileIdentifier}");
				Debug.WriteLine(ex);
				return null;
			}

			if (Controls.LogFilters)
				LogModifiedAudio(wav, fileIdentifier);

			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		public static SoundEffect ApplyFiltersTo(FormatWav wav, string fileIdentifier, params SoLoudFilterInstance[] filters) {
			int index = 0;
			try {
				foreach (SoLoudFilterInstance filter in filters) {
					SimulateFilter(filter, wav);
					index++;
				}
			} catch (Exception ex) {
				// Swallow any exceptions
				SoLoudFilterInstance badFilter = filters[index];
				Debug.WriteLine($"An exception was thrown when trying to apply a {badFilter.Parent.GetType().FullName} filter.");
				Debug.WriteLine($"Asset: {fileIdentifier}");
				Debug.WriteLine(ex);
				return null;
			}

			if (Controls.LogFilters)
				LogModifiedAudio(wav, fileIdentifier);

			return new SoundEffect(wav.GetSoundBytes(), wav.SampleRate, (AudioChannels)wav.ChannelCount);
		}

		private static void LogModifiedAudio(FormatWav wav, string path) {
			try {
				// Check that the path is a valid path
				string directory = Controls.LogDirectory;
				string dummy = Path.GetFullPath(directory);

				// Save the file
				wav.SaveToFile(Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + $"_filtered_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.wav"));
			} catch (Exception ex) {
				throw new InvalidOperationException("Logging directory was set to an invalid path.", ex);
			}
		}

		public static void SimulateFilter(SoLoudFilterInstance filter, FormatWav wav) {
			int channelCount = wav.ChannelCount;
			int sampleRate = wav.SampleRate;

			float[] samples;

			if (filter is EchoFilterInstance echo) {
				samples = wav.DeconstructToFloatSamples();

				// Due to the echo filter causing repetitions, a buffer of nothing will be used
				// This will let the full echo sound play
				double time = EchoTimeStretchFactor(targetVolume: 0.075f, echo.paramDecay, echo.paramDelay);
				Array.Resize(ref samples, samples.Length + (int)(wav.ByteRate * time) / wav.ChannelCount * wav.ChannelCount);

				samples = FormatWav.UninterleaveSamples(samples, wav.ChannelCount);
			} else {
				// Perform both steps simultaneously
				samples = wav.DeconstructAndUninterleaveSamples();
			}

			double currentTime = 0d;
			SimulateOneFilter(filter, samples, channelCount, sampleRate, ref currentTime);

			wav.ReconstructFromSamples(FormatWav.InterleaveSamples(samples, wav.ChannelCount));
		}

		public static void SimulateOneFilter(SoLoudFilterInstance filter, ref float[] interleavedSamples, int channelCount, int sampleRate, ref double currentTime) {
			float[] samples = FormatWav.UninterleaveSamples(interleavedSamples, channelCount);

			SimulateOneFilter(filter, samples, channelCount, sampleRate, ref currentTime);

			interleavedSamples = FormatWav.InterleaveSamples(samples, channelCount);
		}

		public static void SimulateOneFilter(SoLoudFilterInstance filter, Span<float> uninterleavedSamples, int channelCount, int sampleRate, ref double currentTime) {
			const double FADER_UPDATE_RATE = 1 / 50d;  // 50 updates per second

			int channelSize = uninterleavedSamples.Length / channelCount;

			int sampleCountForNextUpdate = Math.Min((int)Math.Ceiling(sampleRate * FADER_UPDATE_RATE), channelSize);  // Process at least one update

			if (filter.pendingReset) {
				filter.pendingReset = false;
				filter.ResetFilterState();
			}

			filter.BeginFiltering(channelCount, channelSize, sampleRate);

			for (int i = 0; i < channelSize;) {
				// Update the faders
				filter.UpdateParameterFaders(currentTime);

				// Apply the filter to the samples
				filter.ApplyFilteringToAllChannels(uninterleavedSamples, i, sampleCountForNextUpdate, channelCount, channelSize, sampleRate);

				filter.MarkNoneChanged();

				double deltaTime = sampleCountForNextUpdate / (double)sampleRate;
				double oldTime = currentTime;
				currentTime += deltaTime;
				// Adjust the next slice to keep the update rate consistent
				int nextSampleStart = i + sampleCountForNextUpdate;
				double timeError = FADER_UPDATE_RATE - (currentTime - oldTime);
				sampleCountForNextUpdate = (int)Math.Ceiling(sampleRate * (FADER_UPDATE_RATE + timeError));

				// The last iteration may have fewer samples than the rest
				sampleCountForNextUpdate = Math.Min(sampleCountForNextUpdate, channelSize - nextSampleStart);

				i = nextSampleStart;
			}
		}

		private static double EchoTimeStretchFactor(float targetVolume, float decayFactor, float delayFactor) {
			//Calculate how many iterations it will take to get to the final volume
			//(decay)^x = (target)     x = log(decay, target)
			double decayIterations = Math.Log(targetVolume, decayFactor);

			//Then calculate the time it would take based on the above and the (delay) factor
			double time = decayIterations * delayFactor;

			if (!Controls.AllowEchoOversampling && time > 30d)
				throw new Exception($"Echo filter contained parameters which would cause MonoSound to generate over 30 seconds' worth of samples.\nDelay: {delayFactor:N3}s, Decay: {decayFactor:N3}x");

			return time;
		}
	}
}
