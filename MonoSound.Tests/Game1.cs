using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoSound.Filters;
using MonoSound.Streaming;
using System;
using System.IO;

namespace MonoSound.Tests {
	public class Game1 : Game {
		private GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;

		public Game1() {
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
		}

		protected override void Initialize() {
			// TODO: Add your initialization logic here

			base.Initialize();
		}

		FontSystem _fontSystem;

		protected override void LoadContent() {
			_spriteBatch = new SpriteBatch(GraphicsDevice);

			// TODO: use this.Content to load your game content here

			MonoSound.Init();

			lowPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.LowPass, 1, 1000, 5);
			highPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.HighPass, 1, 1500, 8);
			bandPass = FilterLoader.RegisterBiquadResonantFilter(SoundFilterType.BandPass, 1, 2000, 3);

			Controls.StreamBufferLengthInSeconds = 0.05;

			_fontSystem = new FontSystem();
			_fontSystem.AddFont(File.ReadAllBytes("C:/Windows/Fonts/times.ttf"));
		}

		KeyboardState kb, oldKb;
		SoundEffect sfx, song, filteredSfx;
		SoundEffectInstance songInstance, filteredSfxInstance;
		StreamPackage streamedSound;

		int lowPass, highPass, bandPass;

		int timer;

		protected override void Update(GameTime gameTime) {
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape)) {
				MonoSound.DeInit();
				Exit();
			}

			kb = Keyboard.GetState();

			// Different pan testing (fails due to the sound using Stereo)
			if (IsKeyToggledOn(Keys.P)) {
				if (sfx is null)
					sfx = EffectLoader.GetEffect("Content/MunchMunch-resaved2.wav");

				float revolutionsPerSecond = 0.3f;
				float sin = MathF.Sin(MathHelper.ToRadians(timer * 6f * revolutionsPerSecond));

				sfx.Play(1, 0, sin);
			}

			// Different pan testing (succeeds due to the sound using Mono)
			if (IsKeyToggledOn(Keys.O)) {
				if (streamedSound != null)
					StreamLoader.FreeStreamedSound(ref streamedSound);

				if (song is null)
					song = EffectLoader.GetEffect("Content/chill-mono.ogg");

				if (songInstance is null) {
					songInstance = song.CreateInstance();
					songInstance.Volume = 0.3f;
				} else {
					songInstance.Dispose();
					songInstance = null;
				}

				songInstance.Play();
			}

			if (songInstance != null) {
				float revolutionsPerSecond = 0.6f;
				float sin = MathF.Sin(MathHelper.ToRadians(timer * 6f * revolutionsPerSecond));

				songInstance.Pan = sin;
			}

			// Streamed audio testing
			if (IsKeyToggledOn(Keys.C)) {
				if (songInstance != null) {
					songInstance.Dispose();
					songInstance = null;
				}

				if (streamedSound is null) {
					streamedSound = StreamLoader.GetStreamedSound("Content/chill-mono.ogg", looping: true);
					streamedSound.PlayingSound.Volume = 0.3f;
				}

				streamedSound.PlayingSound.Play();
			}

			// Applying filters to streamed audio testing
			if (streamedSound != null) {
				if (IsKeyToggledOn(Keys.Q))
					streamedSound.ApplyFilters(lowPass);
				else if (IsKeyToggledOn(Keys.W))
					streamedSound.ApplyFilters(highPass);
				else if (IsKeyToggledOn(Keys.E))
					streamedSound.ApplyFilters(bandPass);
			}

			// Audio with filters testing
			if (IsKeyToggledOn(Keys.F)) {
				filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", lowPass);

				filteredSfxInstance?.Dispose();
				filteredSfxInstance = filteredSfx.CreateInstance();

				filteredSfxInstance.Play();
			}

			if (IsKeyToggledOn(Keys.G)) {
				filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", highPass);

				filteredSfxInstance?.Dispose();
				filteredSfxInstance = filteredSfx.CreateInstance();

				filteredSfxInstance.Play();
			}

			if (IsKeyToggledOn(Keys.H)) {
				filteredSfx = EffectLoader.GetFilteredEffect("Content/spooky.mp3", bandPass);

				filteredSfxInstance?.Dispose();
				filteredSfxInstance = filteredSfx.CreateInstance();

				filteredSfxInstance.Play();
			}

			timer++;

			oldKb = kb;

			base.Update(gameTime);
		}

		private bool IsKeyToggledOn(Keys key) => kb.IsKeyDown(key) && !oldKb.IsKeyDown(key);

		protected override void Draw(GameTime gameTime) {
			GraphicsDevice.Clear(Color.CornflowerBlue);

			_spriteBatch.Begin();

			SpriteFontBase font = _fontSystem.GetFont(18);

			_spriteBatch.DrawString(font, $"FPS: {1 / (float)gameTime.ElapsedGameTime.TotalSeconds}", new Vector2(5, 5), Color.White);
			
			if (songInstance != null)
				_spriteBatch.DrawString(font, $"chill.ogg / Non-streamed     Pan = {songInstance.Pan}", new Vector2(20, 80), Color.White);

			_spriteBatch.End();

			base.Draw(gameTime);
		}
	}
}
