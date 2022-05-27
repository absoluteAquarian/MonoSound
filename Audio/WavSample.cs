using System;

namespace MonoSound.Audio{
	public struct WavSample{
		public const int MaxValue_24BitPCM = 0x800000;

		private byte[] data;
		public byte[] Data{
			get => data;
			set{
				EnsureDataIsValid(value);

				data = value;
			}
		}

		public readonly short SampleSize;

		public WavSample(short size, byte[] initialData){
			SampleSize = size;
			data = null;

			Data = initialData;
		}

		private void EnsureDataIsValid(byte[] arr){
			if(arr.Length % SampleSize != 0)
				throw new ArgumentException($"Sample data was the incorrect size.  Expected: {SampleSize} bytes", "arr");
		}

		public float ToFloatSample(){
			//Data can either be 16-bit or 24-bit PCM
			float result;

			if(SampleSize == 2){
				//16-bit
				short convert = BitConverter.ToInt16(data, 0);
				result = convert / (float)short.MaxValue;
			}else if(SampleSize == 3){
				byte[] oldData = (byte[])data.Clone();
				
				if(BitConverter.IsLittleEndian)
					data = new byte[4]{ 0x00, data[0], data[1], data[2] };
				else
					data = new byte[4]{ data[0], data[1], data[2], 0x00 };

				int convert = BitConverter.ToInt32(data, 0);
				result = convert / (float)MaxValue_24BitPCM;

				data = oldData;
			}else
				throw new InvalidOperationException("Sample size was invalid.  Expected either 16-bit or 24-bit PCM.");

			return result;
		}
	}
}
