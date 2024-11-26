using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MonoSound.FFT {
	internal enum FFTGraphRenderMode {
		Static,
		DecayOverTime
	}

	internal abstract class FFTGraph {
		protected double[] _frequencyPoints;
		protected double[] _magnitudes;

		private FFTGraphRenderMode _mode;
		private double _decayFactor;

		public abstract void Populate(FFTQuery query);

		public void Clear() {
			_frequencyPoints = null;
			_magnitudes = null;
		}

		public void SetToStaticRenderMode() {
			_mode = FFTGraphRenderMode.Static;
			_decayFactor = -1;
		}

		public void SetToDecayRenderMode(double decayFactor) {
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(decayFactor, 0);
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(decayFactor, 1);

			_mode = FFTGraphRenderMode.DecayOverTime;
			_decayFactor = decayFactor;
		}

		public IEnumerable<FFTGraphPoint> ExtractAxesData(double time) {
			if (_frequencyPoints is null || _magnitudes is null)
				yield break;

			int length = _frequencyPoints.Length;

			for (int i = 0; i < length; i++) {
				double magnitude = _magnitudes[i];

				if (_mode == FFTGraphRenderMode.DecayOverTime)
					magnitude *= Math.Pow(_decayFactor, time);

				yield return new FFTGraphPoint(_frequencyPoints[i], magnitude);
			}
		}
	}

	internal sealed class FFTGraph<T> : FFTGraph where T : IFFTFrequencyGraph<T> {
		public override void Populate(FFTQuery query) {
			if (query is null || query.Active || !query.TryGetFFT(out Complex[] fftBuffer)) {
				// Query is still processing or the FFT data is not yet available
				_frequencyPoints = null;
				_magnitudes = null;
				return;
			}

			/*
			int length = fftBuffer.Length;
			int halfLength = fftBuffer.Length / 2 + 1;
			ref Complex fft = ref fftBuffer[0];

			_magnitudes = new double[halfLength];
			ref double magnitude = ref _magnitudes[0];

			// First point (DC component) is not doubled
			magnitude = T.ModifyMagnitude(fft.Magnitude, length);
			magnitude = ref Unsafe.Add(ref magnitude, 1);
			fft = ref Unsafe.Add(ref fft, 1);

			// Subsequent points are doubled to account for both positive and negative frequencies
			for (int i = 1; i < halfLength; i++, magnitude = ref Unsafe.Add(ref magnitude, 1), fft = ref Unsafe.Add(ref fft, 1))
				magnitude = T.ModifyMagnitude(2 * fft.Magnitude, length);
			
			_frequencyPoints = FFTOperations.GetFrequencyPoints(query.sampleRate, fftBuffer.Length, out _resolution);
			*/

			List<double> frequencies = [];
			List<double> values = [];

			// From: https://github.com/jiwoojang/AudioVisualizer/blob/master/AudioVis/AudioObject.cpp#L170
			
		//	float maxSampleIndex = Math.Min(query.samples.Length / 2f, 20000f);
			float maxSampleIndex = fftBuffer.Length / 2f;
			double logMax = Math.Log(maxSampleIndex);

			for (float i = 1; i < maxSampleIndex; i *= 1.05f) {
				double x = Math.Log(i) / logMax;
				double y = T.ModifyMagnitude(fftBuffer[(int)i].Magnitude);

				frequencies.Add(x);
				values.Add(y);
			}

			/*
			int halfLength = fftBuffer.Length / 2;  // Graph ends up being mirrored, so we only need to show half of the data
			ref Complex fft = ref fftBuffer[halfLength];

			for (int i = halfLength; i < fftBuffer.Length; i++, fft = ref Unsafe.Add(ref fft, 1)) {
				double x = (double)(i - halfLength) / halfLength;
				double y = T.ModifyMagnitude(2 * fft.Magnitude);

				frequencies.Add(x);
				values.Add(y);
			}
			*/

			_frequencyPoints = [.. frequencies];
			_magnitudes = [.. values];
		}
	}

	/// <summary>
	/// A structure representing a point on an FFT graph
	/// </summary>
	public readonly record struct FFTGraphPoint(double Frequency, double Value);

	internal readonly struct DecibelGraph : IFFTFrequencyGraph<DecibelGraph> {
	//	public static double ModifyMagnitude(double magnitude, int fftLength) => 20 * Math.Log10(magnitude / fftLength);
		public static double ModifyMagnitude(double magnitude) {
		//	double mag = -20 * Math.Log(magnitude);
		//	return mag < 0 ? mag : 0;
			double mag = 20 * Math.Log(magnitude);
			return mag;
		}
	}

	internal readonly struct RootMeanSquareGraph : IFFTFrequencyGraph<RootMeanSquareGraph> {
	//	public static double ModifyMagnitude(double magnitude, int fftLength) => magnitude / fftLength;
		public static double ModifyMagnitude(double magnitude) => magnitude;
	}
}
