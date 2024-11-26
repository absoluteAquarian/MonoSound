using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MonoSound.FFT {
	internal static class FFTOperations {
		// Assumes buffer size is a power of two
		public static void FFT(Span<Complex> buffer) {
			// From: https://github.com/swharden/FftSharp/blob/main/src/FftSharp/FftOperations.cs

			/*
			// Initial shuffling of data
			int length = buffer.Length;
			int bitCount = BitOperations.Log2((uint)length);

			for (int i = 1; i < length; i++) {
				int j = ReverseBits(i, bitCount);
				if (j > i)
					(buffer[j], buffer[i]) = (buffer[i], buffer[j]);
			}

			// Perform the FFT
			int halfLength = length / 2;
			for (int i = 1; i <= halfLength; i *= 2) {
				double frequency = -Math.PI / i;
				Complex cosSin = ComplexMath.CosSin(frequency);

				for (int j = 0; j < length; j += i * 2) {
					for (int k = 0; k < i; k++) {
						int even = j + k;
						int odd = j + k + i;
						Complex fft = cosSin * k * buffer[odd];
						buffer[odd] = buffer[even] - fft;
						buffer[even] += fft;
					}
				}
			}
			*/

			// From: https://github.com/jiwoojang/AudioVisualizer/blob/master/AudioVis/AudioObject.cpp#L78

			// DFT
			int N = buffer.Length, k = N, n;
			double thetaT = Math.PI / N;
			Complex phiT = new Complex(Math.Cos(thetaT), -Math.Sin(thetaT)), T;

			while (k > 1) {
				n = k;
				k >>= 1;
				phiT *= phiT;
				T = 1;

				for (int l = 0; l < k; l++) {
					for (int a = l; a < N; a += n) {
						int b = a + k;
						Complex t = buffer[a] - buffer[b];
						buffer[a] += buffer[b];
						buffer[b] = t * T;
					}

					T *= phiT;
				}
			}

			// Decimate
			int m = BitOperations.Log2((uint)N);
			for (int a = 0; a < N; a++) {
				int b = ReverseBits(a, m);

				if (b > a)
					(buffer[b], buffer[a]) = (buffer[a], buffer[b]);
			}

			// Normalize
			Complex f = 1.0 / Math.Sqrt(N);
			for (int i = 0; i < N; i++)
				buffer[i] *= f;
		}

		private static int ReverseBits(int value, int bitCount) => (int)(ReverseUInt((uint)value) >> (32 - bitCount));

		private static uint ReverseUInt(uint value) {
			// From: https://graphics.stanford.edu/~seander/bithacks.html#ReverseParallel
			value = ((value >> 1) & 0x55555555) | ((value & 0x55555555) << 1);
			value = ((value >> 2) & 0x33333333) | ((value & 0x33333333) << 2);
			value = ((value >> 4) & 0x0F0F0F0F) | ((value & 0x0F0F0F0F) << 4);
			value = ((value >> 8) & 0x00FF00FF) | ((value & 0x00FF00FF) << 8);
			return (value >> 16) | (value << 16);
		}

		public static double[] GetFrequencyPoints(int sampleRate, int spectrumLength) {
			// See: https://github.com/swharden/FftSharp/blob/main/src/FftSharp/FFT.cs#L134

			// Only the positive frequencies are used in this implementation
			int length = spectrumLength / 2 + 1;
			double[] points = new double[length];
			ref double point = ref points[0];
			double resolution = (double)sampleRate / (length - 1) / 2;

			// Frequencies start at 0 and go up to the maximum frequency
			for (int i = 0; i < length; i++, point = ref Unsafe.Add(ref point, 1))
				point = i * resolution;

			return points;
		}
	}
}
