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

using System;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006

namespace MonoSound.Filters.Instances{
	internal unsafe class EchoFilter : Filter{
		float *mBuffer;
		int mBufferLength;
		int mBufferMaxLength;
		int mOffset;

		float mDelay;
		float mDecay;
		float mFilter;

		public const int WET = 0;
		public const int DELAY = 1;
		public const int DECAY = 2;
		public const int FILTER = 3;

		public override bool RequiresSampleMemory => true;

		public EchoFilter(){
			mDelay = 0.3f;
			mDecay = 0.7f;
			mFilter = 0.0f;
			mBuffer = null;
			mBufferLength = 0;
			mBufferMaxLength = 0;
			mOffset = 0;

			initParams(4);
		}

		~EchoFilter(){
			Free();
		}

		public override void Reset(){
			mOffset = 0;
		}

		public override void Free(){
			Marshal.FreeHGlobal((IntPtr)mBuffer);

			base.Free();
		}

		public SoLoudResult setParams(float aDelay, float aDecay, float aFilter){
			if(aDelay <= 0 || aDecay <= 0 || aFilter < 0 || aFilter >= 1.0f)
				return SoLoudResult.INVALID_PARAMETER;

			mParam[DECAY] = mDecay = aDecay;
			mParam[DELAY] = mDelay = aDelay;
			mParam[FILTER] = mFilter = aFilter;
		
			return SoLoudResult.SO_NO_ERROR;
		}

		public override unsafe void filterChannel(float* aBuffer, uint aSamples, float aSamplerate, double aTime, uint aChannel, uint aChannels){
			updateParams(aTime);
			if(mBuffer == (float*)IntPtr.Zero){
				// We only know channels and sample rate at this point.. not really optimal
				mBufferMaxLength = (int)Math.Ceiling(mParam[DELAY] * aSamplerate);
				mBuffer = (float*)Marshal.AllocHGlobal(sizeof(int) * mBufferMaxLength * (int)aChannels);
				
				uint ii;
				for (ii = 0; ii < mBufferMaxLength * aChannels; ii++)
					mBuffer[ii] = 0;
			}

			mBufferLength = (int)Math.Ceiling(mParam[DELAY] * aSamplerate);
			if(mBufferLength > mBufferMaxLength)
				mBufferLength = mBufferMaxLength;

			uint i, j;
			int prevofs = (mOffset + mBufferLength - 1) % mBufferLength;
			for(i = 0; i < aSamples; i++){
				for(j = 0; j < aChannels; j++){
					uint chofs = j * (uint)mBufferLength;
					uint bchofs = j * aSamples;
				
					mBuffer[mOffset + chofs] = mParam[FILTER] * mBuffer[prevofs + chofs] + (1 - mParam[FILTER]) * mBuffer[mOffset + chofs];
				
					float n = aBuffer[i + bchofs] + mBuffer[mOffset + chofs] * mParam[DECAY];
					mBuffer[mOffset + chofs] = n;

					aBuffer[i + bchofs] += (n - aBuffer[i + bchofs]) * mParam[WET];
				}

				prevofs = mOffset;
				mOffset = (mOffset + 1) % mBufferLength;
			}
		}
	}
}
