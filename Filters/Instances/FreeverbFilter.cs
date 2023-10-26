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

#pragma warning disable IDE0044, IDE1006

namespace MonoSound.Filters.Instances {
	// Based on code written by Jezar at Dreampoint, June 2000 http://www.dreampoint.co.uk, 
	// which was placed in public domain. The code was massaged quite a bit by 
	// Jari Komppa, result in the license listed at top of this file.

	internal unsafe class FreeverbFilter : Filter{
		Revmodel mModel;

		public const int WET = 0;
		public const int FREEZE = 1;
		public const int ROOMSIZE = 2;
		public const int DAMP = 3;
		public const int WIDTH = 4;

		float mMode;
		float mRoomSize;
		float mDamp;
		float mWidth;

		public override bool RequiresSampleMemory => true;

		public FreeverbFilter(){
			initParams(5);

			mModel = new Revmodel();

			setParams(0, 0.5f, 0.5f, 1);
		}

		public override void Free(){
			mModel?.Free();

			base.Free();
		}

		public override void Reset(){
			mModel = new Revmodel();
		}

		public SoLoudResult setParams(float aFreeze, float aRoomSize, float aDamp, float aWidth){
			if (aFreeze < 0 || aFreeze > 1 || aRoomSize <= 0 || aDamp < 0 || aWidth <= 0)
				return SoLoudResult.INVALID_PARAMETER;

			mParam[FREEZE] = mMode = aFreeze;
			mParam[ROOMSIZE] = mRoomSize = aRoomSize;
			mParam[DAMP] = mDamp = aDamp;
			mParam[WIDTH] = mWidth = aWidth;

			return SoLoudResult.SO_NO_ERROR;
		}

		public override unsafe void filterChannel(float* aBuffer, uint aSamples, float aSamplerate, double aTime, uint aChannel, uint aChannels){
			//SoLoud only supports stereo sounds, but that shouldn't be an issue hopefully
			
			if(mParamChanged > 0){
				mModel.setdamp(mParam[DAMP]);
				mModel.setmode(mParam[FREEZE]);
				mModel.setroomsize(mParam[ROOMSIZE]);
				mModel.setwidth(mParam[WIDTH]);
				mModel.setwet(mParam[WET]);
				mModel.setdry(1 - mParam[WET]);
				mParamChanged = 0;
			}

			mModel.process(aBuffer, aSamples);
		}
	}

	internal unsafe class Revmodel{
		const int	gNumcombs = 8;
		const int	gNumallpasses = 4;
		const float	gMuted = 0;
		const float	gFixedgain = 0.015f;
		const float gScalewet = 3;
		const float gScaledry = 2;
		const float gScaledamp = 0.4f;
		const float gScaleroom = 0.28f;
		const float gOffsetroom = 0.7f;
		const float gInitialroom = 0.5f;
		const float gInitialdamp = 0.5f;
		const float gInitialwet = 1 / gScalewet;
		const float gInitialdry = 0;
		const float gInitialwidth = 1;
		const float gInitialmode = 0;
		const float gFreezemode = 0.5f;
		const int	gStereospread = 23;

		// These values assume 44.1KHz sample rate
		// they will probably be OK for 48KHz sample rate
		// but would need scaling for 96KHz (or other) sample rates.
		// The values were obtained by listening tests.
		const int gCombtuningL1 = 1116;
		const int gCombtuningR1 = 1116 + gStereospread;
		const int gCombtuningL2 = 1188;
		const int gCombtuningR2 = 1188 + gStereospread;
		const int gCombtuningL3 = 1277;
		const int gCombtuningR3 = 1277 + gStereospread;
		const int gCombtuningL4 = 1356;
		const int gCombtuningR4 = 1356 + gStereospread;
		const int gCombtuningL5 = 1422;
		const int gCombtuningR5 = 1422 + gStereospread;
		const int gCombtuningL6 = 1491;
		const int gCombtuningR6 = 1491 + gStereospread;
		const int gCombtuningL7 = 1557;
		const int gCombtuningR7 = 1557 + gStereospread;
		const int gCombtuningL8 = 1617;
		const int gCombtuningR8 = 1617 + gStereospread;
		const int gAllpasstuningL1 = 556;
		const int gAllpasstuningR1 = 556 + gStereospread;
		const int gAllpasstuningL2 = 441;
		const int gAllpasstuningR2 = 441 + gStereospread;
		const int gAllpasstuningL3 = 341;
		const int gAllpasstuningR3 = 341 + gStereospread;
		const int gAllpasstuningL4 = 225;
		const int gAllpasstuningR4 = 225 + gStereospread;

		float	mGain;
		float	mRoomsize, mRoomsize1;
		float	mDamp, mDamp1;
		float	mWet, mWet1, mWet2;
		float	mDry;
		float	mWidth;
		float	mMode;

		int		mDirty;

		// The following are all declared inline 
		// to remove the need for dynamic allocation
		// with its subsequent error-checking messiness

		// Comb filters
		Comb[] mCombL = new Comb[gNumcombs];
	//	Comb[] mCombR = new Comb[gNumcombs];

		// Allpass filters
		Allpass[] mAllpassL = new Allpass[gNumallpasses];
	//	Allpass[] mAllpassR = new Allpass[gNumallpasses];

		// Buffers for the combs
		float[] mBufcombL1 = new float[gCombtuningL1];
	//	float[] mBufcombR1 = new float[gCombtuningR1];
		float[] mBufcombL2 = new float[gCombtuningL2];
	//	float[] mBufcombR2 = new float[gCombtuningR2];
		float[] mBufcombL3 = new float[gCombtuningL3];
	//	float[] mBufcombR3 = new float[gCombtuningR3];
		float[] mBufcombL4 = new float[gCombtuningL4];
	//	float[] mBufcombR4 = new float[gCombtuningR4];
		float[] mBufcombL5 = new float[gCombtuningL5];
	//	float[] mBufcombR5 = new float[gCombtuningR5];
		float[] mBufcombL6 = new float[gCombtuningL6];
	//	float[] mBufcombR6 = new float[gCombtuningR6];
		float[] mBufcombL7 = new float[gCombtuningL7];
	//	float[] mBufcombR7 = new float[gCombtuningR7];
		float[] mBufcombL8 = new float[gCombtuningL8];
	//	float[] mBufcombR8 = new float[gCombtuningR8];

		// Buffers for the allpasses
		float[] mBufallpassL1 = new float[gAllpasstuningL1];
	//	float[] mBufallpassR1 = new float[gAllpasstuningR1];
		float[] mBufallpassL2 = new float[gAllpasstuningL2];
	//	float[] mBufallpassR2 = new float[gAllpasstuningR2];
		float[] mBufallpassL3 = new float[gAllpasstuningL3];
	//	float[] mBufallpassR3 = new float[gAllpasstuningR3];
		float[] mBufallpassL4 = new float[gAllpasstuningL4];
	//	float[] mBufallpassR4 = new float[gAllpasstuningR4];

		//Modified to only support Mono instead of Stereo
		public Revmodel(){
			mGain = 0;
			mRoomsize = 0;
			mRoomsize1 = 0;
			mDamp = 0;
			mDamp1 = 0;
			mWet = 0;
			mWet1 = 0;
			mWet2 = 0;
			mDry = 0;
			mWidth = 0;
			mMode = 0;

			mDirty = 1;

			for(int i = 0; i < mCombL.Length; i++)
				mCombL[i] = new Comb();
		//	for(int i = 0; i < mCombR.Length; i++)
		//		mCombR[i] = new Comb();

			for(int i = 0; i < mAllpassL.Length; i++)
				mAllpassL[i] = new Allpass();
		//	for(int i = 0; i < mAllpassR.Length; i++)
		//		mAllpassR[i] = new Allpass();

			// Tie the components to their buffers
			mCombL[0].setbuffer(mBufcombL1, gCombtuningL1);
		//	mCombR[0].setbuffer(mBufcombR1, gCombtuningR1);
			mCombL[1].setbuffer(mBufcombL2, gCombtuningL2);
		//	mCombR[1].setbuffer(mBufcombR2, gCombtuningR2);
			mCombL[2].setbuffer(mBufcombL3, gCombtuningL3);
		//	mCombR[2].setbuffer(mBufcombR3, gCombtuningR3);
			mCombL[3].setbuffer(mBufcombL4, gCombtuningL4);
		//	mCombR[3].setbuffer(mBufcombR4, gCombtuningR4);
			mCombL[4].setbuffer(mBufcombL5, gCombtuningL5);
		//	mCombR[4].setbuffer(mBufcombR5, gCombtuningR5);
			mCombL[5].setbuffer(mBufcombL6, gCombtuningL6);
		//	mCombR[5].setbuffer(mBufcombR6, gCombtuningR6);
			mCombL[6].setbuffer(mBufcombL7, gCombtuningL7);
		//	mCombR[6].setbuffer(mBufcombR7, gCombtuningR7);
			mCombL[7].setbuffer(mBufcombL8, gCombtuningL8);
		//	mCombR[7].setbuffer(mBufcombR8, gCombtuningR8);
			mAllpassL[0].setbuffer(mBufallpassL1, gAllpasstuningL1);
		//	mAllpassR[0].setbuffer(mBufallpassR1, gAllpasstuningR1);
			mAllpassL[1].setbuffer(mBufallpassL2, gAllpasstuningL2);
		//	mAllpassR[1].setbuffer(mBufallpassR2, gAllpasstuningR2);
			mAllpassL[2].setbuffer(mBufallpassL3, gAllpasstuningL3);
		//	mAllpassR[2].setbuffer(mBufallpassR3, gAllpasstuningR3);
			mAllpassL[3].setbuffer(mBufallpassL4, gAllpasstuningL4);
		//	mAllpassR[3].setbuffer(mBufallpassR4, gAllpasstuningR4);

			// Set default values
			mAllpassL[0].setfeedback(0.5f);
		//	mAllpassR[0].setfeedback(0.5f);
			mAllpassL[1].setfeedback(0.5f);
		//	mAllpassR[1].setfeedback(0.5f);
			mAllpassL[2].setfeedback(0.5f);
		//	mAllpassR[2].setfeedback(0.5f);
			mAllpassL[3].setfeedback(0.5f);
		//	mAllpassR[3].setfeedback(0.5f);
			setwet(gInitialwet);
			setroomsize(gInitialroom);
			setdry(gInitialdry);
			setdamp(gInitialdamp);
			setwidth(gInitialwidth);
			setmode(gInitialmode);			

			// Buffer will be full of rubbish - so we MUST mute them
			mute();	
		}

		public void Free(){
			for(int i = 0; i < mCombL.Length; i++)
				mCombL[i].Free();

		//	for(int i = 0; i < mCombR.Length; i++)
		//		mCombR[i].Free();

			for(int i = 0; i < mAllpassL.Length; i++)
				mAllpassL[i].Free();

		//	for(int i = 0; i < mAllpassR.Length; i++)
		//		mAllpassR[i].Free();
		}

		public void mute(){			
			if(mMode >= gFreezemode)
				return;

			for(int i = 0; i < gNumcombs; i++){
				mCombL[i].mute();
		//		mCombR[i].mute();
			}

			for(int i = 0; i < gNumallpasses; i++){
				mAllpassL[i].mute();
		//		mAllpassR[i].mute();
			}
		}

		public void process(float* aSampleData, long aNumSamples){
			float* inputL, inputR;
			inputL = aSampleData;
		//	inputR = aSampleData + aNumSamples;

			if(mDirty == 1)
				update();

			mDirty = 0;

			while(aNumSamples-- > 0){
				float outL, outR, input;
				outL = outR = 0;
				input = (*inputL /* + *inputR */) * mGain;

				// Accumulate comb filters in parallel
				for(int i = 0; i < gNumcombs; i++){
					outL += mCombL[i].process(input);
			//		outR += mCombR[i].process(input);
				}

				// Feed through allpasses in series
				for(int i = 0; i < gNumallpasses; i++){
					outL = mAllpassL[i].process(outL);
			//		outR = mAllpassR[i].process(outR);
				}

				// Calculate output REPLACING anything already there
				*inputL = outL * mWet1 + outR * mWet2 + *inputL * mDry;
			//	*inputR = outR * mWet1 + outL * mWet2 + *inputR * mDry;

				// Increment sample pointers, allowing for interleave (if any)
				inputL++;
			//	inputR++;
			}
		}

		public void update(){
			// Recalculate internal values after parameter change

			int i;

			mWet1 = mWet * (mWidth / 2 + 0.5f);
			mWet2 = mWet * ((1 - mWidth) / 2);

			if(mMode >= gFreezemode){
				mRoomsize1 = 1;
				mDamp1 = 0;
				mGain = gMuted;
			}else{
				mRoomsize1 = mRoomsize;
				mDamp1 = mDamp;
				mGain = gFixedgain;
			}

			for(i = 0; i < gNumcombs; i++){
				mCombL[i].setfeedback(mRoomsize1);
			//	mCombR[i].setfeedback(mRoomsize1);
			}

			for(i = 0; i < gNumcombs; i++){
				mCombL[i].setdamp(mDamp1);
			//	mCombR[i].setdamp(mDamp1);
			}
		}

		public void setroomsize(float aValue){
			mRoomsize = aValue * gScaleroom + gOffsetroom;
			mDirty = 1;
		}

		public void setdamp(float aValue){
			mDamp = aValue * gScaledamp;
			mDirty = 1;
		}

		public void setwet(float aValue){
			mWet = aValue * gScalewet;
			mDirty = 1;
		}

		public void setdry(float aValue){
			mDry = aValue * gScaledry;
		}

		public void setwidth(float aValue){
			mWidth = aValue;
			mDirty = 1;
		}

		public void setmode(float aValue){
			mMode = aValue;
			mDirty = 1;
		}
	}

	internal unsafe class Comb{
		float	mFeedback;
		float	mFilterstore;
		float	mDamp1;
		float	mDamp2;
		float*  mBuffer;
		int		mBufsize;
		int		mBufidx;

		public Comb(){
			mFilterstore = 0;
			mBufidx = 0;
			mFeedback = 0;
			mDamp1 = 0;
			mDamp2 = 0;
			mBufsize = 0;
		}

		public void Free(){
			Marshal.FreeHGlobal((IntPtr)mBuffer);
		}

		public float process(float aInput){
			float output;
			float bufout;

			bufout = mBuffer[mBufidx];

			output = -aInput + bufout;
			mBuffer[mBufidx] = aInput + (bufout * mFeedback);

			if(++mBufidx >= mBufsize)
				mBufidx = 0;

			return output;
		}

		public void setbuffer(float[] aBuf, int aSize){
			mBuffer = (float*)Marshal.AllocHGlobal(sizeof(float) * aBuf.Length);
			for(int i = 0; i < aBuf.Length; i++)
				mBuffer[i] = aBuf[i];

			mBufsize = aSize;
		}

		public void mute(){
			for(int i = 0; i < mBufsize; i++)
				mBuffer[i] = 0;
		}

		public void setdamp(float aVal){
			mDamp1 = aVal;
			mDamp2 = 1 - aVal;
		}

		public void setfeedback(float aVal){
			mFeedback = aVal;
		}
	}

	internal unsafe class Allpass{
		float	mFeedback;
		float*  mBuffer;
		int		mBufsize;
		int		mBufidx;

		public Allpass(){
			mBufidx = 0;
			mFeedback = 0;
			mBufsize = 0;
		}

		public void Free(){
			Marshal.FreeHGlobal((IntPtr)mBuffer);
		}

		public float process(float aInput){
			float output;
			float bufout;

			bufout = mBuffer[mBufidx];

			output = -aInput + bufout;
			mBuffer[mBufidx] = aInput + (bufout * mFeedback);

			if(++mBufidx >= mBufsize)
				mBufidx = 0;

			return output;
		}

		public void setbuffer(float[] aBuf, int aSize){
			mBuffer = (float*)Marshal.AllocHGlobal(sizeof(float) * aBuf.Length);
			for(int i = 0; i < aBuf.Length; i++)
				mBuffer[i] = aBuf[i];

			mBufsize = aSize;
		}

		public void mute(){
			for(int i = 0; i < mBufsize; i++)
				mBuffer[i] = 0;
		}

		public void setfeedback(float aVal){
			mFeedback = aVal;
		}
	}
}
