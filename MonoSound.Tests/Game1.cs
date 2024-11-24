using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoSound.Default;
using MonoSound.FFT;
using MonoSound.Filters;
using MonoSound.Streaming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MonoSound.Tests {
	public class Game1 : Game {
		private readonly GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;

		private TestStateMachine machine;

		public Game1() {
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
		}

		protected override void Initialize() {
			// TODO: Add your initialization logic here

			machine = new TestStateMachine();

			machine.InitializeStates(
				// Different pan testing
				new TestState(Keys.D1, "Sound Panning", null).WithChildren(
					new TestState(Keys.D1, "Stereo Channel - Broken", PlayStereoPanning),
					new TestState(Keys.D2, "Mono Channel - Working", PlayMonoPanning)),
				new TestState(Keys.D2, "Sound Streaming", null).WithChildren(
					new TestState(Keys.D1, "Play Song", PlayStreamedSong),
					new TestState(Keys.D2, "Stop Song", StopStreamedSong),
					new TestState(Keys.D3, "Clear Filters", ClearStreamedSongFilters),
					new TestState(Keys.D4, "Apply Low Pass Filter", ApplyLowPassFilterToStreamedSong),
					new TestState(Keys.D5, "Apply High Pass Filter", ApplyHighPassFilterToStreamedSong),
					new TestState(Keys.D6, "Apply Band Pass Filter", ApplyBandPassFilterToStreamedSong)),
				new TestState(Keys.D3, "Segmented Sound Streaming", null).WithChildren(
					new TestState(Keys.D1, "Play Song", PlaySegmentedSong),
					new TestState(Keys.D2, "Stop Song", StopSegmentedSong),
					new TestState(Keys.D3, "Clear Filters", ClearSegmentedSongFilters),
					new TestState(Keys.D4, "Apply Low Pass Filter", ApplyLowPassFilterToSegmentedSong),
					new TestState(Keys.D5, "Apply High Pass Filter", ApplyHighPassFilterToSegmentedSong),
					new TestState(Keys.D6, "Apply Band Pass Filter", ApplyBandPassFilterToSegmentedSong),
					new TestState(Keys.D7, "Jump to Section...", null).WithChildren(
						new TestState(Keys.D1, "Introduction", null).WithChildren(
							new TestState(Keys.D1, "Immediate", JumpToSection_0_Immediate),
							new TestState(Keys.D2, "After Current", JumpToSection_0_Delayed),
							new TestState(Keys.D3, "Fade To", JumpToSection_0_Fade)
						),
						new TestState(Keys.D2, "Middle", null).WithChildren(
							new TestState(Keys.D1, "Immediate", JumpToSection_1_Immediate),
							new TestState(Keys.D2, "After Current", JumpToSection_1_Delayed),
							new TestState(Keys.D3, "Fade To", JumpToSection_1_Fade)
						),
						new TestState(Keys.D3, "Ending", null).WithChildren(
							new TestState(Keys.D1, "Immediate", JumpToSection_2_Immediate),
							new TestState(Keys.D2, "After Current", JumpToSection_2_Delayed),
							new TestState(Keys.D3, "Fade To", JumpToSection_2_Fade)
						)
					)
				),
				new TestState(Keys.D4, "Filtered Sounds", null).WithChildren(
					new TestState(Keys.D1, "Direct", null).WithChildren(
						new TestState(Keys.D1, "No Filter", PlayNoFilterFilteredSound),
						new TestState(Keys.D2, "Low Pass Filter", PlayLowPassFilteredSound),
						new TestState(Keys.D3, "High Pass Filter", PlayHighPassFilteredSound),
						new TestState(Keys.D4, "Band Pass Filter", PlayBandPassFilteredSound),
						new TestState(Keys.D5, "Echo Filter", PlayEchoFilteredSound),
						new TestState(Keys.D6, "Reverb Filter", PlayReverbFilteredSound)
					),
					new TestState(Keys.D2, "Live Update", null).WithChildren(
						new TestState(Keys.D1, "Play With No Filter", LiveUpdate_PlayWithNoFilter),
						new TestState(Keys.D2, "Stop Playing", LiveUpdate_StopPlaying),
						new TestState(Keys.D3, "Low Pass Filter", null).WithChildren(
							new TestState(Keys.D1, "Apply", LiveUpdate_PlayLowPass),
							new TestState(Keys.D2, "Random Parameters", LiveUpdate_UpdateToRandom_LowPass)
						),
						new TestState(Keys.D4, "High Pass Filter", null).WithChildren(
							new TestState(Keys.D1, "Apply", LiveUpdate_PlayHighPass),
							new TestState(Keys.D2, "Random Parameters", LiveUpdate_UpdateToRandom_HighPass)
						),
						new TestState(Keys.D5, "Band Pass Filter", null).WithChildren(
							new TestState(Keys.D1, "Apply", LiveUpdate_PlayBandPass),
							new TestState(Keys.D2, "Random Parameters", LiveUpdate_UpdateToRandom_BandPass)
						),
						new TestState(Keys.D6, "Echo Filter", null).WithChildren(
							new TestState(Keys.D1, "Apply", LiveUpdate_PlayEcho),
							new TestState(Keys.D2, "Random Parameters", LiveUpdate_UpdateToRandom_Echo)
						),
						new TestState(Keys.D7, "Reverb Filter", null).WithChildren(
							new TestState(Keys.D1, "Apply", LiveUpdate_PlayReverb),
							new TestState(Keys.D2, "Random Parameters", LiveUpdate_UpdateToRandom_Reverb),
							new TestState(Keys.D3, "Toggle Echo Decay Freeze", LiveUpdate_Reverb_ToggleFreeze)
						),
						new TestState(Keys.D8, "Toggle FFT", LiveUpdate_ToggleFFTRender)
					)
				)
			);

			base.Initialize();
		}

		FontSystem _fontSystem;

		protected override void LoadContent() {
			_graphics.PreferredBackBufferWidth = 1600;
			_graphics.PreferredBackBufferHeight = 1200;
			_graphics.ApplyChanges();

			_spriteBatch = new SpriteBatch(GraphicsDevice);

			// TODO: use this.Content to load your game content here

			MonoSoundLibrary.Init(this);

			lowPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.LowPass, 0.6f, 2700, 3);
			highPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.HighPass, 0.5f, 1800, 12);
			bandPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.BandPass, 0.7f, 3400, 4);
			echo = FilterLoader.RegisterEchoFilter(1f, 0.3f, 0.8f, 0.7f);
			reverb = FilterLoader.RegisterReverbFilter(1f, 0.5f, 0.5f, 1f);

			_fontSystem = new FontSystem();
			_fontSystem.AddFont(File.ReadAllBytes("C:/Windows/Fonts/times.ttf"));
		}

		static KeyboardState kb, oldKb;
		static SoundEffect sfx, song, filteredSfx;
		static SoundEffectInstance songInstance, filteredSfxInstance;
		static StreamPackage streamedSound;
		static SegmentedOggStream streamedSegmentedSound;
		static float segmentedSongVolume;
		static float segmentFade;

		static int lowPass, highPass, bandPass, echo, reverb;

		static SoLoudFilter liveUpdateFilter;
		static SoLoudFilterInstance liveUpdateFilterInstance;
		static StreamPackage liveUpdateStream;
		static FastFourierTransform liveUpdateFFT;

		static readonly Random random = new();

		static int timer;

		static readonly List<Keys> currentSequence = [];

		protected override void OnExiting(object sender, EventArgs args) {
			MonoSoundLibrary.DeInit();
		}

		protected override void Update(GameTime gameTime) {
			oldKb = kb;
			kb = Keyboard.GetState();

			var input = kb.GetPressedKeys().Except(oldKb.GetPressedKeys());
			if (!input.Any(k => k == Keys.Escape))
				currentSequence.AddRange(input);
			else if (currentSequence.Count > 0)
				currentSequence.RemoveAt(currentSequence.Count - 1);

			var current = machine.GetCurrentNode(currentSequence);
			if (current is null) {
				currentSequence.Clear();
			} else if (current.onSelected != null) {
				machine.InvokeCurrent(currentSequence);
				currentSequence.Clear();
			}

			// Different pan testing -> Mono Channel
			if (songInstance != null) {
				float revolutionsPerSecond = 0.6f;
				float sin = MathF.Sin(MathHelper.ToRadians(timer * 6f * revolutionsPerSecond));

				songInstance.Pan = sin;
			}

			timer++;

			// Update the current segmented song fade
			if (segmentFade > 0) {
				segmentFade -= 1f / 60f;  // Fade lasts 60 update ticks = 1 second

				if (segmentFade <= 0) {
					streamedSegmentedSound.JumpToDelayedSection();
					streamedSegmentedSound.OnDelayedSectionStart += p => p.Metrics.Volume = segmentedSongVolume;
					segmentFade = 0;
				} else {
					// Apply the lower volume
					streamedSegmentedSound.Metrics.Volume = segmentedSongVolume * segmentFade;
				}
			}

			base.Update(gameTime);
		}

		private static void PlayStereoPanning() {
			// Different pan testing (fails due to the sound using Stereo)
			sfx ??= EffectLoader.GetEffect("Content/MunchMunch-resaved2.wav");

			float revolutionsPerSecond = 0.3f;
			float sin = MathF.Sin(MathHelper.ToRadians(timer * 6f * revolutionsPerSecond));

			sfx.Play(1, 0, sin);
		}

		private static void PlayMonoPanning() {
			// Different pan testing (succeeds due to the sound using Mono)
			StreamLoader.FreeStreamedSound(ref streamedSound);
			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);
			StreamLoader.FreeStreamedSound(ref liveUpdateStream);

			song ??= EffectLoader.GetEffect("Content/chill-mono.ogg");

			if (songInstance is null) {
				songInstance = song.CreateInstance();
				songInstance.Volume = 0.3f;

				songInstance.Play();
			} else {
				songInstance.Dispose();
				songInstance = null;
			}
		}

		private static void PlayStreamedSong() {
			// Streamed audio testing
			if (songInstance != null) {
				songInstance.Dispose();
				songInstance = null;
			}

			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);
			StreamLoader.FreeStreamedSound(ref liveUpdateStream);

			if (!StreamLoader.IsStreaming(streamedSound)) {
				streamedSound = StreamLoader.GetStreamedSound("Content/chill-mono.ogg", looping: true);
				streamedSound.Metrics.Volume = 0.3f;

				streamedSound.Play();
			}
		}

		private static void StopStreamedSong() {
			StreamLoader.FreeStreamedSound(ref streamedSound);
		}

		private static void ClearStreamedSongFilters() {
			// Applying filters to streamed audio testing
			streamedSound?.ApplyFilters(ids: null);
		}

		private static void ApplyLowPassFilterToStreamedSong() {
			// Applying filters to streamed audio testing
			streamedSound?.ApplyFilters(ids: lowPass);
		}

		private static void ApplyHighPassFilterToStreamedSong() {
			// Applying filters to streamed audio testing
			streamedSound?.ApplyFilters(ids: highPass);
		}

		private static void ApplyBandPassFilterToStreamedSong() {
			// Applying filters to streamed audio testing
			streamedSound?.ApplyFilters(ids: bandPass);
		}

		private static void PlaySegmentedSong() {
			// Streamed audio testing
			if (songInstance != null) {
				songInstance.Dispose();
				songInstance = null;
			}

			StreamLoader.FreeStreamedSound(ref streamedSound);
			StreamLoader.FreeStreamedSound(ref liveUpdateStream);

			if (!StreamLoader.IsStreaming(streamedSegmentedSound)) {
				streamedSegmentedSound = (SegmentedOggStream)StreamLoader.GetStreamedSound("Content/bonetrousle.ogg", new SegmentedOggFormat(), true, new IAudioSegment[] {
					// Intro
					new Segment(TimeSpan.Zero, TimeSpan.FromSeconds(31.95)),
					// Main loop
					new Segment(TimeSpan.FromSeconds(31.95), TimeSpan.FromSeconds(89.55)),
					// Ending
					new EndSegment(TimeSpan.FromSeconds(89.55))
				});
				streamedSegmentedSound.Metrics.Volume = segmentedSongVolume = 0.3f;
			}

			streamedSegmentedSound.Play();
		}

		private static void StopSegmentedSong() {
			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);
		}

		private static void ClearSegmentedSongFilters() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			// Applying filters to streamed audio testing
			streamedSegmentedSound.ApplyFilters(ids: null);
		}

		private static void ApplyLowPassFilterToSegmentedSong() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			// Applying filters to streamed audio testing
			streamedSegmentedSound.ApplyFilters(ids: lowPass);
		}

		private static void ApplyHighPassFilterToSegmentedSong() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			// Applying filters to streamed audio testing
			streamedSegmentedSound.ApplyFilters(ids: highPass);
		}

		private static void ApplyBandPassFilterToSegmentedSong() {
			// Applying filters to streamed audio testing
			streamedSegmentedSound?.ApplyFilters(ids: bandPass);
		}

		private static void JumpToSection_0_Immediate() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(0, false);
		}

		private static void JumpToSection_0_Delayed() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(0, true);
		}

		private static void JumpToSection_0_Fade() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(0, true);

			if (segmentFade == 0)
				segmentFade = 1;
		}

		private static void JumpToSection_1_Immediate() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(1, false);
		}

		private static void JumpToSection_1_Delayed() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(1, true);
		}

		private static void JumpToSection_1_Fade() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(1, true);
			
			if (segmentFade == 0)
				segmentFade = 1;
		}

		private static void JumpToSection_2_Immediate() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(2, false);
		}

		private static void JumpToSection_2_Delayed() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(2, true);
		}

		private static void JumpToSection_2_Fade() {
			if (!StreamLoader.IsStreaming(streamedSegmentedSound))
				return;

			streamedSegmentedSound.JumpTo(2, true);
			
			if (segmentFade == 0)
				segmentFade = 1;
		}

		private static void PlayNoFilterFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetEffect("Content/spooky.mp3");

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void PlayLowPassFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", lowPass);

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void PlayHighPassFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", highPass);

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void PlayBandPassFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", bandPass);

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void PlayEchoFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", echo);

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void PlayReverbFilteredSound() {
			// Audio with filters testing
			filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", reverb);

			filteredSfxInstance?.Dispose();
			filteredSfxInstance = filteredSfx.CreateInstance();

			filteredSfxInstance.Play();
		}

		private static void LiveUpdate_PlayWithNoFilter() {
			LiveUpdate_InitStream(false);

			liveUpdateFilterInstance?.Dispose();
			liveUpdateFilterInstance = null;
			liveUpdateFilter = null;
		}

		private static void LiveUpdate_PlayLowPass() {
			if (liveUpdateFilterInstance is BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.LOW_PASS })
				return;  // Already playing the low pass filter

			var old = liveUpdateFilterInstance;

			liveUpdateFilter = new BiquadResonantFilter(1.0f, BiquadResonantFilter.LOW_PASS, 5000f, 5);
			liveUpdateFilterInstance = liveUpdateFilter.CreateInstance();

			LiveUpdate_InitStream(true);

			old?.Dispose();
		}

		private static void LiveUpdate_PlayHighPass() {
			if (liveUpdateFilterInstance is BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.HIGH_PASS })
				return;  // Already playing the high pass filter

			var old = liveUpdateFilterInstance;

			liveUpdateFilter = new BiquadResonantFilter(1.0f, BiquadResonantFilter.HIGH_PASS, 2500f, 8);
			liveUpdateFilterInstance = liveUpdateFilter.CreateInstance();

			LiveUpdate_InitStream(true);

			old?.Dispose();
		}

		private static void LiveUpdate_PlayBandPass() {
			if (liveUpdateFilterInstance is BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.BAND_PASS })
				return;  // Already playing the band pass filter

			var old = liveUpdateFilterInstance;

			liveUpdateFilter = new BiquadResonantFilter(1.0f, BiquadResonantFilter.BAND_PASS, 3500f, 3);
			liveUpdateFilterInstance = liveUpdateFilter.CreateInstance();

			LiveUpdate_InitStream(true);

			old?.Dispose();
		}

		private static void LiveUpdate_PlayEcho() {
			if (liveUpdateFilter is EchoFilter)
				return;  // Already playing the echo filter

			var old = liveUpdateFilterInstance;

			liveUpdateFilter = new EchoFilter(0.5f, 0.4f, 0.8f, 0.7f);
			liveUpdateFilterInstance = liveUpdateFilter.CreateInstance();

			LiveUpdate_InitStream(true);

			old?.Dispose();
		}

		private static void LiveUpdate_PlayReverb() {
			if (liveUpdateFilter is FreeverbFilter)
				return;  // Already playing the reverb filter

			var old = liveUpdateFilterInstance;

			liveUpdateFilter = new FreeverbFilter(1f, 0.5f, 0.5f, 1f);
			liveUpdateFilterInstance = liveUpdateFilter.CreateInstance();

			LiveUpdate_InitStream(true);

			old?.Dispose();
		}

		private static void LiveUpdate_InitStream(bool hasFilter) {
			// Streamed audio testing
			if (songInstance != null) {
				songInstance.Dispose();
				songInstance = null;
			}

			StreamLoader.FreeStreamedSound(ref streamedSound);
			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);

			if (liveUpdateStream is null) {
				// Start a new stream
				liveUpdateStream = StreamLoader.GetStreamedSound("Content/Stickerbush Symphony Restored to HD.mp3", looping: true);
				liveUpdateStream.Metrics.Volume = 0.4f;

				if (hasFilter)
					liveUpdateStream.ApplyFilters(liveUpdateFilterInstance);

				liveUpdateStream.Play();
			} else {
				// Set the filters
				if (hasFilter)
					liveUpdateStream.ApplyFilters(liveUpdateFilterInstance);
				else
					liveUpdateStream.RemoveAllFilters();
			}
		}

		private static void LiveUpdate_StopPlaying() {
			StreamLoader.FreeStreamedSound(ref liveUpdateStream);
			liveUpdateFilterInstance?.Dispose();
			liveUpdateFilterInstance = null;
			liveUpdateFilter = null;
			liveUpdateFFT = null;
		}

		private static void LiveUpdate_UpdateToRandom_LowPass() {
			if (liveUpdateFilterInstance is not BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.LOW_PASS } instance)
				return;  // Not playing the low pass filter

			LiveUpdate_UpdateToRandom_BQR(instance);
		}

		private static void LiveUpdate_UpdateToRandom_HighPass() {
			if (liveUpdateFilterInstance is not BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.HIGH_PASS } instance)
				return;  // Not playing the high pass filter

			LiveUpdate_UpdateToRandom_BQR(instance);
		}

		private static void LiveUpdate_UpdateToRandom_BandPass() {
			if (liveUpdateFilterInstance is not BiquadResonantFilterInstance { paramType.Value: BiquadResonantFilter.BAND_PASS } instance)
				return;  // Not playing the band pass filter

			LiveUpdate_UpdateToRandom_BQR(instance);
		}

		private static void LiveUpdate_UpdateToRandom_BQR(BiquadResonantFilterInstance instance) {
			instance.paramStrength.Value = instance.paramStrength.GenerateRandomMin(random, min: 0.1f);  // [0.1, 1.0]
			instance.paramFrequency.Value = instance.paramFrequency.GenerateRandom(random);              // [10, 8000]
			instance.paramResonance.Value = instance.paramResonance.GenerateRandom(random);              // [0.1, 20]
		}

		private static void LiveUpdate_UpdateToRandom_Echo() {
			if (liveUpdateFilter is not EchoFilter)
				return;  // Not playing the echo filter

			EchoFilterInstance instance = (EchoFilterInstance)liveUpdateFilterInstance;
			instance.paramStrength.Value = instance.paramStrength.GenerateRandomMin(random, min: 0.1f);     // [0.1, 1.0]
			instance.paramDelay.Value = instance.paramDelay.GenerateRandom(random, min: 0.15f, max: 0.4f);  // [0.15, 0.4]
			instance.paramDecay.Value = instance.paramDecay.GenerateRandom(random, min: 0.78f, max: 0.9f);  // [0.78, 0.9]
			instance.paramBias.Value = instance.paramBias.GenerateRandom(random);                           // [0.0, 1.0]

			// Force the buffer to reset
			instance.Reset();
		}

		private static void LiveUpdate_UpdateToRandom_Reverb() {
			if (liveUpdateFilter is not FreeverbFilter)
				return;  // Not playing the reverb filter

			FreeverbFilterInstance instance = (FreeverbFilterInstance)liveUpdateFilterInstance;
			instance.paramStrength.Value = instance.paramStrength.GenerateRandomMin(random, min: 0.1f);  // [0.1, 1.0]
			instance.paramFeeback.Value = instance.paramFeeback.GenerateRandom(random);                  // [0.0, 1.0]
			instance.paramDampness.Value = instance.paramDampness.GenerateRandom(random);                // [0.0, 1.0]
			instance.paramStereoWidth.Value = instance.paramStereoWidth.GenerateRandom(random);          // [0.0, 1.0]
		}

		private static void LiveUpdate_Reverb_ToggleFreeze() {
			if (liveUpdateFilter is not FreeverbFilter)
				return;  // Not playing the reverb filter

			FreeverbFilterInstance instance = (FreeverbFilterInstance)liveUpdateFilterInstance;
			instance.paramFrozen.Value = !instance.paramFrozen.Value;
		}

		private static void LiveUpdate_ToggleFFTRender() {
			if (liveUpdateStream is null)
				return;  // Not playing any stream

			if (liveUpdateStream.FFT is null) {
				liveUpdateFFT = liveUpdateStream.BeginTrackingFFT();
				liveUpdateFFT.SetGraphToDecayRenderMode(0.962350626398);  // 0.1 ^ (1 / 60)
			} else {
				liveUpdateStream.StopTrackingFFT();
				liveUpdateFFT = null;
			}
		}

		static Process ThisProcess;

		private static double _fftTime;

		protected override void Draw(GameTime gameTime) {
			ThisProcess ??= Process.GetCurrentProcess();

			if (timer % 30 == 0)
				ThisProcess.Refresh();

			GraphicsDevice.Clear(Color.CornflowerBlue);

			_spriteBatch.Begin();

			SpriteFontBase font = _fontSystem.GetFont(18);

			_spriteBatch.DrawString(font, $"FPS: {1 / (float)gameTime.ElapsedGameTime.TotalSeconds}", new Vector2(5, 5), Color.White);
			_spriteBatch.DrawString(font, $"MEM: {ByteCountToLargeRepresentation(ThisProcess.WorkingSet64)} / {ByteCountToLargeRepresentation(ThisProcess.PeakWorkingSet64)}", new Vector2(5, 5 + font.LineHeight + 2), Color.White);

			if (songInstance != null)
				_spriteBatch.DrawString(font, $"chill.ogg / Non-streamed     Pan = {songInstance.Pan}", new Vector2(20, 40), Color.White);
			else if (StreamLoader.IsStreaming(streamedSound)) {
				TimeSpan current = streamedSound.CurrentDuration;
				TimeSpan max = streamedSound.MaxDuration;
				_spriteBatch.DrawString(font, $"chill.ogg / Streamed         {FormatTime(current)} / {FormatTime(max)}", new Vector2(20, 40), Color.White);
			} else if (StreamLoader.IsStreaming(streamedSegmentedSound)) {
				TimeSpan current = streamedSegmentedSound.CurrentDuration;
				TimeSpan max = streamedSegmentedSound.MaxDuration;

				int sectionNum = streamedSegmentedSound.tracker.TargetSegment;
				IAudioSegment section = streamedSegmentedSound.tracker.CurrentSegment;

				_spriteBatch.DrawString(font, $"bonetrousle.ogg / Streamed         {FormatTime(current)} / {FormatTime(max)}", new Vector2(20, 40), Color.White);
				_spriteBatch.DrawString(font, $"Segment #{sectionNum + 1}:  {FormatTime(section.Start)} - {FormatTime(section.End)}", new Vector2(26, 40 + font.LineHeight + 2), Color.White);
			} else if (StreamLoader.IsStreaming(liveUpdateStream)) {
				TimeSpan current = liveUpdateStream.CurrentDuration;

				_spriteBatch.DrawString(font, $"Stickerbush Symphony Restored to HD.mp3 / Streamed         {FormatTime(current)}", new Vector2(20, 40), Color.White);

				string filterStrength = liveUpdateFilterInstance is not null ? $"Strength: {FormatPercent(liveUpdateFilterInstance.paramStrength)}" : null;

				if (liveUpdateFilterInstance is BiquadResonantFilterInstance bqr) {
					string type = bqr.paramType.Value switch {
						BiquadResonantFilter.LOW_PASS => "Low Pass",
						BiquadResonantFilter.HIGH_PASS => "High Pass",
						BiquadResonantFilter.BAND_PASS => "Band Pass",
						_ => "Unknown"
					};

					_spriteBatch.DrawString(font, "Filter: Biquad Resonant", new Vector2(26, 40 + font.LineHeight + 2), Color.White);
					_spriteBatch.DrawString(font, $"Type: {type}", new Vector2(32, 40 + font.LineHeight * 2 + 2), Color.White);
					_spriteBatch.DrawString(font, filterStrength, new Vector2(32, 40 + font.LineHeight * 3 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Frequency: {bqr.paramFrequency.Value:N0} Hz", new Vector2(32, 40 + font.LineHeight * 4 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Resonance: {bqr.paramResonance.Value:N2}", new Vector2(32, 40 + font.LineHeight * 5 + 2), Color.White);
				} else if (liveUpdateFilterInstance is EchoFilterInstance echo) {
					_spriteBatch.DrawString(font, "Filter: Echo", new Vector2(26, 40 + font.LineHeight + 2), Color.White);
					_spriteBatch.DrawString(font, filterStrength, new Vector2(32, 40 + font.LineHeight * 2 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Delay: {echo.paramDelay.Value:N4} s", new Vector2(32, 40 + font.LineHeight * 3 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Decay: {FormatPercent(echo.paramDecay)}", new Vector2(32, 40 + font.LineHeight * 4 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Bias: {FormatPercent(echo.paramBias)}", new Vector2(32, 40 + font.LineHeight * 5 + 2), Color.White);
				} else if (liveUpdateFilterInstance is FreeverbFilterInstance reverb) {
					_spriteBatch.DrawString(font, "Filter: Reverb", new Vector2(26, 40 + font.LineHeight + 2), Color.White);
					_spriteBatch.DrawString(font, filterStrength, new Vector2(32, 40 + font.LineHeight * 2 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Feedback: {reverb.paramFeeback.Value:N2}", new Vector2(32, 40 + font.LineHeight * 3 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Dampness: {reverb.paramDampness.Value:N2}", new Vector2(32, 40 + font.LineHeight * 4 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Stereo Width: {reverb.paramStereoWidth.Value:N2}", new Vector2(32, 40 + font.LineHeight * 5 + 2), Color.White);
					_spriteBatch.DrawString(font, $"Frozen: {reverb.paramFrozen.Value}", new Vector2(32, 40 + font.LineHeight * 6 + 2), Color.White);
				}
			}

			int y = 220;
			foreach (string line in machine.ReportCurrentTree(currentSequence)) {
				_spriteBatch.DrawString(font, line, new Vector2(10, y), Color.White);
				y += font.LineHeight;
			}

			_spriteBatch.End();

			if (StreamLoader.IsStreaming(liveUpdateStream) && liveUpdateFFT is not null) {
				InitFFTGraph(300, 200);
				RenderFFTGraph(400, 40 + font.LineHeight * 6 + 2 + 220);

				_fftTime += gameTime.ElapsedGameTime.TotalSeconds;
			} else
				_fftTime = 0;

			base.Draw(gameTime);
		}

		private static string FormatTime(TimeSpan time) {
			return $"{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";
		}

		private static string FormatPercent(float percent) {
			return $"{percent * 100f:N2}%";
		}

		private static string ByteCountToLargeRepresentation(long bytes) {
			string[] storageSizes = [ "B", "kB", "MB", "GB" ];
			int sizeLog = (int)Math.Log(bytes, 1000);
			double sizePow = (long)Math.Pow(1000, sizeLog);
			string size = storageSizes[sizeLog];

			return $"{bytes / sizePow:N3}{size}";
		}

		private const int GRAPH_PADDING = 15;
		private static VertexBuffer _graphBackgroundVertices, _graphAxesVertices, _graphDataVertices;
		private static IndexBuffer _graphBackgroundIndices, _graphAxesIndices, _graphDataIndices;
		private static BasicEffect _graphEffect;
		private static float _graphWidth, _graphHeight;
		private static int _graphDataVertexCount;

		private void InitFFTGraph(float width, float height) {
			if (_graphEffect is null) {
				_graphEffect = new BasicEffect(GraphicsDevice) {
					VertexColorEnabled = true
				};
			}

			if (_graphBackgroundVertices is null || _graphBackgroundIndices is null) {
				_graphBackgroundVertices?.Dispose();
				_graphBackgroundIndices?.Dispose();

				Color colorBackground = Color.LightGray;

				// Device must not have a buffer assigned for a buffer to be given data
				GraphicsDevice.SetVertexBuffer(null);

				_graphBackgroundVertices = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), 4, BufferUsage.WriteOnly);
				_graphBackgroundVertices.SetData(new VertexPositionColor[] {
					new(new Vector3(0, 0, 0), colorBackground),
					new(new Vector3(width, 0, 0), colorBackground),
					new(new Vector3(0, height, 0), colorBackground),
					new(new Vector3(width, height, 0), colorBackground)
				});

				_graphBackgroundIndices = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
				_graphBackgroundIndices.SetData(new ushort[] {
					// Top left triangle
					0, 1, 2,
					// Bottom right triangle
					1, 3, 2
				});

				_graphWidth = width;
				_graphHeight = height;
			}

			if (_graphAxesVertices is null || _graphAxesIndices is null) {
				_graphAxesVertices?.Dispose();
				_graphAxesIndices?.Dispose();

				Color colorAxes = Color.Black;

				// Device must not have a buffer assigned for a buffer to be given data
				GraphicsDevice.SetVertexBuffer(null);

				_graphAxesVertices = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), 3, BufferUsage.WriteOnly);
				_graphAxesVertices.SetData(new VertexPositionColor[] {
					new(new Vector3(GRAPH_PADDING, GRAPH_PADDING, 0), colorAxes),
					new(new Vector3(GRAPH_PADDING, height - GRAPH_PADDING, 0), colorAxes),
					new(new Vector3(width - GRAPH_PADDING, height - GRAPH_PADDING, 0), colorAxes)
				});

				_graphAxesIndices = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, 4, BufferUsage.WriteOnly);
				_graphAxesIndices.SetData(new ushort[] {
					// Y-axis
					0, 1,
					// X-axis
					1, 2
				});
			}
		}

		private void RenderFFTGraph(float x, float y) {
			// Update the matrix
			var viewport = GraphicsDevice.Viewport;
			_graphEffect.World = Matrix.CreateTranslation(new Vector3(x, y, 0));
			_graphEffect.View = Matrix.CreateScale(1f, 1f, 1f);
			_graphEffect.Projection = Matrix.CreateOrthographicOffCenter(left: 0, right: viewport.Width, bottom: viewport.Height, top: 0, zNearPlane: -1, zFarPlane: 10);

			// Draw the background
			GraphicsDevice.SetVertexBuffer(_graphBackgroundVertices);
			GraphicsDevice.Indices = _graphBackgroundIndices;
			_graphEffect.CurrentTechnique.Passes[0].Apply();
			GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

			// Draw the axes
			GraphicsDevice.SetVertexBuffer(_graphAxesVertices);
			GraphicsDevice.Indices = _graphAxesIndices;
			_graphEffect.CurrentTechnique.Passes[0].Apply();
			GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, 2);

			// Get the data points
			const float X_MIN_FREQ = GRAPH_PADDING + 5;
			float xMaxFreq = _graphWidth - GRAPH_PADDING - 5;
			float yZeroDB = _graphHeight - GRAPH_PADDING - 5;
			const float Y_MIN_DB = GRAPH_PADDING + 5;

			const float MIN_DB = -120f;
			List<FFTGraphPoint> points = [ .. liveUpdateFFT.QueryDBGraph(ref _fftTime) ];

			if (points.Count == 0) {
				// No data to render
				return;
			}

			// Convert the points to vertices
			VertexPositionColor[] vertices = new VertexPositionColor[points.Count * 2];

			float xRange = xMaxFreq - X_MIN_FREQ;
			float yRange = yZeroDB - Y_MIN_DB;

			for (int i = 0; i < points.Count; i++) {
				var point = points[i];

				// Convert the point to a vertex
			//	float pointX = (float)(X_MIN_FREQ + (point.Frequency / liveUpdateFFT.sampleRate) * xRange);
			//	float pointY = (float)(yZeroDB - (point.Value / MIN_DB) * yRange);
				float pointX = (float)(X_MIN_FREQ + point.Frequency * xRange);
				float pointY = (float)(Y_MIN_DB - point.Value / MIN_DB * yRange);

				pointY = Math.Clamp(pointY, Y_MIN_DB, yZeroDB);

				vertices[i * 2] = new VertexPositionColor(new Vector3(pointX, pointY, 0), Color.Blue);
				vertices[i * 2 + 1] = new VertexPositionColor(new Vector3(pointX, yZeroDB, 0), Color.Blue);
			}

			if (_graphDataVertexCount != vertices.Length) {
				_graphDataVertices?.Dispose();
				_graphDataIndices?.Dispose();

				_graphDataVertices = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), vertices.Length, BufferUsage.WriteOnly);
				_graphDataIndices = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, vertices.Length, BufferUsage.WriteOnly);
				_graphDataIndices.SetData(Enumerable.Range(0, points.Count * 2).Select(i => (ushort)i).ToArray());

				_graphDataVertexCount = vertices.Length;
			}

			// Update the data vertices
			// Device must not have a buffer assigned for a buffer to be given data
			GraphicsDevice.SetVertexBuffer(null);
			_graphDataVertices.SetData(vertices);

			// Draw the data
			Rectangle prevScissor = GraphicsDevice.ScissorRectangle;
			GraphicsDevice.ScissorRectangle = new Rectangle(TruncateAwayFromZero(x), TruncateAwayFromZero(y), TruncateAwayFromZero(_graphWidth), TruncateAwayFromZero(_graphHeight));

			GraphicsDevice.SetVertexBuffer(_graphDataVertices);
			GraphicsDevice.Indices = _graphDataIndices;
			_graphEffect.CurrentTechnique.Passes[0].Apply();
			GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, _graphDataVertexCount / 2);

			GraphicsDevice.ScissorRectangle = prevScissor;
		}

		private static int TruncateAwayFromZero(float value) => (int)(value < 0 ? MathF.Ceiling(value) : MathF.Floor(value));
	}
}
