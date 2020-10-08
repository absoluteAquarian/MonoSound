# MonoSound
A library for MonoGame projects which allows easy creation of filtered sound via SoLoud's sound filters.

The sound filter code was ported directly from the C++ sound filter library, SoLoud.

The GitHub repository for SoLoud can be found [here](https://github.com/jarikomppa/soloud).
The documentation for SoLoud can be found [here](http://sol.gfxile.net/soloud/index.html).

The `LICENSE.txt` file included in this repository was taken from the [MonoGame Github repository](https://github.com/MonoGame/MonoGame) due to the following files containing code directly from said repository:

* `Decompressor.cs`
* `Lz4StreamDecoder.cs`
* `LzxStreamDecoder.cs`
* `ReflectionHelpers.cs`

The `LzxDecoder.cs` file also contains certain licenses.

### How it Works

MonoSound is able to process WAVE data from `.wav` and compiled `.xnb` files directly.

First, the library needs to be initialized via `MonoSoundManager.Init();`, preferably in the `Game.LoadContent()` method.

Then, custom sound filters can be registered at any time.  See the next section for what sound filters are implemented and examples of implementing them.

Finally, when the game is closed, `MonoSoundManager.DeInit()` must be called in `Game.UnloadContent()` to free up used resources.

### Implemented Sound Filters

MonoSound currently implements three of SoLoud's sound filters:

1. Low Pass (Biquad Resonant)
2. Band Pass (Biquad Resonant)
3. High Pass (Biquad Resonant)

See `Filters/SoundFilterType.cs` for explanations of what each sound filter does.

#### Low Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 1x
//   Freqency Cap: 2000 Hz
//   Resonance: 5
int lowPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.LowPass, strength: 1f, frequencyCap: 2000, resonance: 5);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file or a compiled .xnb file.
SoundEffect lowPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", lowPass);
lowPassEffect.Play();
```

#### Band Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 1x
//   Freqency Cap: 4500 Hz
//   Resonance: 3
int bandPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.BandPass, strength: 1f, frequencyCap: 4500, resonance: 3);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file or a compiled .xnb file.
SoundEffect bandPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", bandPass);
bandPassEffect.Play();
```

#### High Pass Example
```cs
// NOTE: Sound filters should only be registered once!  Cache the return value and use it later.
// Filter parameters:
//   Strength ("Wetness"): 0.5x
//   Freqency Cap: 7000 Hz
//   Resonance: 8
int highPass = MonoSoundManager.RegisterBiquadResonantFilter(SoundFilterType.HighPass, strength: 0.5f, frequencyCap: 7000, resonance: 8);
// GetFilteredEffect() can use either a relative path or an absolute path.  The file provided must either be a .wav file or a compiled .xnb file.
SoundEffect highPassEffect = MonoSoundManager.GetFilteredEffect("mysound.wav", highPass);
highPassEffect.Play();
```
