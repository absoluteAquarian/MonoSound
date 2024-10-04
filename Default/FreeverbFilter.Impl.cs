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
using System.Runtime.CompilerServices;

namespace MonoSound.Default {
	partial class FreeverbFilterInstance {
		private struct Comb {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L39

			public float feedback;  // mFeedback
			private float store;    // mFilterstore
			private float damp1;    // mDamp1
			private float damp2;    // mDamp2
			public float[] buffer;  // mBuffer
			private int index;      // mBufidx

			public void SetDamp(float damp) {
				damp1 = damp;
				damp2 = 1 - damp;
			}

			public float Process(float sample) {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L235

				ref float bufferSample = ref buffer[index];
				float output = bufferSample;

				store = (output * damp2) + (store * damp1);
				bufferSample = sample + (store * feedback);

				if (++index >= buffer.Length)
					index = 0;

				return output;
			}

			public readonly void Mute() => buffer.AsSpan().Clear();

			public void Reset() {
				Mute();
				store = default;
				index = 0;
			}
		}

		private struct Allpass {
			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L57

			public float feedback;  // mFeedback
			public float[] buffer;  // mBuffer
			private int index;      // mBufidx

			public float Process(float sample) {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L192

				ref float bufferSample = ref buffer[index];
				float output = -sample + bufferSample;

				bufferSample = sample + (bufferSample * feedback);

				if (++index >= buffer.Length)
					index = 0;

				return output;
			}

			public readonly void Mute() => buffer.AsSpan().Clear();

			public void Reset() {
				Mute();
				index = 0;
			}
		}

		// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L71
		
		// Constants used to initialize the model
		const int NUM_COMBS = 8;                  // gNumCombs
		const int NUM_PASSES = 4;                 // gNumallpasses
		const float MUTED = 0;                    // gMuted
		const float FIXED_GAIN = 0.015f;          // gFixedgain
		const float SCALE_WET = 3;                // gScalewet
		const float SCALE_DRY = 2;                // gScaledry
		const float SCALE_DAMP = 0.4f;            // gScaledamp
		const float SCALE_ROOM = 0.28f;           // gScaleroom
		const float OFFSET_ROOM = 0.7f;           // gOffsetroom
		const float INITIAL_ROOM = 0.5f;          // gInitialroom
		const float INITIAL_DAMP = 0.5f;          // gInitialdamp
		const float INITIAL_WET = 1 / SCALE_WET;  // gInitialwet
		const float INITIAL_DRY = 0;              // gInitialdry
		const float INITIAL_WIDTH = 1;            // gInitialwidth
		const bool INITIAL_MODE = false;          // gInitialmode
		const bool FREEZE_MODE = true;            // gFreezemode
		const int STEREO_SPREAD = 23;             // gStereospread

		// These values assume 44.1KHz sample rate
		// they will probably be OK for 48KHz sample rate
		// but would need scaling for 96KHz (or other) sample rates.
		// The values were obtained by listening tests.
		const int TUNING_COMB_1 = 1116;           // gCombtuning1
		const int TUNING_COMB_2 = 1188;           // gCombtuning2
		const int TUNING_COMB_3 = 1277;           // gCombtuning3
		const int TUNING_COMB_4 = 1356;           // gCombtuning4
		const int TUNING_COMB_5 = 1422;           // gCombtuning5
		const int TUNING_COMB_6 = 1491;           // gCombtuning6
		const int TUNING_COMB_7 = 1557;           // gCombtuning7
		const int TUNING_COMB_8 = 1617;           // gCombtuning8
		const int TUNING_PASS_1 = 556;            // gAllpasstuning1
		const int TUNING_PASS_2 = 441;            // gAllpasstuning2
		const int TUNING_PASS_3 = 341;            // gAllpasstuning3
		const int TUNING_PASS_4 = 225;            // gAllpasstuning4

		private class Revmodel : IDisposable {
			private float gain;          // mGain
			private float roomsize;      // mRoomsize
			private float usedRoomsize;  // mRoomsize1
			private float damp;          // mDamp
			private float usedDamp;      // mDamp1
			private float wet;           // mWet
			private float wetLeft;       // mWet1
			private float wetRight;      // mWet2
			private float dry;           // mDry
			private float width;         // mWidth
			private bool mode;           // mMode
			private bool dirty;          // mDirty

			// Comb filters
			private Comb[] combLeft = new Comb[NUM_COMBS];   // mCombL
			private Comb[] combRight = new Comb[NUM_COMBS];  // mCombR

			// Allpass filters
			private Allpass[] allpassLeft = new Allpass[NUM_PASSES];   // mAllpassL
			private Allpass[] allpassRight = new Allpass[NUM_PASSES];  // mAllpassR

			public Revmodel() {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L273

				dirty = true;

				// Tie the components to their buffers
				combLeft[0].buffer = new float[TUNING_COMB_1];
				combRight[0].buffer = new float[TUNING_COMB_1 + STEREO_SPREAD];
				combLeft[1].buffer = new float[TUNING_COMB_2];
				combRight[1].buffer = new float[TUNING_COMB_2 + STEREO_SPREAD];
				combLeft[2].buffer = new float[TUNING_COMB_3];
				combRight[2].buffer = new float[TUNING_COMB_3 + STEREO_SPREAD];
				combLeft[3].buffer = new float[TUNING_COMB_4];
				combRight[3].buffer = new float[TUNING_COMB_4 + STEREO_SPREAD];
				combLeft[4].buffer = new float[TUNING_COMB_5];
				combRight[4].buffer = new float[TUNING_COMB_5 + STEREO_SPREAD];
				combLeft[5].buffer = new float[TUNING_COMB_6];
				combRight[5].buffer = new float[TUNING_COMB_6 + STEREO_SPREAD];
				combLeft[6].buffer = new float[TUNING_COMB_7];
				combRight[6].buffer = new float[TUNING_COMB_7 + STEREO_SPREAD];
				combLeft[7].buffer = new float[TUNING_COMB_8];
				combRight[7].buffer = new float[TUNING_COMB_8 + STEREO_SPREAD];
				allpassLeft[0].buffer = new float[TUNING_PASS_1];
				allpassRight[0].buffer = new float[TUNING_PASS_1 + STEREO_SPREAD];
				allpassLeft[1].buffer = new float[TUNING_PASS_2];
				allpassRight[1].buffer = new float[TUNING_PASS_2 + STEREO_SPREAD];
				allpassLeft[2].buffer = new float[TUNING_PASS_3];
				allpassRight[2].buffer = new float[TUNING_PASS_3 + STEREO_SPREAD];
				allpassLeft[3].buffer = new float[TUNING_PASS_4];
				allpassRight[3].buffer = new float[TUNING_PASS_4 + STEREO_SPREAD];

				// Set default values
				for (int i = 0; i < NUM_PASSES; i++) {
					allpassLeft[i].feedback = 0.5f;
					allpassRight[i].feedback = 0.5f;
				}

				SetWet(INITIAL_WET);
				SetFeedback(INITIAL_ROOM);
				SetDry(INITIAL_DRY);
				SetDamp(INITIAL_DAMP);
				SetStereoWidth(INITIAL_WIDTH);
				SetFreezeMode(INITIAL_MODE);
			}

			public void Process(Span<float> samples, int channelSize) {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L352

				ref float channelLeft = ref samples[0];
				ref float channelRight = ref samples[channelSize];

				if (dirty) {
					UpdateParameters();
					dirty = false;
				}

				for (int i = 0; i < channelSize; i++, channelLeft = ref Unsafe.Add(ref channelLeft, 1), channelRight = ref Unsafe.Add(ref channelRight, 1)) {
					float outputLeft = 0, outputRight = 0;
					float sample = (channelLeft + channelRight) * gain;

					// Accumulate comb filters in parallel
					for (int c = 0; c < NUM_COMBS; c++) {
						outputLeft += combLeft[c].Process(sample);
						outputRight += combRight[c].Process(sample);
					}

					// Feed through allpasses in series
					for (int p = 0; p < NUM_PASSES; p++) {
						outputLeft = allpassLeft[p].Process(outputLeft);
						outputRight = allpassRight[p].Process(outputRight);
					}

					// Calculate output REPLACING anything already there
					channelLeft = outputLeft * wetLeft + outputRight * wetRight + channelLeft * dry;
					channelRight = outputRight * wetLeft + outputLeft * wetRight + channelRight * dry;
				}
			}

			public void Mute() {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L335

				if (mode == FREEZE_MODE)
					return;

				for (int i = 0; i < NUM_COMBS; i++) {
					combLeft[i].Mute();
					combRight[i].Mute();
				}

				for (int i = 0; i < NUM_PASSES; i++) {
					allpassLeft[i].Mute();
					allpassRight[i].Mute();
				}
			}

			public void Reset() {
				for (int i = 0; i < NUM_COMBS; i++) {
					combLeft[i].Reset();
					combRight[i].Reset();
				}

				for (int i = 0; i < NUM_PASSES; i++) {
					allpassLeft[i].Reset();
					allpassRight[i].Reset();
				}

				// Force parameters to be recalculated
				dirty = true;
			}

			private void UpdateParameters() {
				// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L392

				// Recalculate internal values after parameter change
				wetLeft = wet * (width / 2 + 0.5f);
				wetRight = wet * ((1 - width) / 2);

				if (mode == FREEZE_MODE) {
					usedRoomsize = 1;
					usedDamp = 0;
					gain = MUTED;
				} else {
					usedRoomsize = roomsize;
					usedDamp = damp;
					gain = FIXED_GAIN;
				}

				for (int i = 0; i < NUM_COMBS; i++) {
					combLeft[i].feedback = usedRoomsize;
					combRight[i].feedback = usedRoomsize;
				}

				for (int i = 0; i < NUM_COMBS; i++) {
					combLeft[i].SetDamp(usedDamp);
					combRight[i].SetDamp(usedDamp);
				}
			}

			// https://github.com/jarikomppa/soloud/blob/master/src/filter/soloud_freeverbfilter.cpp#L427

			public void SetFeedback(float value) {
				roomsize = (value * SCALE_ROOM) + OFFSET_ROOM;
				dirty = true;
			}

			public void SetDamp(float value) {
				damp = value * SCALE_DAMP;
				dirty = true;
			}

			public void SetWet(float value) {
				wet = value * SCALE_WET;
				dirty = true;
			}

			public void SetDry(float value) {
				dry = value * SCALE_DRY;
			}

			public void SetStereoWidth(float value) {
				width = value;
				dirty = true;
			}

			public void SetFreezeMode(bool value) {
				mode = value;
				dirty = true;
			}

			public void Dispose() {
				DisposeImpl();
				GC.SuppressFinalize(this);
			}

			private void DisposeImpl() {
				for (int i = 0; i < NUM_COMBS; i++) {
					combLeft[i].buffer = null;
					combRight[i].buffer = null;
				}

				combLeft = null;
				combRight = null;

				for (int i = 0; i < NUM_PASSES; i++) {
					allpassLeft[i].buffer = null;
					allpassRight[i].buffer = null;
				}

				allpassLeft = null;
				allpassRight = null;
			}

			~Revmodel() => DisposeImpl();
		}
	}
}
