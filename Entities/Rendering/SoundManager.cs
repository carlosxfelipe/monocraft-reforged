using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace MonoCraft.Entities.Rendering;

public static class SoundManager
{
    private static Dictionary<string, SoundEffect> _sounds = new Dictionary<string, SoundEffect>();

    public static void Initialize(string basePath)
    {
        LoadOgg(basePath, "default_grass_footstep.1");
        LoadOgg(basePath, "default_water_footstep.1");
        LoadOgg(basePath, "default_dirt_footstep.1");
        LoadOgg(basePath, "default_stone_footstep.1");
        LoadOgg(basePath, "default_sand_footstep.1");
        LoadOgg(basePath, "default_wood_footstep.1");
        LoadOgg(basePath, "default_snow_footstep.1");
    }

    private static void LoadOgg(string basePath, string name)
    {
        string filePath = Path.Combine(basePath, name + ".ogg");
        if (!File.Exists(filePath))
            return;

        try
        {
            using (var vorbis = new VorbisReader(filePath))
            {
                var channels = vorbis.Channels;
                var sampleRate = vorbis.SampleRate;

                // O MonoGame SoundEffect só suporta 1 ou 2 canais (Mono ou Stereo)
                AudioChannels audioChannels =
                    channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;

                float[] buffer = new float[vorbis.TotalSamples * channels];
                int totalRead = 0;
                int read;
                while (
                    (read = vorbis.ReadSamples(buffer, totalRead, buffer.Length - totalRead)) > 0
                )
                {
                    totalRead += read;
                }

                // Converter float array de NVorbis para short array (16-bit PCM)
                byte[] byteBuffer = new byte[totalRead * 2];
                for (int i = 0; i < totalRead; i++)
                {
                    float sample = buffer[i];
                    // Clamp to -1.0 .. 1.0
                    sample = Math.Max(-1f, Math.Min(1f, sample));
                    short val = (short)(sample * short.MaxValue);

                    // Little-endian
                    byteBuffer[i * 2] = (byte)(val & 0xFF);
                    byteBuffer[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
                }

                SoundEffect sfx = new SoundEffect(byteBuffer, sampleRate, audioChannels);
                _sounds[name] = sfx;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar o som {name}: {ex.Message}");
        }
    }

    public static void Play(string name, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (_sounds.TryGetValue(name, out var sfx))
        {
            try
            {
                // Pitch e Pan precisam estar no limite permitido (-1 a 1)
                pitch = Math.Max(-1f, Math.Min(1f, pitch));
                pan = Math.Max(-1f, Math.Min(1f, pan));
                sfx.Play(volume, pitch, pan);
            }
            catch (Exception)
            {
                // Ignorar erros caso o limite de sons simultâneos seja atingido
            }
        }
    }
}
