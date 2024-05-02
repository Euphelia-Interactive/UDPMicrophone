namespace UDPMicrophoneReceiver
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using Concentus;
	using NAudio.Wave;
	public class Program
	{
		private static void Main(string[] args)
		{
			var        udpClient = new UdpClient(11000); // Listening on port 11000
			IPEndPoint remoteEp  = null;

			// Receive configuration first
			var configBytes = udpClient.Receive(ref remoteEp);
			var config      = Encoding.ASCII.GetString(configBytes).Split(',');
			var sampleRate  = int.Parse(config[0]);
			var channels    = int.Parse(config[1]);

			var waveOut    = new BufferedWaveProvider(new WaveFormat(sampleRate, channels));
			var wavePlayer = new WaveOutEvent();
			wavePlayer.Init(waveOut);
			wavePlayer.Play();

			var decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);

			while (true)
			{
				var data = udpClient.Receive(ref remoteEp);

				// The minimum safe buffer size is 5760 samples
				var decoded = new float[5760];
				var length  = decoder.Decode(data, decoded, decoded.Length / channels);

				// Convert float PCM to 16-bit PCM
				var pcmBytes = FloatTo16BitPCM(decoded, length * channels);

				// Add samples to NAudio's wave provider
				waveOut.AddSamples(pcmBytes, 0, pcmBytes.Length);
			}
		}

		private static byte[] FloatTo16BitPCM(IReadOnlyList<float> input, int length)
		{
			var bytes = new byte[length * 2];
			for (var i = 0; i < length; i++)
			{
				var val        = (short)(input[i] * 32767); // Convert float to 16-bit short
				var shortBytes = BitConverter.GetBytes(val);
				Buffer.BlockCopy(shortBytes, 0, bytes, i * 2, 2);
			}
			return bytes;
		}
	}
}