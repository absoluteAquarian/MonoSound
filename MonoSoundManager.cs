using Microsoft.Xna.Framework.Audio;
using MonoSound.Filters;
using System;
using System.IO;

namespace MonoSound {
	/// <summary>
	/// The class where all sound filtering is handled through.
	/// </summary>
	public static class MonoSoundManager {
		/// <inheritdoc cref="MonoSoundLibrary.NextFilterID"/>
		[Obsolete("Use MonoSoundLibrary.NextFilterID instead", error: true)]
		public static int NextFilterID {
			get => MonoSoundLibrary.NextFilterID;
			private set => MonoSoundLibrary.NextFilterID = value;
		}

		/// <inheritdoc cref="Controls.AllowEchoOversampling"/>
		[Obsolete("Use Controls.AllowEchoOversampling instead", error: true)]
		public static bool AllowEchoOversampling {
			get => Controls.AllowEchoOversampling;
			set => Controls.AllowEchoOversampling = value;
		}

		/// <inheritdoc cref="Controls.LogDirectory"/>
		[Obsolete("Use Controls.LogDirectory instead", error: true)]
		public static string LogDirectory {
			get => Controls.LogDirectory;
			set => Controls.LogDirectory = value;
		}

		/// <inheritdoc cref="Controls.LogFilters"/>
		[Obsolete("Use Controls.LogFilters instead", error: true)]
		public static bool LogFilters {
			get => Controls.LogFilters;
			set => Controls.LogFilters = value;
		}

		/// <inheritdoc cref="MonoSoundLibrary.Version"/>
		[Obsolete("Use MonoSoundLibrary.Version instead")]
		public static readonly string Version = MonoSoundLibrary.Version;

		/// <inheritdoc cref="MonoSoundLibrary.Init"/>
		[Obsolete("Use MonoSoundLibrary.Init instead", error: true)]
		public static void Init() => MonoSoundLibrary.Init();

		/// <inheritdoc cref="MonoSoundLibrary.DeInit"/>
		[Obsolete("Use MonoSoundLibrary.DeInit instead", error: true)]
		public static void DeInit() => MonoSoundLibrary.DeInit();

		/// <inheritdoc cref="MonoSoundLibrary.SetMaxFilterCount(int)"/>
		[Obsolete("Use MonoSoundLibrary.SetMaxFilterCount instead", error: true)]
		public static void SetMaxFilterCount(int count) => MonoSoundLibrary.SetMaxFilterCount(count);

		/// <inheritdoc cref="MonoSoundLibrary.ClearFilters"/>
		[Obsolete("Use MonoSoundLibrary.ClearFilters instead", error: true)]
		public static void ClearFilters() => MonoSoundLibrary.ClearFilters();

		/// <inheritdoc cref="EffectLoader.GetEffect(string)"/>
		[Obsolete("Use EffectLoader.GetEffect instead", error: true)]
		public static SoundEffect GetEffect(string file) => EffectLoader.GetEffect(file);

		/// <inheritdoc cref="EffectLoader.GetFilteredEffect(string, int)"/>
		[Obsolete("Use EffectLoader.GetFilteredEffect instead", error: true)]
		public static SoundEffect GetFilteredEffect(string file, int filterID) => EffectLoader.GetFilteredEffect(file, filterID);

		/// <inheritdoc cref="EffectLoader.GetMultiFilteredEffect(string, int[])"/>
		[Obsolete("Use EffectLoader.GetMultiFilteredEffect instead", error: true)]
		public static SoundEffect GetMultiFilteredEffect(string file, params int[] filterIDs) => EffectLoader.GetMultiFilteredEffect(file, filterIDs);

		/// <inheritdoc cref="EffectLoader.GetEffect(Stream, AudioType)"/>
		[Obsolete("Use EffectLoader.GetEffect instead", error: true)]
		public static SoundEffect GetEffect(Stream stream, AudioType fileType) => EffectLoader.GetEffect(stream, fileType);

		/// <inheritdoc cref="EffectLoader.GetFilteredEffect(Stream, AudioType, string, int)"/>
		[Obsolete("Use EffectLoader.GetFilteredEffect instead", error: true)]
		public static SoundEffect GetFilteredEffect(Stream stream, AudioType type, string nameIndicator, int filterID) => EffectLoader.GetFilteredEffect(stream, type, nameIndicator, filterID);

		/// <inheritdoc cref="EffectLoader.GetMultiFilteredEffect(Stream, AudioType, string, int[])"/>
		[Obsolete("Use EffectLoader.GetMultiFilteredEffect instead", error: true)]
		public static SoundEffect GetMultiFilteredEffect(Stream stream, AudioType type, string nameIndicator, params int[] filterIDs) => EffectLoader.GetMultiFilteredEffect(stream, type, nameIndicator, filterIDs);

		/// <inheritdoc cref="EffectLoader.GetEffectFromBank(string, string, string)"/>
		[Obsolete("Use EffectLoader.GetEffectFromBank instead", error: true)]
		public static SoundEffect GetEffectFromBank(string soundBankFile, string waveBankFile, string cueName) => EffectLoader.GetEffectFromBank(soundBankFile, waveBankFile, cueName);

		/// <inheritdoc cref="EffectLoader.GetBankFilteredEffect(string, string, string, int)"/>
		[Obsolete("Use EffectLoader.GetBankFilteredEffect instead", error: true)]
		public static SoundEffect GetBankFilteredEffect(string soundBankFile, string waveBankFile, string cueName, int filterID) => EffectLoader.GetBankFilteredEffect(soundBankFile, waveBankFile, cueName, filterID);

		/// <inheritdoc cref="EffectLoader.GetBankMultiFilteredEffect(string, string, string, int[])"/>
		[Obsolete("Use EffectLoader.GetBankMultiFilteredEffect instead", error: true)]
		public static SoundEffect GetBankMultiFilteredEffect(string soundBankFile, string waveBankFile, string cueName, params int[] filterIDs) => EffectLoader.GetBankMultiFilteredEffect(soundBankFile, waveBankFile, cueName, filterIDs);

		/// <inheritdoc cref="EffectLoader.GetMultiFilteredEffect(Stream, AudioType, string, int[])"/>
		[Obsolete("Use EffectLoader.GetBankEffect instead", error: true)]
		public static SoundEffect GetBankEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName) => EffectLoader.GetBankEffect(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName);

		/// <inheritdoc cref="EffectLoader.GetBankFilteredEffect(Stream, string, Stream, string, string, int)"/>
		[Obsolete("Use EffectLoader.GetBankFilteredEffect instead", error: true)]
		public static SoundEffect GetBankFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, int filterID) => EffectLoader.GetBankFilteredEffect(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, filterID);

		/// <inheritdoc cref="EffectLoader.GetBankMultiFilteredEffect(Stream, string, Stream, string, string, int[])"/>
		[Obsolete("Use EffectLoader.GetBankMultiFilteredEffect instead", error: true)]
		public static SoundEffect GetBankMultiFilteredEffect(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, params int[] filterIDs) => EffectLoader.GetBankMultiFilteredEffect(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, filterIDs);

		/// <inheritdoc cref="StreamLoader.GetStreamedSound(string, bool)"/>
		[Obsolete("Use StreamLoader.GetStreamedSound instead", error: true)]
		public static SoundEffectInstance GetStreamedSound(string filePath, bool looping) => StreamLoader.GetStreamedSound(filePath, looping).PlayingSound;

		/// <inheritdoc cref="StreamLoader.GetStreamedSound(Stream, AudioType, bool)"/>
		[Obsolete("Use StreamLoader.GetStreamedSound instead", error: true)]
		public static SoundEffectInstance GetStreamedSound(Stream sampleSource, AudioType fileIdentifier, bool looping) => StreamLoader.GetStreamedSound(sampleSource, fileIdentifier, looping).PlayingSound;

		/// <inheritdoc cref="StreamLoader.GetStreamedXACTSound(string, string, string, bool)"/>
		[Obsolete("Use StreamLoader.GetStreamedXACTSound instead", error: true)]
		public static SoundEffectInstance GetStreamedXACTSound(string soundBankPath, string waveBankPath, string cueName, bool looping) => StreamLoader.GetStreamedXACTSound(soundBankPath, waveBankPath, cueName, looping).PlayingSound;

		/// <inheritdoc cref="StreamLoader.GetStreamedXACTSound(Stream, string, Stream, string, string, bool)"/>
		[Obsolete("Use StreamLoader.GetStreamedXACTSound instead", error: true)]
		public static SoundEffectInstance GetStreamedXACTSound(Stream soundBankSource, string soundBankIdentifier, Stream waveBankSource, string waveBankIdentifier, string cueName, bool looping) => StreamLoader.GetStreamedXACTSound(soundBankSource, soundBankIdentifier, waveBankSource, waveBankIdentifier, cueName, looping).PlayingSound;

		/// <inheritdoc cref="StreamLoader.FreeStreamedSound(ref SoundEffectInstance)"/>
		[Obsolete("Use StreamLoader.FreeStreamedSound instead", error: true)]
		public static void FreeStreamedSound(ref SoundEffectInstance instance) => StreamLoader.FreeStreamedSound(ref instance);

		/// <inheritdoc cref="FilterLoader.RegisterBiquadResonantFilter(SoundFilterType, float, float, float)"/>
		[Obsolete("Use StreamLoader.FreeStreamedSound instead", error: true)]
		public static int RegisterBiquadResonantFilter(SoundFilterType type, float strength, float frequencyCap, float resonance) => FilterLoader.RegisterBiquadResonantFilter(type, strength, frequencyCap, resonance);

		/// <inheritdoc cref="FilterLoader.RegisterEchoFilter(float, float, float, float)"/>
		[Obsolete("Use StreamLoader.FreeStreamedSound instead", error: true)]
		public static int RegisterEchoFilter(float strength, float delay, float decayFactor, float filterStrength) => FilterLoader.RegisterEchoFilter(strength, delay, decayFactor, filterStrength);

		/// <inheritdoc cref="FilterLoader.RegisterReverbFilter(float, float, float, float)"/>
		[Obsolete("Use StreamLoader.FreeStreamedSound instead", error: true)]
		public static int RegisterReverbFilter(float filterStrength, float lowFrequencyReverbStrength, float highFrequencyReverbStrength, float reverbStrength) => FilterLoader.RegisterReverbFilter(filterStrength, lowFrequencyReverbStrength, highFrequencyReverbStrength, reverbStrength);
	}
}
