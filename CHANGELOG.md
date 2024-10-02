## v1.7.2
**Fixes:**
- Reduced allocations from `StreamManager` by caching the `IEnumerator<T>` object and only updating it when new streams are added
- Fixed `FormatWav` not implementing the `IDisposable` interface correctly
- Fixed an oversight where `WavStream`, `XnbStream` and `MP3Stream` would not initialize `SteamPackage.PlayingSound` nor `StreamPackage.Metrics`

**API Changes:**
- New types:
  - `class DynamicStreamPackage`
  - `struct PCM16Bit`
  - `struct PCM24Bit`
  - `class PcmFormat`
  - `class PcmStream`
- `CustomAudioFormat`
  - New members:
    - `FormatWav ReadWav(string, object)`
    - `FormatWav ReadWav(Stream, object)`
- `DynamicStreamPackage`
  - An empty, customizable `StreamPackage`
  - Events:
    - `event Action<DynamicStreamPackage> OnReset`
    - `event DynamicStreamPackage.ModifySamplesDelegate OnSamplesRead`
    - `event DynamicStreamPackage.ModifyReadSecondsDelegate OnModifyReadSeconds`
    - `event DynamicStreamPackage.ModifyByteSamplesDelegate OnPreSubmitByteBuffer`
    - `event DynamicStreamPackage.ModifyWaveSamplesDelegate OnPreSubmitWaveBuffer`
  - Methods:
    - `virtual byte[] ReadSamples(double)`
- `EffectLoader`
  - New members:
    - `SoundEffect GetEffect(string, CustomAudioFormat, object)`
    - `SoundEffect GetFilteredEffect(string, CustomAudioFormat, object, int)`
    - `SoundEffect GetMultiFilteredEffect(string, CustomAudioFormat, object, params int[])`
    - `SoundEffect GetEffect(Stream, CustomAudioFormat, object)`
    - `SoundEffect GetFilteredEffect(Stream, CustomAudioFormat, object, string, int)`
    - `SoundEffect GetMultiFilteredEffect(Stream, CustomAudioFormat, object, string, params int[])`
- `FormatWav`
  - Removed members:
    - `byte[] Data { get; }`
  - New members:
    - `static FormatWav FromSampleDataAndSettings(WavSample[], AudioChannels, int, int)`
- `PcmFormat`, `PcmStream`
  - Used to handle reading/streaming from `.pcm` files
  - Automatically registered, but requires a `PcmFormatSettings` object to be loaded or streamed
- `StreamedSoundEffectInstance`
  - Class is now `public`
- `StreamPackage`
  - `StreamedSoundEffectInstance PlayingSound { get; }` is now `public`
  - `void InitSound()` is now `protected`
  - 24-bit PCM sample data is now supported
  - New members:
    - `struct SubmitBufferControls`
      - Interface for setting hidden controls used while queuing and submitting audio data buffers
    - `void PreQueueBuffers(ref StreamPackage.SubmitBufferControls)`
    - `void PreSubmitBuffer(ref byte[])`
      - Called when `PreQueueBuffers()` sets `controls.requestPCMSamplesForEvent = false;`
    - `void PreSubmitBuffer(ref WavSample[])`
	  - Called when `PreQueueBuffers()` sets `controls.requestPCMSamplesForEvent = true;`
- `WavSample`
  - Internal structure has been completely reworked to be more efficient and easier to work with
  - Removed members:
    - `WavSample..ctor(short, data[])`
  - New members:
	- `WavSample..ctor(PCM16Bit)`
	- `WavSample..ctor(PCM24Bit)`
	- `WavSample..ctor(byte[])`
	- `WavSample..ctor(ReadOnlySpan<byte>)`
	- `int SampleSize { get; }`
	- `PCM16Bit Sample16Bit { get; set; }`
	- `PCM24Bit Sample24Bit { get; set; }`
	- `static byte[] ToByteArray(WavSample[])`
  - Replaced members:
    - `short SampleSize` with `int SampleSize { get; }`
	- `byte[] Data { get; set; }` with `PCM16Bit Sample16Bit { get; set; }` and `PCM24Bit Sample24Bit { get; set; }`

**Miscellaneous:**
- Updated the library to .NET 8.0
- Added missing XML summaries

## v1.7.1.1
**Fixes:**
- Added an exception handler to audio stream loading to prevent the worker thread from stopping if an exception is thrown
- Fixed an oversight where `MonoSoundLibrary.DeInit()` would softlock

## v1.7.1
**Fixes:**
- Implemented a hopefully temporary fix for a bug where streamed audio would randomly have the wrong pitch or volume
- Moved audio streaming to a worker thread and an internal `SoundEffectInstance`-deriving type to fix audio popping at lower framerates

**API Changes:**
- Removed the internal `FastReflection` class and its related classes in favor of an assembly publicizer package
- `MonoSoundLibrary`
  - Obsolete members:
    - `void Init()`
      - Replacement: `void Init(Game)`
      - `MonoSoundLibrary` now has to track the `Game` it's assigned to for some new API features
- `Controls`
  - New properties:
    - `StreamFocusBehavior DefaultStreamFocusBehavior { get; set; }`
      - Controls the default behavior for `StreamPackage`s when the game is not focused
      - Defaults to `StreamFocusBehavior.KeepPlaying`
- `StreamPackage`
  - New methods:
    - `void Resume()`
      - Previously missing method for mirroring of SoundEffectInstance handling
      - Pauses the stream
  - New properties:
    - `StreamFocusBehavior? FocusBehavior { get; set; }`
      - Acts as an override of `Controls.DefaultStreamFocusBehavior`
      - Defaults to `null`, which is interpreted as using the value of `Controls.DefaultStreamFocusBehavior`

## v1.7
 **Fixes:**
- Fixed a copy/paste typo which caused the `RegisterBiquadResonantFilter`, `RegisterEchoFilter` and `RegisterReverbFilter` methods in `MonoSoundManager` to mention the wrong methods
- Fixed a bug where the additional samples created by the Echo filter would not be played
- Fixed a bug where `StreamPackage` would fail to loop due to a null-reference error

**API Changes:**
- `Controls.StreamBufferLengthInSeconds` now defaults to `0.01` instead of `0.1`
- `StreamPackage`
  - New methods:
    - `void ClearAudioQueue()`
    - `double GetSecondDuration(long)`
    - `long ModifyResetOffset(long)`
    - `void OnLooping()`
    - `void Pause()`
    - `void Play()`
    - `void SetStreamPosition(double)`
    - `void Stop()`
  - New properties:
    - `TimeSpan CurrentDuration { get; }`
    - `TimeSpan MaxDuration { get; }`
    - `TimeSpan ReadTime { get; }`
    - `SoundMetrics metrics { get; }`
  - Obsolete members:
    - `override void ChildDispose(bool)`
      - Replacement: `override void Dispose(bool)`
  - Member changes:
    - `int ReadBytes` ⇒ `long ReadBytes`
    - `int TotalBytes` ⇒ `long TotalBytes`
    - The setters for `ReadBytes` and `SecondsRead` are now `protected`
    - The setter for `IsLooping` is now `public`
    - The `void HandleLooping()` method is now `virtual protected`
    - The `PlayingSound` property is now `internal`
- `OggStream`
  - `vorbisStream` is now `protected`
  - `vorbisReadStart` ⇒ `loopTargetTime`
  - `loopTargetTime` is now `protected`
- `Filter`
  - Now contains a `bool RequiresSampleMemory` property which is used to prevent the filter from being used by certain APIs
- `EffectLoader` and `SteamLoader` have new methods for handling custom audio formats
- Added `MonoSound.Default.SegmentedOggFormat`
  - Example use case for `CustomAudioFormat`
  - This type should not be registered.  Instead, create a `new SegmentedOggFormat()` and pass it to the `StreamLoader` methods that accept a `CustomAudioFormat` parameter
  - Streams created from this format use the `MonoSound.Default.SegmentedOggStream` type
  - Example usage can be found in the `MonoSound.Tests` project
- Added methods in `FilterLoader` for updating the parameters on an already-existing filters

**Obsolete API:**
- `CustomFileFormat` ⇒ `CustomAudioFormat`
- `MonoSoundLibrary.RegisterFormat(string, Func<Stream, FormatWav>, Func<Stream, StreamPackage>)` ⇒ `MonoSoundLibrary.RegisterFormat(CustomAudioFormat)`

## v1.6.2
- Replaced all usage of File.OpenRead() with TitleContainer.OpenStream(), since the former prevented MonoSound from loading on Android projects

## v1.6.1
- Fixed an issue where filters on a streamed sound would have noise

## v1.6.0.0
- Modified the API to be more user-friendly and sensible
- Made the following classes public:  StreamPackage, WavStream, XnbStream, Mp3Stream, OggStream, WavebankStream
- Fixed an issue that causes sound streaming to not work as intended
- Added an API for reading sound files in custom formats via MonoSoundLibrary.RegisterFormat()
- Improved the logic responsible for caching loaded wavebanks
- Fixed some bugs in the streaming logic which caused looping XWB sounds to sometimes fail
- Added logic to allow streamed sounds to have effects applied to them dynamically
- Added a control for specifying the length of the buffers read for streamed sounds.  Defaults to 0.1 seconds.
- Added proper safeguards to Filter object freeing in order to prevent some nasty CLR crashes
- Added a safeguard for MP3Sharp audio loading

## v1.5.2.0
- Made the MonoSound.Audio.FormatWav class public
- WAVE data with metadata can now be loaded by MonoSound

## v1.5.1.0
- Added support for loading SoundEffect instances from XACT wavebank streams (overload was missing)

## v1.5.0.0
- Added support for loading SoundEffect instances from System.IO.Stream instances
- Added support for applying sound filters to System.IO.Stream instances

## v1.4.0.0
- Added support for streaming from MPEG Audio Layer III (.mp3) files
- Added support for creating SoundEffect instances directly from the supported audio file types (.xnb, .wav, .ogg and .mp3)

## v1.3.0.0
- Added support for streaming from OGG Vorbis (.ogg) sound files

## v1.2.0.0
- Added support for sounds located in XACT sound and wave banks
  - Dislaimer: any looping data stored in the sounds is effectively removed.  Looping sounds will have to be set manually in code via SoundEffectInstance.IsLooped
- Added a sub-manager for streaming sound effects from compiled XNB files and XACT wave banks.
  - Disclaimer: streamed sounds cannot have filters applied to them.

## v1.1.0.0
- Implemented the Freeverb (Reverb) and Echo sound filters from SoLoud
- Sound files can now be passed through multiple filters at once
- OGG Vorbis file format support (.ogg) via NVorbis
- MonoSoundManager contains a new property, AllowEchoOversampling, which controls whether the Echo filter is allowed to extend the original sound by over 30 seconds
- MonoSound can now write the sampled sound to a file.  Set the MonoSoundManager.LogDirectory property to the directory where the sounds will be saved to and set MonoSoundManager.LogFilters to true to enable saving to files

## v1.0.0.0
- First official release
