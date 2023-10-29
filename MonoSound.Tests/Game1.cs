using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoSound.Default;
using MonoSound.Filters;
using MonoSound.Streaming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MonoSound.Tests {
	public class Game1 : Game {
		private GraphicsDeviceManager _graphics;
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
							new TestState(Keys.D2, "After Current", JumpToSection_0_Delayed)),
						new TestState(Keys.D2, "Middle", null).WithChildren(
							new TestState(Keys.D1, "Immediate", JumpToSection_1_Immediate),
							new TestState(Keys.D2, "After Current", JumpToSection_1_Delayed)),
						new TestState(Keys.D3, "Ending", null).WithChildren(
							new TestState(Keys.D1, "Immediate", JumpToSection_2_Immediate),
							new TestState(Keys.D2, "After Current", JumpToSection_2_Delayed)))),
				new TestState(Keys.D4, "Filtered Sounds", null).WithChildren(
					new TestState(Keys.D1, "Low Pass Filter", PlayLowPassFilteredSound),
					new TestState(Keys.D2, "High Pass Filter", PlayHighPassFilteredSound),
					new TestState(Keys.D3, "Band Pass Filter", PlayBandPassFilteredSound)));

			base.Initialize();
		}

		FontSystem _fontSystem;

		protected override void LoadContent() {
			_spriteBatch = new SpriteBatch(GraphicsDevice);

			// TODO: use this.Content to load your game content here

			MonoSoundLibrary.Init();

			lowPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.LowPass, 1, 1000, 5);
			highPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.HighPass, 1, 1500, 8);
			bandPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.BandPass, 1, 2000, 3);

			_fontSystem = new FontSystem();
			_fontSystem.AddFont(File.ReadAllBytes("C:/Windows/Fonts/times.ttf"));
		}

		static KeyboardState kb, oldKb;
		static SoundEffect sfx, song, filteredSfx;
		static SoundEffectInstance songInstance, filteredSfxInstance;
		static StreamPackage streamedSound;
		static SegmentedOggStream streamedSegmentedSound;

		static int lowPass, highPass, bandPass;

		static int timer;

		static readonly List<Keys> currentSequence = new List<Keys>();

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

			song ??= EffectLoader.GetEffect("Content/chill-mono.ogg");

			if (songInstance is null) {
				songInstance = song.CreateInstance();
				songInstance.Volume = 0.3f;
			} else {
				songInstance.Dispose();
				songInstance = null;
			}

			songInstance.Play();
		}

		private static void PlayStreamedSong() {
			// Streamed audio testing
			if (songInstance != null) {
				songInstance.Dispose();
				songInstance = null;
			}

			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);

			if (!StreamLoader.IsStreaming(streamedSound)) {
				streamedSound = StreamLoader.GetStreamedSound("Content/chill-mono.ogg", looping: true);
				streamedSound.PlayingSound.Volume = 0.3f;
			}

			streamedSound.PlayingSound.Play();
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

			if (!StreamLoader.IsStreaming(streamedSegmentedSound)) {
				streamedSegmentedSound = (SegmentedOggStream)StreamLoader.GetStreamedSound("Content/bonetrousle.ogg", new SegmentedOggFormat(), new IAudioSegment[] {
					// Intro
					new Segment(TimeSpan.Zero, TimeSpan.FromSeconds(31.95)),
					// Main loop
					new Segment(TimeSpan.FromSeconds(31.95), TimeSpan.FromSeconds(89.55)),
					// Ending
					new EndSegment(TimeSpan.FromSeconds(89.55))
				});
				streamedSegmentedSound.PlayingSound.Volume = 0.3f;
			}

			streamedSegmentedSound.PlayingSound.Play();
		}

		private static void StopSegmentedSong() {
			StreamLoader.FreeStreamedSound(ref streamedSegmentedSound);
		}

		private static void ClearSegmentedSongFilters() {
			// Applying filters to streamed audio testing
			streamedSegmentedSound?.ApplyFilters(ids: null);
		}

		private static void ApplyLowPassFilterToSegmentedSong() {
			// Applying filters to streamed audio testing
			streamedSegmentedSound?.ApplyFilters(ids: lowPass);
		}

		private static void ApplyHighPassFilterToSegmentedSong() {
			// Applying filters to streamed audio testing
			streamedSegmentedSound?.ApplyFilters(ids: highPass);
		}

		private static void ApplyBandPassFilterToSegmentedSong() {
			// Applying filters to streamed audio testing
			streamedSegmentedSound?.ApplyFilters(ids: bandPass);
		}

		private static void JumpToSection_0_Immediate() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(0, false);
		}

		private static void JumpToSection_0_Delayed() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(0, true);
		}

		private static void JumpToSection_1_Immediate() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(1, false);
		}

		private static void JumpToSection_1_Delayed() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(1, true);
		}

		private static void JumpToSection_2_Immediate() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(2, false);
		}

		private static void JumpToSection_2_Delayed() {
			if (streamedSegmentedSound is null)
				return;

			streamedSegmentedSound.JumpTo(2, true);
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

		static Process ThisProcess;

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
			else if (streamedSound != null) {
				TimeSpan current = streamedSound.CurrentDuration;
				TimeSpan max = streamedSound.MaxDuration;
				_spriteBatch.DrawString(font, $"chill.ogg / Streamed         {FormatTime(current)} / {FormatTime(max)} s", new Vector2(20, 40), Color.White);
			} else if (streamedSegmentedSound != null) {
				TimeSpan current = streamedSegmentedSound.CurrentDuration;
				TimeSpan max = streamedSegmentedSound.MaxDuration;

				int sectionNum = streamedSegmentedSound.tracker.TargetSegment;
				IAudioSegment section = streamedSegmentedSound.tracker.CurrentSegment;

				_spriteBatch.DrawString(font, $"bonetrousle.ogg / Streamed         {FormatTime(current)} / {FormatTime(max)} s", new Vector2(20, 40), Color.White);
				_spriteBatch.DrawString(font, $"Segment #{sectionNum + 1}:  {FormatTime(section.Start)} - {FormatTime(section.End)} s", new Vector2(26, 40 + font.LineHeight + 2), Color.White);
			}

			int y = 100;
			foreach (string line in machine.ReportCurrentTree(currentSequence)) {
				_spriteBatch.DrawString(font, line, new Vector2(10, y), Color.White);
				y += font.LineHeight;
			}

			_spriteBatch.End();

			base.Draw(gameTime);
		}

		private static string FormatTime(TimeSpan time) {
			return $"{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";
		}

		private static string ByteCountToLargeRepresentation(long bytes) {
			string[] storageSizes = new string[] { "B", "kB", "MB", "GB" };
			int sizeLog = (int)Math.Log(bytes, 1000);
			double sizePow = (long)Math.Pow(1000, sizeLog);
			string size = storageSizes[sizeLog];

			return $"{bytes / sizePow:N3}{size}";
		}
	}
}
