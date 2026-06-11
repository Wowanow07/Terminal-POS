using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Terminal_POS
{
    public class Sound
    {
        public void PlaySound(string soundFile)
        {
            int sampleRate = 44100;
            double duration = 0.12;
            int samples = (int)(sampleRate * duration);
            short[] buffer = new short[samples];

            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / sampleRate;
                double sample = 0;

                // click
                if (t < 0.008)
                {
                    Random rand = new Random(i);
                    sample += (rand.NextDouble() * 2 - 1) * 0.4;
                }

                // tone
                double freq = t < 0.05 ? 1300 : 900;
                sample += Math.Sin(2 * Math.PI * freq * t) * 0.6;

                // fade out
                double fade = 1.0 - (t / duration);
                sample *= fade;

                buffer[i] = (short)(sample * short.MaxValue);
            }

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + buffer.Length * 2);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(buffer.Length * 2);

                foreach (short s in buffer)
                    writer.Write(s);

                ms.Position = 0;
                new SoundPlayer(ms).Play();
            }
        }
    }
}
