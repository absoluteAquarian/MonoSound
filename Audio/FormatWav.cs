using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MonoSound.Audio{
	internal sealed class FormatWav : IDisposable{
		private byte[] data;

		/// <summary>
		/// Gets a clone of the underlying byte stream
		/// </summary>
		public byte[] Data => (byte[])data.Clone();

		//Header data is always the first 44 bytes
		/// <summary>
		/// "RIFF" if the file stores its values as Little-Endian, "RIFX" otherwise
		/// </summary>
		public string EndianHeader => Encoding.UTF8.GetString(data, 0, 4);
		/// <summary>
		/// The total size of the file in bytes
		/// </summary>
		public int Size => BitConverter.ToInt32(data, 4);
		/// <summary>
		/// The type of audio saved.  Always set to "WAVE"
		/// </summary>
		public string FileTypeHeader => Encoding.UTF8.GetString(data, 8, 4);
		/// <summary>
		/// The format chunk marker.  Always set to "fmt "
		/// </summary>
		public string FormatChunkMarker => Encoding.UTF8.GetString(data, 12, 4);
		/// <summary>
		/// The length of the format data in bytes
		/// </summary>
		public int FormatLength => BitConverter.ToInt32(data, 16);
		/// <summary>
		/// The audio format each sample is saved as.
		/// <para>PCM is 1</para>
		/// <para>Any other values are assumed to be some other form of compression</para>
		/// </summary>
		public short FormatType => BitConverter.ToInt16(data, 20);
		/// <summary>
		/// What channels this WAV sound will use.
		/// <para>Mono is 1</para>
		/// <para>Stereo is 2</para>
		/// </summary>
		public short ChannelCount => BitConverter.ToInt16(data, 22);
		/// <summary>
		/// How many samples are played per second.  Measured in Hertz (Hz)
		/// </summary>
		public int SampleRate => BitConverter.ToInt32(data, 24);
		/// <summary>
		/// How many bytes are played per second
		/// <para>Usually set to: <c>SampleRate * BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public int ByteRate => BitConverter.ToInt32(data, 28);
		/// <summary>
		/// The number of bytes per sample including all channels
		/// <para>Usually set to: <c>BitsPerSample * ChannelCount / 8</c></para>
		/// </summary>
		public short BlockAlign => BitConverter.ToInt16(data, 32);
		/// <summary>
		/// How many bits PER CHANNEL are in one sample
		/// </summary>
		public short BitsPerSample => BitConverter.ToInt16(data, 34);
		/// <summary>
		/// The data chunk marker.  Always set to "data"
		/// </summary>
		public string DataChunkMarker => Encoding.UTF8.GetString(data, 36, 4);
		/// <summary>
		/// The length of the sample data in bytes
		/// </summary>
		public int DataLength => BitConverter.ToInt32(data, 40);

		public WavSample[] GetSamples(){
			byte[] audioData = GetSoundBytes();

			List<WavSample> samples = new List<WavSample>();
			int size = BitsPerSample / 8;
			
			for(int i = 0; i < audioData.Length; i += size){
				byte[] pass = new byte[size];
				Array.Copy(audioData, i, pass, 0, size);
				samples.Add(new WavSample((short)(BitsPerSample / 8), pass));
			}

			return samples.ToArray();
		}

		public byte[] GetSoundBytes(){
			byte[] audioData = new byte[DataLength];
			Buffer.BlockCopy(data, 44, audioData, 0, audioData.Length);
			return audioData;
		}

		private FormatWav(){ }

		public static FormatWav FromFile(string file){
			if(Path.GetExtension(file) != ".wav")
				throw new ArgumentException("File must be a .wav file", "file");

			using(BinaryReader reader = new BinaryReader(File.OpenRead(file)))
			using(MemoryStream stream = new MemoryStream()){
				reader.BaseStream.CopyTo(stream);
				return FromBytes(stream.GetBuffer());
			}
		}

		public static FormatWav FromDecompressorData(byte[] sampleData, byte[] header){
			byte[] addon = new byte[44];
			addon[0] = (byte)'R';
			addon[1] = (byte)'I';
			addon[2] = (byte)'F';
			addon[3] = (byte)'F';

			//Excluded: Total file size
			
			addon[8] = (byte)'W';
			addon[9] = (byte)'A';
			addon[10] = (byte)'V';
			addon[11] = (byte)'E';
			
			addon[12] = (byte)'f';
			addon[13] = (byte)'m';
			addon[14] = (byte)'t';
			addon[15] = (byte)' ';

			//Format header length
			byte[] arr = BitConverter.GetBytes(header.Length);
			addon[16] = arr[0];
			addon[17] = arr[1];
			addon[18] = arr[2];
			addon[19] = arr[3];

			//Type of Format, Number of Channels, Samples per Second, Bytes per Second, Block Align, Bits per Sample
			Buffer.BlockCopy(header, 0, addon, 20, header.Length);

			addon[36] = (byte)'d';
			addon[37] = (byte)'a';
			addon[38] = (byte)'t';
			addon[39] = (byte)'a';

			arr = BitConverter.GetBytes(sampleData.Length);
			addon[40] = arr[0];
			addon[41] = arr[1];
			addon[42] = arr[2];
			addon[43] = arr[3];

			byte[] actualStream = new byte[addon.Length + sampleData.Length];

			//Copy the data
			Buffer.BlockCopy(addon, 0, actualStream, 0, addon.Length);
			Buffer.BlockCopy(sampleData, 0, actualStream, addon.Length, sampleData.Length);

			arr = BitConverter.GetBytes(actualStream.Length);
			actualStream[4] = arr[0];
			actualStream[5] = arr[1];
			actualStream[6] = arr[2];
			actualStream[7] = arr[3];

			return FromBytes(actualStream);
		}

		public static FormatWav FromBytes(byte[] data){
			if(data.Length < 44)
				throw new ArgumentException("Data was too short to contain a header.", "data");

			FormatWav wav = new FormatWav(){
				data = data
			};

			//Verify that the input data was correct
			try{
				string eHeader = wav.EndianHeader;
				if((eHeader != "RIFF" && eHeader != "RIFX") || wav.FileTypeHeader != "WAVE" || wav.FormatChunkMarker != "fmt " || wav.DataChunkMarker != "data")
					throw new Exception("A header string was invalid.");

				if(data.Length != wav.Size)
					throw new Exception("File size did not match stored size.");

				int sampleRate = wav.SampleRate;
				if(sampleRate < 8000 || sampleRate > 48000)
					throw new Exception("Sample rate was outside the range of valid values.");
			}catch(Exception ex){
				throw new ArgumentException("Data was invalid for the WAV format.", "data", ex);
			}

			return wav;
		}

		public void SaveToFile(string file){
			if(Path.GetExtension(file) != ".wav")
				throw new ArgumentException("Destination file must be a .wav file", "file");

			Directory.CreateDirectory(Path.GetDirectoryName(file));

			using(BinaryWriter writer = new BinaryWriter(File.Open(file, FileMode.Create))){
				writer.Write(data);
			}
		}

		public void DeconstructToFloatSamples(out float[] allSamples){
			int length = DataLength / 2;
			
			WavSample[] samples = GetSamples();

			//All of the data goes to one "channel" since it's processed in pairs anyway
			allSamples = new float[length];

			for(int i = 0; i < samples.Length; i++)
				allSamples[i] = samples[i].ToFloatSample();
		}

		public void ReconstructFromFloatSamples(float[] allSamples){
			if(BitsPerSample == 16){
				byte[] newData = new byte[allSamples.Length * 2];

				for(int i = 0; i < allSamples.Length; i++){
					short convert = (short)(allSamples[i] * short.MaxValue);
					byte[] temp = BitConverter.GetBytes(convert);
					newData[2 * i] = temp[0];
					newData[2 * i + 1] = temp[1];
				}

				Buffer.BlockCopy(newData, 0, data, 44, newData.Length);
			}else if(BitsPerSample == 24){
				byte[] newData = new byte[allSamples.Length * 3];

				for(int i = 0; i < allSamples.Length; i++){
					int convert = (int)(allSamples[i] * WavSample.MaxValue_24BitPCM);
					byte[] temp = BitConverter.GetBytes(convert);

					if(BitConverter.IsLittleEndian){
						newData[3 * i] = temp[1];
						newData[3 * i + 1] = temp[2];
						newData[3 * i + 2] = temp[3];
					}else{
						newData[3 * i] = temp[3];
						newData[3 * i + 1] = temp[2];
						newData[3 * i + 2] = temp[1];
					}
					
					Buffer.BlockCopy(newData, 0, data, 44, newData.Length);
				}
			}else
				throw new InvalidOperationException("Attempted to process data for a bit depth that's not supported.  WAV files must use 16-bit or 24-bit PCM data");
		}

		private bool disposed;

		~FormatWav(){
			Dispose(false);
			GC.SuppressFinalize(this);
		}

		public void Dispose(){
			Dispose(true);
		}

		public void Dispose(bool disposing){
			if(!disposed){
				if(disposing){
					data = null;
				}

				disposed = true;
			}
		}
	}
}
