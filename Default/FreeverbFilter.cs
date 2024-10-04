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
using System.Threading.Channels;

namespace MonoSound.Default {
	/// <summary>
	/// A filter that applies reverb to a sound using the Freeverb algorithm
	/// </summary>
	public class FreeverbFilter : SoLoudFilter<FreeverbFilterInstance> {
		/// <summary>
		/// Creates a new <see cref="FreeverbFilter"/> filter with the specified parameters
		/// </summary>
		/// <param name="strength">
		/// The strength of the filter, with a minimum of 0% and a maximum of 100%.  Default is 100%.
		/// </param>
		/// <param name="feedback">
		/// The room size of the reverb, with larger values creating a longer reverb time.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </param>
		/// <param name="dampness">
		/// The damping factor of the reverb.  High damping results in reduced sharpness of the reverb and a more muted sound, whereas low damping results in a sharper and more aggressive reverb.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </param>
		/// <param name="stereoWidth">
		/// The width of the stereo reverb.  A value of 0 is mono reverb, 1 is full stereo reverb.<br/>
		/// The default is 1.
		/// </param>
		public FreeverbFilter(float strength, float feedback, float dampness, float stereoWidth) : base(strength) {
			FreeverbFilterInstance singleton = Singleton;
			singleton.paramFeeback.Value = feedback;
			singleton.paramDampness.Value = dampness;
			singleton.paramStereoWidth.Value = stereoWidth;
		}

		/// <inheritdoc cref="SoLoudFilter.CreateInstance"/>
		protected override SoLoudFilterInstance NewInstance() => new FreeverbFilterInstance(this);
	}

	/// <summary>
	/// An instance of a <see cref="FreeverbFilter"/> filter
	/// </summary>
	public partial class FreeverbFilterInstance : SoLoudFilterInstance {
		private readonly Revmodel model;  // mModel

		/// <summary>
		/// Whether the reverb is frozen in place.  When frozen, the reverb will be sustained indefinitely and won't decay.
		/// </summary>
		public readonly SoLoudFilter.BoolParameter paramFrozen;  // SoLoud::FreeverbFilter::FREEZE, mMode
		/// <summary>
		/// The room size of the reverb, with larger values creating a longer reverb time.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramFeeback;  // SoLoud::FreeverbFilter::ROOMSIZE, mRoomSize
		/// <summary>
		/// The damping factor of the reverb.  High damping results in reduced sharpness of the reverb and a more muted sound, whereas low damping results in a sharper and more aggressive reverb.<br/>
		/// Range is 0 to 1, with a default of 0.5.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramDampness;  // SoLoud::FreeverbFilter::DAMP, mDamp
		/// <summary>
		/// The width of the stereo reverb.  A value of 0 is mono reverb, 1 is full stereo reverb.<br/>
		/// The default is 1.
		/// </summary>
		public readonly SoLoudFilter.Parameter<float> paramStereoWidth;  // SoLoud::FreeverbFilter::WIDTH, mWidth

		/// <summary>
		/// Creates a new <see cref="FreeverbFilterInstance"/> instance with the specified parent filter
		/// </summary>
		public FreeverbFilterInstance(FreeverbFilter parent) : base(parent) {
			paramFrozen = CreateParameter(false);
			paramFeeback = CreateParameter(0.5f, 0f, 1f);
			paramDampness = CreateParameter(0.5f, 0f, 1f);
			paramStereoWidth = CreateParameter(1f, 0f, 1f);

			model = new();
		}

		/// <inheritdoc cref="SoLoudFilterInstance.UpdateParameterFaders"/>
		protected internal override void UpdateParameterFaders(double time) {
			base.UpdateParameterFaders(time);
			paramFeeback.UpdateFader(time);
			paramDampness.UpdateFader(time);
			paramStereoWidth.UpdateFader(time);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.CopyParametersTo"/>
		protected internal override void CopyParametersTo(SoLoudFilterInstance other) {
			base.CopyParametersTo(other);
			FreeverbFilterInstance instance = (FreeverbFilterInstance)other;
			paramFrozen.CopyTo(instance.paramFrozen);
			paramFeeback.CopyTo(instance.paramFeeback);
			paramDampness.CopyTo(instance.paramDampness);
			paramStereoWidth.CopyTo(instance.paramStereoWidth);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.BeginFiltering"/>
		protected internal override void BeginFiltering(int channelCount, int channelSize, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L480

			if (channelCount != 2)
				throw new InvalidOperationException("FreeverbFilter requires stereo audio, mono audio was provided");

			if (HasAnyParameterChanged) {
				model.SetDamp(paramDampness);
				model.SetFreezeMode(paramFrozen);
				model.SetFeedback(paramFeeback);
				model.SetStereoWidth(paramStereoWidth);
				model.SetWet(paramStrength);
				model.SetDry(1 - paramStrength);
			}
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ApplyFilteringToAllChannels"/>
		protected internal sealed override void ApplyFilteringToAllChannels(Span<float> uninterleavedSamples, int offset, int sampleCount, int channelCount, int channelSize, int sampleRate) {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L491

			// Since only a portion of the sample data will be processed, it has to be pieced together then used to overwrite the original data
			Span<float> segmentLeft = uninterleavedSamples.Slice(0 * channelSize + offset, sampleCount);
			Span<float> segmentRight = uninterleavedSamples.Slice(1 * channelSize + offset, sampleCount);
			Span<float> samples = [ .. segmentLeft, .. segmentRight ];

			// Processing modifies both channels at the same time
			model.Process(samples, channelSize);

			// Copy the processed samples back to the original data
			samples.CopyTo(segmentLeft);
			samples[channelSize..].CopyTo(segmentRight);
		}

		/// <inheritdoc cref="SoLoudFilterInstance.ApplyFilter"/>
		protected internal sealed override void ApplyFilter(Span<float> samples, int channel, int sampleRate) { }

		/// <inheritdoc cref="SoLoudFilterInstance.ResetFilterState"/>
		protected internal override void ResetFilterState() {
			// Reset the buffers
			model.Reset();
		}

		/// <inheritdoc cref="SoLoudFilterInstance.Dispose(bool)"/>
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (disposing) {
				paramFrozen.Dispose();
				paramFeeback.Dispose();
				paramDampness.Dispose();
				paramStereoWidth.Dispose();
			}
		}
	}
}
