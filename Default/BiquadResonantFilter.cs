/*
SoLoud audio engine
Copyright (c) 2013-2020 Jari Komppa

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
  claim that you wrote the original software. If you use this software
  in a product, an acknowledgment in the product documentation would be
  appreciated but is not required.

  2. Altered source versions must be plainly marked as such, and must not be
  misrepresented as being the original software.

  3. This notice may not be removed or altered from any source
  distribution.
*/

using MonoSound.Filters;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace MonoSound.Default {
	/// <summary>
	/// A sound filter for Low Pass, Band Pass, and High Pass filtering.  Reduces the amplitude of frequencies not within close proximity to the set frequency.
	/// </summary>
	public class BiquadResonantFilter : SoLoudFilter<BiquadResonantFilterInstance> {
		/// <inheritdoc cref="SoundFilterType.LowPass"/>
		public const int LOW_PASS = 0;
		/// <inheritdoc cref="SoundFilterType.BandPass"/>
		public const int BAND_PASS = 1;
		/// <inheritdoc cref="SoundFilterType.HighPass"/>
		public const int HIGH_PASS = 2;

		/// <summary>
		/// Creates a new <see cref="BiquadResonantFilter"/> filter with the specified parameters
		/// </summary>
		/// <param name="strength">
		/// The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.
		/// </param>
		/// <param name="type">
		/// The type of Biquad Resonant filter to use: Low Pass, High Pass, or Band Pass.<br/>
		/// <i>Low Pass</i> reduces the amplitude of higher frequencies than the set frequency.<br/>
		/// <i>High Pass</i> reduces the amplitude of lower frequencies than the set frequency.<br/>
		/// <i>Band Pass</i> reduces the amplitude of frequencies not within close proximity to the set frequency.<br/>
		/// Defaults to Low Pass.
		/// </param>
		/// <param name="frequency">
		/// The frequency cutoff for the filter.<br/>
		/// <i>Low Pass</i>: frequencies above this value are attenuated.<br/>
		/// <i>High Pass</i>: frequencies below this value are attenuated.<br/>
		/// <i>Band Pass</i>: frequencies not within close proximity to this value are attenuated.<br/>
		/// Range is 10 to 8000 Hz, with a default of 2000 Hz.
		/// </param>
		/// <param name="resonance">
		/// The resonance of the filter.  Low resonance results in a smoother attenuation and more subtle filtering, whereas high resonance results in more aggressive filtering.<br/>
		/// Range is 0.1 to 20, with a default of 2.
		/// </param>
		public BiquadResonantFilter(float strength, int type, float frequency, float resonance) : base(strength) {
			BiquadResonantFilterInstance singleton = Singleton;
			singleton.paramType.Value = type;
			singleton.paramFrequency.Value = frequency;
			singleton.paramResonance.Value = resonance;
		}

		/// <inheritdoc cref="SoLoudFilter.CreateInstance"/>
		protected override SoLoudFilterInstance NewInstance() => new BiquadResonantFilterInstance(this);
	}

	/// <summary>
	/// An instance of a <see cref="BiquadResonantFilter"/> filter
	/// </summary>
	public sealed class BiquadResonantFilterInstance : SoLoudFilterInstance {
		private struct BRQState {
			// float y1, y2, x1, x2;
			// Changed to Vector2 and Vector3 to allow for SIMD optimization
			public Vector2 y;
			public Vector3 x;
		}

		// Filter coefficients
		private BRQState[] channelStates = [];
		// float mA0, mA1, mA2, mB1, mB2;
		private Vector3 a;
		private Vector2 b;
		// float mSamplerate;
		private int lastKnownSampleRate = 44100;  // Default sample rate

		/// <summary>
		/// The type of Biquad Resonant filter to use: Low Pass, High Pass, or Band Pass.<br/>
		/// <i>Low Pass</i> reduces the amplitude of higher frequencies than the set frequency.<br/>
		/// <i>High Pass</i>  reduces the amplitude of lower frequencies than the set frequency.<br/>
		/// <i>Band Pass</i>  reduces the amplitude of frequencies not within close proximity to the set frequency.<br/>
		/// Defaults to Low Pass.
		/// </summary>
		public readonly SoLoudFilter.Parameter<int> paramType;  // SoLoud::BiquadResonantFilter::TYPE, mFilterType
		/// <summary>
		/// The frequency cutoff for the filter.<br/>
		/// <i>Low Pass</i>: frequencies above this value are attenuated.<br/>
		/// <i>High Pass</i>: frequencies below this value are attenuated.<br/>
		/// <i>Band Pass</i>: frequencies not within close proximity to this value are attenuated.<br/>
		/// Range is 10 to 8000 Hz, with a default of 2000 Hz.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramFrequency;  // SoLoud::BiquadResonantFilter::FREQUENCY, mFrequency
		/// <summary>
		/// The resonance of the filter.  Low resonance results in a smoother attenuation and more subtle filtering, whereas high resonance results in more aggressive filtering.<br/>
		/// Range is 0.1 to 20, with a default of 2.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramResonance;  // SoLoud::BiquadResonantFilter::RESONANCE, mResonance

		/// <summary>
		/// Creates a new <see cref="BiquadResonantFilterInstance"/> instance with the specified parent filter
		/// </summary>
		public BiquadResonantFilterInstance(BiquadResonantFilter parent) : base(parent) {
			paramType = CreateParameter(BiquadResonantFilter.LOW_PASS, BiquadResonantFilter.LOW_PASS, BiquadResonantFilter.HIGH_PASS);
			paramFrequency = CreateParameter(2000f, 10f, 8000f);
			paramResonance = CreateParameter(2f, 0.1f, 20f);
			CalculateBQRCoefficients();
		}

		/// <inheritdoc cref="SoLoudFilterInstance.UpdateParameterFaders(double)"/>
		protected internal override void UpdateParameterFaders(double time) {
			base.UpdateParameterFaders(time);
			paramType.UpdateFader(time);
			paramFrequency.UpdateFader(time);
			paramResonance.UpdateFader(time);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.CopyParametersTo"/>
		protected internal override void CopyParametersTo(SoLoudFilterInstance other) {
			base.CopyParametersTo(other);
			BiquadResonantFilterInstance instance = (BiquadResonantFilterInstance)other;
			paramType.CopyTo(instance.paramType);
			paramFrequency.CopyTo(instance.paramFrequency);
			paramResonance.CopyTo(instance.paramResonance);
		}

		private void CalculateBQRCoefficients() {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_biquadresonantfilter.cpp#L37

			float omega = (float)(Math.Tau * paramFrequency / lastKnownSampleRate);
			(float sin_omega, float cos_omega) = MathF.SinCos(omega);
			float alpha = sin_omega / (2.0f * paramResonance);
			float scalar = 1.0f / (1.0f + alpha);

			switch (paramType) {
				case BiquadResonantFilter.LOW_PASS:
					a = new Vector3(1.0f - cos_omega) * new Vector3(0.5f, 1f, 0.5f);
					break;
				case BiquadResonantFilter.BAND_PASS:
					a = new Vector3(alpha) * new Vector3(1f, 0f, -1f);
					break;
				case BiquadResonantFilter.HIGH_PASS:
					a = new Vector3(1f + cos_omega) * new Vector3(0.5f, -1f, 0.5f);
					break;
				default:
					goto case BiquadResonantFilter.LOW_PASS;
			}

			a *= scalar;
			b = new Vector2(-2.0f * cos_omega, 1.0f - alpha) * scalar;
		}

		/// <inheritdoc cref="SoLoudFilterInstance.BeginFiltering"/>
		protected internal override void BeginFiltering(int channelCount, int channelSize, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_biquadresonantfilter.cpp#L106

			if (channelStates.Length < channelCount)
				Array.Resize(ref channelStates, channelCount);

			if (HasAnyParameterChanged || lastKnownSampleRate != sampleRate) {
				lastKnownSampleRate = sampleRate;
				CalculateBQRCoefficients();
			}
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ApplyFilter"/>
		protected internal override void ApplyFilter(Span<float> samples, int channel, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_biquadresonantfilter.cpp#L117

			ref BRQState state = ref channelStates[channel];
			ref Vector3 x = ref state.x;
			ref Vector2 y = ref state.y;

			// make sure we access pairs of samples (one sample may be skipped)
			ref float sample = ref samples[0];

			float wet = paramStrength;

			for (int i = 0; i < samples.Length; i++, sample = ref Unsafe.Add(ref sample, 1)) {
				// Generate outputs by filtering inputs.
				// In order to take advantage of SIMD instructions better, this implementation does not do the variable permutation like SoLoud does

				x.X = sample;
				y.X = Vector3.Dot(a, x) - Vector2.Dot(b, y);
				sample += (y.Y - sample) * wet;

				// Shift the sample memory
				// [ x0, x1, x2 ] -> [ x0, x0, x1 ]  (x0 gets overwritten with the next sample)
				// [ y0, y1 ] -> [ y0, y0 ]  (y0 gets overwritten with the next output)
				x.Z = x.Y;
				x.Y = x.X;
				y.Y = y.X;
			}
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ResetFilterState"/>
		protected internal override void ResetFilterState() {
			channelStates = [];
			CalculateBQRCoefficients();
		}

		/// <inheritdoc cref="SoLoudFilterInstance.Dispose(bool)"/>
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (disposing) {
				paramType.Dispose();
				paramFrequency.Dispose();
				paramResonance.Dispose();
			}

			channelStates = null;
		}
	}
}
