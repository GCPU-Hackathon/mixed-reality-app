using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    // Convertit un AudioClip Unity en wav (byte[]) PCM16 mono little-endian
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip.channels != 1)
        {
            Debug.LogWarning("[WavUtility] Le clip n'est pas mono, je vais forcer en mono en prenant le premier canal.");
        }

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        // Si plusieurs canaux, on ne garde que le canal 0
        short[] intData = new short[clip.samples];
        int outIdx = 0;
        for (int i = 0; i < samples.Length; i += clip.channels)
        {
            float f = Mathf.Clamp(samples[i], -1f, 1f);
            intData[outIdx++] = (short)Mathf.RoundToInt(f * 32767f);
        }

        byte[] wavData;
        using (var memStream = new MemoryStream())
        using (var writer = new BinaryWriter(memStream))
        {
            int numChannels = 1;
            int sampleRate = clip.frequency;
            int bitsPerSample = 16;
            int byteRate = sampleRate * numChannels * bitsPerSample / 8;
            int subchunk2Size = intData.Length * numChannels * bitsPerSample / 8;
            int chunkSize = 36 + subchunk2Size;

            // ---- RIFF header ----
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(chunkSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // ---- fmt subchunk ----
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size for PCM
            writer.Write((ushort)1); // AudioFormat = 1 (PCM)
            writer.Write((ushort)numChannels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((ushort)(numChannels * bitsPerSample / 8)); // BlockAlign
            writer.Write((ushort)bitsPerSample);

            // ---- data subchunk ----
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(subchunk2Size);

            // Samples
            foreach (short s in intData)
            {
                writer.Write(s);
            }

            writer.Flush();
            wavData = memStream.ToArray();
        }

        return wavData;
    }
}
