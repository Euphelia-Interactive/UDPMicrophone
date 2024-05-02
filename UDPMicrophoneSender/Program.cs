namespace UDPMicrophoneSender
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using Concentus;
	using Concentus.Enums;
	using NAudio.Wave;
	public class Program
	{
		public static void Main(string[] args)
		{
			const int deviceNumber  = 0;     // Default device index.
			const int frameDuration = 20;    // 2.5ms - extremely low, 5ms - very low , 10ms - low, 20ms - normal, 40ms - good, and 60ms - high.
			const int bitrate       = 24000; // 16000 - low, 24000 - normal, 48000 - high.
			const int bits          = 16;    // No need to tweak, pretty sure best or only amount possible with opus.
			var       capabilities  = WaveIn.GetCapabilities(deviceNumber);
			var       maxSampleRate = GetMaxSupportedSampleRate(capabilities);
			var       channels      = capabilities.Channels;                            // Use maximum channels supported. 1 - mono, 2 -stereo both work.
			var       frameSize     = CalculateFrameSize(maxSampleRate, frameDuration); // Calculate optimal frame size based on sample rate

			var waveIn = new WaveInEvent
			{
					DeviceNumber       = deviceNumber,
					WaveFormat         = new WaveFormat(maxSampleRate, bits, channels),
					BufferMilliseconds = frameDuration
			};

			var encoder = OpusCodecFactory.CreateEncoder(maxSampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
			encoder.Bitrate = bitrate;

			var udpClient = new UdpClient();
			var endPoint  = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000);

			var config      = $"{maxSampleRate},{channels}";
			var configBytes = Encoding.ASCII.GetBytes(config);
			udpClient.Send(configBytes, configBytes.Length, endPoint);

			waveIn.DataAvailable += (sender, e) =>
			{
				var pcmFloats = ConvertToFloatArray(e.Buffer);
				var encoded   = new byte[1275]; // Maximum possible Opus packet size
				var length    = encoder.Encode(new ReadOnlySpan<float>(pcmFloats, 0, frameSize), frameSize, new Span<byte>(encoded), encoded.Length);
				udpClient.Send(encoded, length, endPoint);
			};

			waveIn.StartRecording();
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();
			waveIn.StopRecording();
			udpClient.Close();
		}

		private static int GetMaxSupportedSampleRate(WaveInCapabilities capabilities)
		{
			// Define Opus-compatible sample rates
			int[] opusRates =
			{
					8000, 12000, 16000, 24000, 48000
			};
			return opusRates.Where(rate => IsSupportedSampleRate(capabilities, rate)).Max();
		}

		private static bool IsSupportedSampleRate(WaveInCapabilities capabilities, int rate)
		{
			// Adjust the SupportedWaveFormat checks to match the actual rates used in Opus
			switch (rate)
			{
				case 8000:
					return capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48M08) ||
					       capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48S08);
				case 12000:
					// Opus supports 12K, but NAudio might not explicitly, so check closest available formats
					return capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1M08) ||
					       capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1S08);
				case 16000:
					return capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1M16) ||
					       capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1S16);
				case 24000:
					// Opus supports 24K, but NAudio might not explicitly, so check closest available formats
					return capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_2M16) ||
					       capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_2S16);
				case 48000:
					return capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48M16) ||
					       capabilities.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48S16);
				default:
					return false;
			}
		}

		private static int CalculateFrameSize(int sampleRate, int desiredFrameDurationMs) => sampleRate / 1000 * desiredFrameDurationMs;

		private static float[] ConvertToFloatArray(IReadOnlyList<byte> buffer)
		{
			// Convert byte array to float array
			var floatArray = new float[buffer.Count / 2];
			for (var i = 0; i < floatArray.Length; i++)
			{
				var idx    = i * 2;
				var sample = (short)(buffer[idx + 1] << 8 | buffer[idx] & 0xFF);
				floatArray[i] = sample / 32768f; // Normalize to -1.0 to 1.0 range
			}
			return floatArray;
		}
	}
}