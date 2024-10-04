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

namespace MonoSound.Default {
	/// <summary>
	/// A sound filter that echoes the sound, creating a repeating effect that fades over time that is overlayed on the original sound.
	/// </summary>
	public class EchoFilter : SoLoudFilter<EchoFilterInstance> {
		/// <summary>
		/// Creates a new <see cref="EchoFilter"/> filter with the specified parameters
		/// </summary>
		/// <param name="strength">The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.</param>
		/// <param name="delay">The delay of the echo in seconds, with a minimum of 0.0001 seconds.  Default is 0.3 seconds.</param>
		/// <param name="decay">The decay factor of the echo, with a minimum of 0% and a maximum of 100%.  Default is 70%.</param>
		/// <param name="bias">The influence of earlier echo samples on the resulting echo, with a minimum of 0% and a maximum of 100%.  Default is 0%.</param>
		public EchoFilter(float strength, float delay, float decay, float bias) : base(strength) {
			EchoFilterInstance singleton = Singleton;
			singleton.paramDelay.Value = delay;
			singleton.paramDecay.Value = decay;
			singleton.paramBias.Value = bias;
		}

		/// <inheritdoc cref="SoLoudFilter.CreateInstance"/>
		protected override SoLoudFilterInstance NewInstance() => new EchoFilterInstance(this);
	}

	/// <summary>
	/// An instance of an <see cref="EchoFilter"/> filter
	/// </summary>
	public sealed class EchoFilterInstance : SoLoudFilterInstance {
		private class EchoState(int bufferLength) {
			public float[] buffer = new float[bufferLength];  // mBuffer
			public int offset;  // mOffset
		}

		private EchoState[] channelStates = [];
		private int maxEchoBufferLength = -1;  // mBufferMaxLength

		/// <summary>
		/// The delay of the echo in seconds, with a minimum of 0.0001 seconds.  Default is 0.3 seconds.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramDelay;  // SoLoud::EchoFilter::DELAY, mDelay
		/// <summary>
		/// The decay factor of the echo, with a minimum of 0% and a maximum of 100%.  Default is 70%.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramDecay;  // SoLoud::EchoFilter::DECAY, mDecay
		/// <summary>
		/// The influence of earlier echo samples on the resulting echo, with a minimum of 0% and a maximum of 100%.  Default is 0%.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramBias;  // SoLoud::EchoFilter::FILTER, mFilter

		/// <summary>
		/// Creates a new <see cref="EchoFilterInstance"/> with the specified parent filter
		/// </summary>
		public EchoFilterInstance(EchoFilter filter) : base(filter) {
			paramDelay = CreateParameter(0.3f, 0.0001f, float.MaxValue);
			paramDecay = CreateParameter(0.7f, 0f, 1f);
			paramBias = CreateParameter(0f, 0f, 1f);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.UpdateParameterFaders"/>
		protected internal override void UpdateParameterFaders(double time) {
			base.UpdateParameterFaders(time);
			paramDelay.UpdateFader(time);
			paramDecay.UpdateFader(time);
			paramBias.UpdateFader(time);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.CopyParametersTo"/>
		protected internal override void CopyParametersTo(SoLoudFilterInstance other) {
			base.CopyParametersTo(other);
			EchoFilterInstance instance = (EchoFilterInstance)other;
			paramDelay.CopyTo(instance.paramDelay);
			paramDecay.CopyTo(instance.paramDecay);
			paramBias.CopyTo(instance.paramBias);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.BeginFiltering"/>
		protected internal override void BeginFiltering(int channelCount, int channelSize, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_echofilter.cpp#L45

			if (maxEchoBufferLength < 0) {
				// We only know channels and sample rate at this point.. not really optimal
				maxEchoBufferLength = (int)Math.Ceiling(paramDelay * sampleRate);
			}

			if (channelStates.Length < channelCount) {
				int oldLength = channelStates.Length;
				Array.Resize(ref channelStates, channelCount);
				for (int i = oldLength; i < channelCount; i++)
					channelStates[i] = new EchoState(maxEchoBufferLength);
			}
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ApplyFilter"/>
		protected internal override void ApplyFilter(Span<float> samples, int channel, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_echofilter.cpp#L57

			EchoState state = channelStates[channel];
			float[] buffer = state.buffer;
			ref int offset = ref state.offset;

			int usableBufferLength = Math.Min((int)Math.Ceiling(paramDelay * sampleRate), maxEchoBufferLength);

			int previousOffset = (state.offset + usableBufferLength - 1) % usableBufferLength;

			float wet = paramStrength, decay = paramDecay, bias = paramBias;

			for (int i = 0; i < samples.Length; i++, previousOffset = offset, offset = (offset + 1) % usableBufferLength) {
				ref float sample = ref samples[i];
				ref float echo = ref buffer[offset];
				
				echo = bias * buffer[previousOffset] + (1 - bias) * echo;

				float overlay = sample + echo * decay;

				echo = overlay;

				sample += (overlay - sample) * wet;
			}
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ResetFilterState"/>
		protected internal override void ResetFilterState() {
			channelStates = [];
			maxEchoBufferLength = -1;
		}

		/// <inheritdoc cref="SoLoudFilterInstance.Dispose(bool)"/>
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (disposing) {
				paramDelay.Dispose();
				paramDecay.Dispose();
				paramBias.Dispose();
			}

			if (channelStates is { Length: > 0 }) {
				for (int i = 0; i < channelStates.Length; i++)
					channelStates[i].buffer = null;
			}

			channelStates = null;
		}
	}
}
