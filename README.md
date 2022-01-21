# MonoSound
A library for MonoGame projects which allows easy creation of filtered sound via SoLoud's sound filters.  
Supported MonoGame builds: DesktopGL

The sound filter code was ported directly from the C++ sound filter library, SoLoud.

The GitHub repository for SoLoud can be found [here](https://github.com/jarikomppa/soloud).
The documentation for SoLoud can be found [here](http://sol.gfxile.net/soloud/index.html).

The `LICENSE.txt` file included in this repository was taken from the [MonoGame Github repository](https://github.com/MonoGame/MonoGame) due to the following files containing code directly from said repository:

* `Decompressor.cs`
* `Lz4StreamDecoder.cs`
* `LzxStreamDecoder.cs`
* `ReflectionHelpers.cs`

The `LzxDecoder.cs` file also contains certain licenses.

Table of Contents |
--- |
[How it Works](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#how-it-works) |
[Implemented Sound Filters](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#implemented-sound-filters) |
[XACT Sound Playing](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#xact-sound-playing) |
[SoundEffect Loading](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#soundeffect-loading) |
[Sound Streaming](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#sound-streaming) |

### How it Works

MonoSound is able to process WAVE data from the following formats:
- `.wav`: WAV audio
- `.xnb`: Compiled MonoGame `SoundEffect` files
- `.xwb`: XACT Wave Banks
- `.ogg`: OGG Vorbis audio
- `.mp3`: MPEG Audio Layer III audio

First, the library needs to be initialized via `MonoSoundManager.Init();`, preferably in the `Game.LoadContent()` method.  
(Until this method is called, most uses of the library will either result in thrown errors or undefined behaviour.)

Then, custom sound filters can be registered at any time.  See the next section for what sound filters are implemented and examples of using them.

Finally, when the game is closed, `MonoSoundManager.DeInit()` must be called in `Game.UnloadContent()` to free up used resources.

### Implemented Sound Filters

MonoSound currently implements five of SoLoud's sound filters:

1. Low Pass (Biquad Resonant)
2. Band Pass (Biquad Resonant)
3. High Pass (Biquad Resonant)
4. Echo
5. Reverb

See `Filters/SoundFilterType.cs` for explanations of what each sound filter does.

#### Low Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 1x
//   Freqency Cap: 1000 Hz
//     - the frequency at which any frequencies above it are modified
//   Resonance: 5
//     - how strong the pass effect is
int lowPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.LowPass, strength: 1f, frequencyCap: 1000, resonance: 5);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file, a compiled .xnb file or an .ogg file.
SoundEffect lowPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", lowPass);
lowPassEffect.Play();
```

#### Band Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 1x
//   Freqency Cap: 2000 Hz
//     - the frequency at which any frequencies considered "out of range" of it are modified
//   Resonance: 3
//     - how strong the pass effect is
int bandPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.BandPass, strength: 1f, frequencyCap: 2000, resonance: 3);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file, a compiled .xnb file or an .ogg file.
SoundEffect bandPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", bandPass);
bandPassEffect.Play();
```

#### High Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 0.5x
//   Freqency Cap: 1500 Hz
//     - the frequency at which any frequencies below it are modified
//   Resonance: 8
//     - how strong the pass effect is
int highPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.HighPass, strength: 0.5f, frequencyCap: 1500, resonance: 8);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file, a compiled .xnb file or an .ogg file.
SoundEffect highPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", highPass);
highPassEffect.Play();
```

#### Echo Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 0.5x
//   Delay: 0.125 seconds
//     - how frequently new echoes are created
//   Decay Factor: 0.6x
//     - the factor applied to each successive echoes' volume
//   Old Sample Preference Factor: 0.7x
//     - how biased the sampler is towards using the data from the original samples
//       the filtered sample and original sample are mixed regardless
int echo = MonoSoundManager.RegisterEchoFilter(strength: 0.5f, delay: 0.125f, decayFactor: 0.6f, filterStrength: 0.7f);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file, a compiled .xnb file or an .ogg file.
SoundEffect echoEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", echo);
echoEffect.Play();
```

#### Reverb Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 0.5x
//   Low Frequency Reverb Strength: 0.5x
//     - how much the filter affects lower-frequency sounds
//   High Frequency Reverb Strength: 0.5x
//     - how much the filter affects higher-frequency sounds
//   Overall Reverb Strength: 1.0x
//     - how strongly the effect is applied to the original samples
int reverb = MonoSoundManager.RegisterReverbFilter(strength: 0.5f, lowFrequencyReverbStrength: 0.5f, highFrequencyReverbStrength: 0.5f, reverbStrength: 1f);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file, a compiled .xnb file or an .ogg file.
SoundEffect reverbEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", reverb);
reverbEffect.Play();
```

### XACT Sound Playing

MonoSound is able to load specific sounds from XACT wave banks as `SoundEffect`s via `MonoSoundManager.GetEffectFromBank(string, string, string)`.  
DISCLAIMER: the requested wave bank will have all of its sound data loaded into memory if that hasn't happened already.  If this outcome is undesirable, use the [streaming alternative](https://github.com/absoluteAquarian/MonoSound/blob/main/README.md#sound-streaming) instead.  
Furthermore, MonoSound only supports simple Cues.

#### Example
```cs
SoundEffect xactSound = MonoSoundManager.GetEffectFromBank("Content/Sound Bank.xsb", "Content/Wave Bank.xwb", "mysound");
xactSound.Play();
```

### SoundEffect Loading
The pipeline in MonoGame can be completely avoided by using this library.  Below is an example of loading a `SoundEffect` directly from a file and playing it:
```cs
SoundEffect sound = MonoSoundManager.GetEffect("Content/spooky.mp3");
sound.Play();
```

### Sound Streaming

MonoSound has built-in support for streaming sounds from `.wav` files, compiled `.xnb` files, XACT `.xwb` wave banks, OGG Vorbis `.ogg` files and MPEG Audio Layer III `.mp3` files.

In order to register a streamed sound, either `MonoSoundManager.GetStreamedSound(string, bool)` or `MonoSoundManager.GetStreamedXACTSound(string, string, string, bool)` has to be called.  
To stop the streamed sound and its streaming, call `MonoSoundManager.FreeStreamedSound(ref SoundEffectInstance)`.

Do note that once a streamed sound has been registered from an XACT wave bank, that bank will **no longer be able** to be used for non-streaming purposes.  
Furthermore, streamed sounds **cannot** have filters applied to them.

#### WAV/XNB/OGG/MP3 Example
```cs
SoundEffectInstance streamedSound = MonoSoundManager.GetStreamedSound("Content/cool_sound.xnb", looping: false);
streamedSound.Play();

...

//Stop the sound and its streaming.  This method automatically calls Stop() and Dispose() on the instance.
MonoSoundManager.FreeStreamedSound(ref streamedSound);
```

#### XACT Example
```cs
SoundEffectInstance streamedXACTSound = MonoSoundManager.GetStreamedXACTSound("Content/Sound Bank.xsb", "Content/Wave Bank.xwb", "mysound", looping: true);
streamedXACTSound.Play();

...

//Stop the sound and its streaming.  This method automatically calls Stop() and Dispose() on the instance.
MonoSoundManager.FreeStreamedSound(ref streamedXACTSound);
```
