# MonoSound
[![NuGet download count](https://img.shields.io/nuget/dt/MonoSound)](https://www.nuget.org/packages/MonoSound)

A library for MonoGame projects which provides an alternative to SoundEffect instance creation, among other useful features.  
Supported MonoGame builds: DesktopGL, Android  
Other builds may be supported, but are currently *untested*.

The sound filter code was ported directly from the C++ sound filter library, SoLoud.

The GitHub repository for SoLoud can be found [here](https://github.com/jarikomppa/soloud).
The documentation for SoLoud can be found [here](http://sol.gfxile.net/soloud/index.html).

The `LICENSE.txt` file included in this repository was taken from the [MonoGame Github repository](https://github.com/MonoGame/MonoGame) due to the following files containing code directly from said repository:

* `Decompressor.cs`
* `Lz4StreamDecoder.cs`
* `LzxStreamDecoder.cs`
* `ReflectionHelpers.cs`

The `LzxDecoder.cs` file also contains certain licenses.

### INFORMATION

MonoSound is able to natively process WAVE data from the following formats:
- `.wav`: WAV audio
- `.xnb`: Compiled MonoGame `SoundEffect` files
- `.xwb`: XACT Wave Banks
- `.ogg`: OGG Vorbis audio
- `.mp3`: MPEG Audio Layer III audio
- `.pcm`: Raw PCM audio

Custom audio formats can also be manually registered.

Make sure to check out the wiki at https://github.com/absoluteAquarian/MonoSound/wiki

### SUPPORT

Want to help support the development of my projects?  Subscribe to the [absoluteAquarian Patreon](https://www.patreon.com/absoluteAquarian).