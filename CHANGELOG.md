## v1.7
- Fixed a copy/paste typo which caused the `RegisterBiquadResonantFilter`, `RegisterEchoFilter` and `RegisterReverbFilter` methods in `MonoSoundManager` to mention the wrong methods
- `Controls.StreamBufferLengthInSeconds` now defaults to `0.01` instead of `0.1`
- Updated the `StreamPackage` class:
  - Fixed a bug where looping would fail due to null reference errors
  - Added `double GetSecondDuration(long)`, `long ModifyResetOffset(long)`, `void SetStreamPosition(double)`, `void OnLooping()` and `void ClearAudioQueue()`
  - Added the `TimeSpan CurrentDuration` and `TimeSpan MaxDuration` properties
  - `override void ChildDispose(bool)` is now obsolete.  Use `override void Dispose(bool)` instead
  - `override void Reset()` is now obsolete.  Use `override void Reset(bool)` instead
  - The `int ReadBytes` property was changed to `long ReadBytes`
  - The `int TotalBytes` property was changed to `long TotalBytes`
  - The `ReadBytes` and `SecondsRead` properties now expose their setters to child classes
  - The `IsLooping` property's setter is now publicly visible
  - The `endOfStream` parameter from `void ReadSamples(double, out byte[], out int, out bool)` has been renamed to `checkLooping`
  - `void HandleLooping()` is now a `virtual` method exposed to child classes
- The `CustomFileFormat` type and its API is now obsolete.  Use the `CustomAudioFormat` type instead
  - `MonoSoundLibrary.RegisterFormat(string, Func<Stream, FormatWav>, Func<Stream, StreamPackage>)` is now obsolete.  Use `MonoSoundLibrary.RegisterFormat(CustomAudioFormat)` instead
  - `CustomAudioFormat` does not have to be registered in order to be usable
- `vorbisStream` and `vorbisReadStart` in `OggStream` are now visible to child classes
  - `vorbisReadStart` has been renamed to `loopTargetTime`
- `Filter` now contains a `bool RequiresSampleMemory` property which is used to prevent the filter from being used by certain APIs
- `SteamLoader` has new methods for handling custom audio formats
- Added an example for `CustomAudioFormat`:  `MonoSound.Default.SegmentedOggFormat`
  - This type should not be registered.  Instead, create a `new SegmentedOggFormat()` and pass it to the `StreamLoader` methods that accept a `CustomAudioFormat` parameter
  - Streams created from this format use the `MonoSound.Default.SegmentedOggStream` type
  - Example usage can be found in the `MonoSound.Tests` project

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