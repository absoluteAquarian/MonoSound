// MIT License - Copyright (C) The Mono.Xna Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using MonoSound.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#pragma warning disable IDE0059, IDE0060

namespace MonoSound{
	internal static class Decompressor{
		const byte ContentCompressedLzx = 0x80;
		const byte ContentCompressedLz4 = 0x40;

		private static readonly List<char> targetPlatformIdentifiers = [
			'w', // Windows (XNA & DirectX)
			'x', // Xbox360 (XNA)
			'm', // WindowsPhone7.0 (XNA)
			'i', // iOS
			'a', // Android
			'd', // DesktopGL
			'X', // MacOSX
			'W', // WindowsStoreApp
			'n', // NativeClient
			'M', // WindowsPhone8
			'r', // RaspberryPi
			'P', // PlayStation4
			'v', // PSVita
			'O', // XboxOne
			'S', // Nintendo Switch
			'G', // Google Stadia
			'b', // WebAssembly and Bridge.NET

			// NOTE: There are additional idenfiers for consoles that 
			// are not defined in this repository.  Be sure to ask the
			// console port maintainers to ensure no collisions occur.

			
			// Legacy identifiers... these could be reused in the
			// future if we feel enough time has passed.

			'p', // PlayStationMobile
			'g', // Windows (OpenGL)
			'l', // Linux
		];

		private static readonly Dictionary<Type, ContentTypeReader> _contentReadersCache;

		private static Dictionary<Type, ContentTypeReader> _contentReaders;

		private static readonly string _assemblyName;

		private static readonly Dictionary<string, Func<ContentTypeReader>> typeCreators = [];

		static Decompressor(){
			_contentReadersCache = new Dictionary<Type, ContentTypeReader>(255);
			_assemblyName = ReflectionHelpers.GetAssembly(typeof(ContentTypeReaderManager)).FullName;
		}

		/// <summary>
		/// Converts the SoundEffect .xnb file into its uncompressed audio data
		/// </summary>
		/// <param name="path">The path to the file</param>
		/// <param name="pcmData">The PCM data from the extracted sound, including things like channel count and sample rate</param>
		/// <param name="header">The extracted header data from the XNB file</param>
		/// <returns></returns>
		public static byte[] DecompressSoundEffectXNB(string path, out PCMData pcmData, out byte[] header){
			if(Path.GetExtension(path) != ".xnb")
				throw new ArgumentException("File must be an XNB file.", "path");
			
			Stream stream = TitleContainer.OpenStream(path);
			byte[] data;

			using(BinaryReader reader = new BinaryReader(stream)){
				Stream decompressedStream = Pre_ReadAsset(reader, stream);
				
				using BinaryReader decompressedReader = new BinaryReader(decompressedStream);
				
				data = Simulate_ContentReader_ReadAsset(decompressedReader, out pcmData, out header);
			}

			return data;
		}

		/// <summary>
		/// Converts the SoundEffect .xnb file into its uncompressed audio data
		/// </summary>
		/// <param name="stream">The stream to retrieve the audio data from</param>
		/// <param name="pcmData">The PCM data from the extracted sound, including things like channel count and sample rate</param>
		/// <param name="header">The extracted header data from the XNB file</param>
		/// <returns></returns>
		public static byte[] DecompressSoundEffectXNB(Stream stream, out PCMData pcmData, out byte[] header){
			byte[] data;

			using(BinaryReader reader = new BinaryReader(stream)){
				Stream decompressedStream = Pre_ReadAsset(reader, stream);
				
				using BinaryReader decompressedReader = new BinaryReader(decompressedStream);
				
				data = Simulate_ContentReader_ReadAsset(decompressedReader, out pcmData, out header);
			}

			return data;
		}

		private static Stream Pre_ReadAsset(BinaryReader reader, Stream stream){
			// The first 4 bytes should be the "XNB" header. i use that to detect an invalid file
			byte x = reader.ReadByte();
			byte n = reader.ReadByte();
			byte b = reader.ReadByte();
			byte platform = reader.ReadByte();

			if(x != 'X' || n != 'N' || b != 'B' || !targetPlatformIdentifiers.Contains((char)platform))
				throw new ContentLoadException("Asset does not appear to be a valid XNB file. Did you process your content for Windows?");

			byte version = reader.ReadByte();
			byte flags = reader.ReadByte();

			bool compressedLzx = (flags & ContentCompressedLzx) != 0;
			bool compressedLz4 = (flags & ContentCompressedLz4) != 0;
			if(version != 5 && version != 4)
				throw new ContentLoadException("Invalid XNB version");

			// The next int32 is the length of the XNB file
			int xnbLength = reader.ReadInt32();

			Stream decompressedStream = null;
			if(compressedLzx || compressedLz4){
				// Decompress the xnb
				int decompressedSize = reader.ReadInt32();

				if(compressedLzx){
					int compressedSize = xnbLength - 14;
					decompressedStream = new LzxDecoderStream(stream, decompressedSize, compressedSize);
				}else if(compressedLz4)
					decompressedStream = new Lz4DecoderStream(stream);
			}else
				decompressedStream = stream;

			return decompressedStream;
		}

		private static byte[] Simulate_ContentReader_ReadAsset(BinaryReader reader, out PCMData pcmData, out byte[] header){
			//LoadAssetReaders
			Simulate_ContentTypeReaderManager_LoadAssetReaders(reader);

			//InitializeTypeReaders
			Simulate_Read7BitEncodedInt(reader);

			//InnerReadObject
			Simulate_Read7BitEncodedInt(reader);

			return Simulate_SoundEffectReader_Read(reader, out pcmData, out header);
		}

		private static byte[] Simulate_SoundEffectReader_Read(BinaryReader reader, out PCMData pcmData, out byte[] header){
			// XNB format for SoundEffect...
			//            
			// Byte [format size]	Format	WAVEFORMATEX structure
			// UInt32	Data size	
			// Byte [data size]	Data	Audio waveform data
			// Int32	Loop start	In bytes (start must be format block aligned)
			// Int32	Loop length	In bytes (length must be format block aligned)
			// Int32	Duration	In milliseconds

			// The header containss the WAVEFORMATEX header structure
			// defined as the following...
			//
			//  WORD  wFormatTag;       // byte[0]  +2
			//  WORD  nChannels;        // byte[2]  +2
			//  DWORD nSamplesPerSec;   // byte[4]  +4
			//  DWORD nAvgBytesPerSec;  // byte[8]  +4
			//  WORD  nBlockAlign;      // byte[12] +2
			//  WORD  wBitsPerSample;   // byte[14] +2
			//  WORD  cbSize;           // byte[16] +2
			//
			// We let the sound effect deal with parsing this based
			// on what format the audio data actually is.

			int headerSize = reader.ReadInt32();
			header = reader.ReadBytes(headerSize);

			// Read the audio data buffer.
			int dataSize = reader.ReadInt32();
			byte[] data = new byte[dataSize];
			reader.Read(data, 0, dataSize);

			int loopStart = reader.ReadInt32();
			int loopLength = reader.ReadInt32();
			int durationMs = reader.ReadInt32();

			pcmData = Simulate_SoundEffect_ctor(header, data, dataSize, durationMs, loopStart, loopLength);

			return data;
		}

		private static PCMData Simulate_SoundEffect_ctor(byte[] header, byte[] buffer, int bufferSize, int durationMs, int loopStart, int loopLength){
			// Peek at the format... handle regular PCM data.
			var format = BitConverter.ToInt16(header, 0);
			if(format == 1){
				var channels = BitConverter.ToInt16(header, 2);
				var sampleRate = BitConverter.ToInt32(header, 4);
				int bitsPerSample = BitConverter.ToInt16(header, 14);
				Simulate_SoundEffect_PlatformInitializePcm(buffer, 0, ref bufferSize, ref bitsPerSample, sampleRate, (AudioChannels)channels, loopStart, loopLength);

				PCMData data = new PCMData(){
					channels = (AudioChannels)channels,
					sampleRate = sampleRate,
					bitsPerSample = (short)bitsPerSample,
					duration = durationMs,
					loopStart = loopStart,
					loopLength = loopLength
				};

				return data;
			}

			throw new InvalidOperationException("Sound file wasn't encoded with PCM data");
		}

		private static void Simulate_SoundEffect_PlatformInitializePcm(byte[] buffer, int offset, ref int count, ref int sampleBits, int sampleRate, AudioChannels channels, int loopStart, int loopLength){
			if(sampleBits == 24){
				// Convert 24-bit signed PCM to 16-bit signed PCM
				buffer = Simulate_AudioLoader_Convert24To16(buffer, offset, count);
				offset = 0;
				count = buffer.Length;
				sampleBits = 16;
			}
		}

		// Convert buffer containing 24-bit signed PCM wav data to a 16-bit signed PCM buffer
		private static unsafe byte[] Simulate_AudioLoader_Convert24To16(byte[] data, int offset, int count)
		{
			if ((offset + count > data.Length) || ((count % 3) != 0))
				throw new ArgumentException("Invalid 24-bit PCM data received");
			// Sample count includes both channels if stereo
			var sampleCount = count / 3;
			var outData = new byte[sampleCount * sizeof(short)];
			fixed (byte* src = &data[offset])
			{
				fixed (byte* dst = &outData[0])
				{
					var srcIndex = 0;
					var dstIndex = 0;
					for (int i = 0; i < sampleCount; ++i)
					{
						// Drop the least significant byte from the 24-bit sample to get the 16-bit sample
						dst[dstIndex] = src[srcIndex + 1];
						dst[dstIndex + 1] = src[srcIndex + 2];
						dstIndex += 2;
						srcIndex += 3;
					}
				}
			}
			return outData;
		}

		private static void Simulate_ContentTypeReaderManager_LoadAssetReaders(BinaryReader reader){
			int numberOfReaders = Simulate_Read7BitEncodedInt(reader);
			var contentReaders = new ContentTypeReader[numberOfReaders];
			var needsInitialize = new BitArray(numberOfReaders);
			_contentReaders = new Dictionary<Type, ContentTypeReader>(numberOfReaders);

			// For each reader in the file, we read out the length of the string which contains the type of the reader,
			// then we read out the string. Finally we instantiate an instance of that reader using reflection
			for (var i = 0; i < numberOfReaders; i++)
			{
				// This string tells us what reader we need to decode the following data
				// string readerTypeString = reader.ReadString();
				string originalReaderTypeString = reader.ReadString();


				//A lot of code was deleted here.  All that matters is that the same amount of "stuff" is being read from the file


				// I think the next 4 bytes refer to the "Version" of the type reader,
				// although it always seems to be zero
				reader.ReadInt32();
			}
		}

		private static int Simulate_Read7BitEncodedInt(BinaryReader reader){
			int num = 0;
			int num2 = 0;
			byte b;

			do{
				if(num2 == 35)
					throw new FormatException("Format_Bad7BitInt32");

				b = reader.ReadByte();
				num |= (b & 0x7F) << num2;
				num2 += 7;
			}while((b & 0x80) != 0);

			return num;
		}
	}
}
