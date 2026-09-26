using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TownOfHost;
public static class CustomSound
{
    private static AudioClip kamae, shot;

    public static AudioClip SniperKamae => Get(ref kamae, "TownOfHost.Resources.Sounds.Sniper_Kamae.wav");
    public static AudioClip SniperShot => Get(ref shot, "TownOfHost.Resources.Sounds.Sniper_Shot.wav");
    public static AudioClip Bomb => Get(ref shot, "TownOfHost.Resources.Sounds.SelfBomber_Bomb.wav");
    public static AudioClip Firework => Get(ref shot, "TownOfHost.Resources.Sounds.FireWorks_Fire.wav");

    private static AudioClip Get(ref AudioClip cache, string resourceName)
    {
        // Unityに破棄されたものは == null が true になる（??= では検出できない）
        if (cache == null) cache = Load(resourceName);
        return cache;
    }
    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        SoundManager.Instance.PlaySound(clip, false, volume);
    }
    public static AudioClip Load(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var br = new BinaryReader(stream);
        br.ReadBytes(12); // RIFF ヘッダ

        int channels = 1, sampleRate = 44100, bits = 16;
        while (stream.Position < stream.Length)
        {
            string id = new string(br.ReadChars(4));
            int size = br.ReadInt32();
            if (id == "fmt ")
            {
                br.ReadInt16(); // format
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                br.ReadInt32(); br.ReadInt16(); // byteRate, blockAlign
                bits = br.ReadInt16();
                br.ReadBytes(size - 16);
            }
            else if (id == "data")
            {
                if (bits != 16) return null; // 16bit PCM のみ対応
                int count = size / 2;
                var samples = new float[count];
                for (int i = 0; i < count; i++)
                    samples[i] = br.ReadInt16() / 32768f;

                var clip = AudioClip.Create(resourceName, count / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                clip.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                return clip;
            }
            else br.ReadBytes(size);
        }
        return null;
    }
}