using System;

#pragma warning disable IDE1006

namespace MonoSound.Filters.Instances{
	struct BQRStateData{
		public float mY1, mY2, mX1, mX2;
	};

	internal unsafe class BiquadResonantFilter : Filter{
		public const int WET = 0;
		public const int TYPE = 1;
		public const int FREQUENCY = 2;
		public const int RESONANCE = 3;

		public const int LOWPASS = 0;
		public const int HIGHPASS = 1;
		public const int BANDPASS = 2;

		readonly BQRStateData[] mState = new BQRStateData[8];
		float mA0, mA1, mA2, mB1, mB2;
		int mDirty;
		float mSamplerate;

		public BiquadResonantFilter(){
			int i;
			for(i = 0; i < 8; i++){
				mState[i].mX1 = 0;
				mState[i].mY1 = 0;
				mState[i].mX2 = 0;
				mState[i].mY2 = 0;
			}

			setParams(LOWPASS, 1000, 2);

			initParams(4);

			mSamplerate = 44100;

			calcBQRParams();
		}

		public void Reset(){
			int i;
			for(i = 0; i < 8; i++){
				mState[i].mX1 = 0;
				mState[i].mY1 = 0;
				mState[i].mX2 = 0;
				mState[i].mY2 = 0;
			}
		}

		private void calcBQRParams(){
			mDirty = 0;

			float omega = (float)(2.0f * Math.PI * mParam[FREQUENCY] / mSamplerate);
			float sin_omega = (float)Math.Sin(omega);
			float cos_omega = (float)Math.Cos(omega);
			float alpha = sin_omega / (2.0f * mParam[RESONANCE]);
			float scalar = 1.0f / (1.0f + alpha);

			switch((int)mParam[TYPE]){
				case LOWPASS:
					mA0 = 0.5f * (1.0f - cos_omega) * scalar;
					mA1 = (1.0f - cos_omega) * scalar;
					mA2 = mA0;
					mB1 = -2.0f * cos_omega * scalar;
					mB2 = (1.0f - alpha) * scalar;
					break;
				case HIGHPASS:
					mA0 = 0.5f * (1.0f + cos_omega) * scalar;
					mA1 = -(1.0f + cos_omega) * scalar;
					mA2 = mA0;
					mB1 = -2.0f * cos_omega * scalar;
					mB2 = (1.0f - alpha) * scalar;
					break;
				case BANDPASS:
					mA0 = alpha * scalar;
					mA1 = 0;
					mA2 = -mA0;
					mB1 = -2.0f * cos_omega * scalar;
					mB2 = (1.0f - alpha) * scalar;
					break;
			}
		}

		internal SoLoudResult setParams(int aType, float aFrequency, float aResonance){
			if(aType < WET || aType > RESONANCE || aFrequency <= 0 || aResonance <= 0)
				return SoLoudResult.INVALID_PARAMETER;

			setFilterParameter(TYPE, aType);
			setFilterParameter(FREQUENCY, aFrequency);
			setFilterParameter(RESONANCE, aResonance);

			return SoLoudResult.SO_NO_ERROR;
		}

		public override void filterChannel(float *aBuffer, uint aSamples, float aSamplerate, double aTime, uint aChannel, uint aChannels){
			uint osamples = aSamples;
			if(aChannel == 0){
				updateParams(aTime);

				if((mParamChanged & ((1 << FREQUENCY) | (1 << RESONANCE) | (1 << TYPE))) != 0 || aSamplerate != mSamplerate){
					mSamplerate = aSamplerate;
					calcBQRParams();
				}

				mParamChanged = 0;			
			}	
			
			float x;
			uint i;
			int c = 0;

			ref BQRStateData s = ref mState[aChannel];

			// make sure we access pairs of samples (one sample may be skipped)
			aSamples = (uint)(aSamples & ~1); 

			for(i = 0; i < aSamples; i +=2, c++){
				// Generate outputs by filtering inputs.
				x = aBuffer[c];
				s.mY2 = mA0 * x + mA1 * s.mX1 + mA2 * s.mX2 - mB1 * s.mY1 - mB2 * s.mY2;
				aBuffer[c] += (s.mY2 - aBuffer[c]) * mParam[WET];

				c++;

				// Permute filter operations to reduce data movement.
				// Just substitute variables instead of doing mX1=x, etc.
				s.mX2 = aBuffer[c];
				s.mY1 = mA0 * s.mX2 + mA1 * x + mA2 * s.mX1 - mB1 * s.mY2 - mB2 * s.mY1;
				aBuffer[c] += (s.mY1 - aBuffer[c]) * mParam[WET];

				// Only move a little data.
				s.mX1 = s.mX2;
				s.mX2 = x;
			}

			// If we skipped a sample earlier, patch it by just copying the previous.
			if(osamples != aSamples)
				aBuffer[c] = aBuffer[c - 1];
		}
	}
}
