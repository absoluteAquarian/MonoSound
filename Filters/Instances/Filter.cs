using System;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006

namespace MonoSound.Filters.Instances {
	/// <summary>
	/// A port of SoLoud::Filter and SoLoud::FilterInstance
	/// </summary>
	internal unsafe class Filter {
		public const int FLOAT_PARAM = 0;
		public const int INT_PARAM = 1;
		public const int BOOL_PARAM = 2;

		//Used to indirectly access this filter from other assemblies
		public int ID;
		public SoundFilterType type;

		public uint mNumParams;
		public uint mParamChanged;
		public float* mParam;
		//Ignored: *mParamFader
		private bool isFree = true;  // Checking mParam is likely not good enough...

		/// <summary>
		/// Whether this filter requires knowledge of samples earlier in the source audio.<br/>
		/// Making this property return <see langword="true"/> will prevent this filter from being used by certain APIs that only expect short snippets of audio data.
		/// </summary>
		public virtual bool RequiresSampleMemory => false;

		public Filter() {
			mNumParams = 0;
			mParamChanged = 0;
			mParam = null;
		}

		~Filter() {
			Free();
		}

		public virtual void Reset() { }

		public virtual void Free() {
			if (isFree)
				return;

			Marshal.FreeHGlobal((IntPtr)mParam);
			isFree = true;
		}

		public virtual SoLoudResult initParams(int aNumParams) {
			//mParam could be set before here... reset it just in case
			Free();

			mNumParams = (uint)aNumParams;
			mParam = (float*)Marshal.AllocHGlobal(sizeof(float) * aNumParams);
			//mParamFader = new Fader[mNumParams];
			isFree = false;

			uint i;
			for (i = 0; i < mNumParams; i++) {
				mParam[i] = 0;
			}

			mParam[FLOAT_PARAM] = 1; // set 'wet' to 1

			return SoLoudResult.SO_NO_ERROR;
		}

		/// <summary>
		/// Sets the strength (or "wetness") of the filter. 0 = no effect, 1 = full effect. Defaults to 1
		/// </summary>
		/// <param name="strength">How strong or weak the filter should be.  Ranges from 0 to 1.</param>
		public void SetStrength(float strength) {
			if (strength < 0 || strength > 1)
				throw new ArgumentException("Value provided was outside the range of valid values.", "strength");

			mParam[FLOAT_PARAM] = strength;
		}

		public virtual void updateParams(double aTime) {
			//Fader is ignored; do nothing
		}

		public virtual unsafe void filter(float* aBuffer, uint aSamples, uint aChannels, float aSamplerate, double aTime) {
			uint i;
			for (i = 0; i < aChannels; i++) {
				filterChannel(aBuffer + i * aSamples, aSamples, aSamplerate, aTime, i, aChannels);
			}
		}

		public virtual unsafe void filterChannel(float* aBuffer, uint aSamples, float aSamplerate, double aTime, uint aChannel, uint aChannels) { }

		public virtual float getFilterParameter(uint aAttributeId) {
			if (aAttributeId >= mNumParams)
				return 0;

			return mParam[aAttributeId];
		}

		public virtual void setFilterParameter(uint aAttributeId, float aValue) {
			if (aAttributeId >= mNumParams)
				return;

			mParam[aAttributeId] = aValue;
			mParamChanged |= (uint)(1 << (int)aAttributeId);
		}

		public virtual void fadeFilterParameter(uint aAttributeId, float aTo, double aTime, double aStartTime) {
			//Fade is ignored; do nothing
		}

		public virtual void oscillateFilterParameter(uint aAttributeId, float aFrom, float aTo, double aTime, double aStartTime) {
			//Fader is ignored; do nothing
		}
	}
}
