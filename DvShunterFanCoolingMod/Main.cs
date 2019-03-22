using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;
using System.IO;

namespace DvShunterFanCoolingMod
{
    public class Main
    {
        public const float FAN_COOL = 6f;
        public const float FUEL_CONSUMPTION = 5f;
        public const float POWER_LOSS = 0.2f;

        public static bool isFanOn = false;
        
        public static AudioClip fanAudioClip;
        
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);

            harmony.PatchAll(Assembly.GetExecutingAssembly());

            fanAudioClip = Utility.LoadDllResource("FanAudio.wav");

            return true;
        }
    }

    // init sound
    [HarmonyPatch(typeof(TrainAudioShunter), "Start")]
    class TrainAudioShunter_Start_Patch
    {
        static void Postfix(TrainAudioShunter __instance)
        {
            __instance.gameObject.AddComponent<TrainAudioShunterCustom>();
        }
    }

    // custom class for handling audio
    class TrainAudioShunterCustom : MonoBehaviour
    {
        LayeredAudio fanAudio;
        LocoControllerShunter controller;

        void Awake()
        {
            var audioAnchors = transform?.Find("Audio anchors");
            var engineAudio = audioAnchors.transform?.Find("Engine");

            var Engine_Layered = engineAudio?.Find("Engine_Layered(Clone)");

            var Engine_Layered_Audio = Engine_Layered.GetComponent<LayeredAudio>();

            var EngineFan_Layered = new GameObject();
            EngineFan_Layered.name = "EngineFan_Layered";
            EngineFan_Layered.transform.parent = engineAudio;
            EngineFan_Layered.transform.localPosition = Vector3.zero;
            EngineFan_Layered.transform.localRotation = Quaternion.identity;

            var train_engine_layer_fan = new GameObject();
            train_engine_layer_fan.name = "train_engine_layer_fan";
            train_engine_layer_fan.transform.parent = EngineFan_Layered.transform;
            train_engine_layer_fan.transform.localPosition = Vector3.zero;
            train_engine_layer_fan.transform.localRotation = Quaternion.identity;

            var audioSource = train_engine_layer_fan.AddComponent<AudioSource>();
            audioSource.outputAudioMixerGroup = Engine_Layered_Audio.audioMixerGroup;
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.maxDistance = 300f;
            audioSource.clip = Main.fanAudioClip;
            audioSource.spatialBlend = 1f;
            audioSource.dopplerLevel = 0f;
            audioSource.spread = 10f;

            var audioLayer = new LayeredAudio.Layer();
            audioLayer.name = "engine_fan";
            audioLayer.volumeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            audioLayer.usePitchCurve = false;
            audioLayer.inertia = 0f;
            audioLayer.inertialPitch = false;
            audioLayer.source = audioSource;
            audioLayer.refVelo = 0f;
            audioLayer.startPitch = 0.5f;

            var layeredAudio = EngineFan_Layered.AddComponent<LayeredAudio>();
            layeredAudio.audioMixerGroup = Engine_Layered_Audio.audioMixerGroup;
            layeredAudio.layers = new LayeredAudio.Layer[1];
            layeredAudio.layers[0] = audioLayer;
            layeredAudio.RandomizeTime();
            layeredAudio.Reset();
            layeredAudio.Play();
            layeredAudio.masterVolume = 0.6f;

            fanAudio = layeredAudio;
            controller = gameObject.GetComponent<LocoControllerShunter>();
        }

        void Update()
        {
            if (controller.EngineOn)
            {
                fanAudio.Set(controller.GetFan() ? 1f : 0.0f);
            }
            else
            {
                fanAudio.Set(0.0f);
            }
        }
    }

    // decrease temperature
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateEngineTemp")]
    class ShunterLocoSimulation_SimulateEngineTemp_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn || __instance.engineTemp.value <= 45f)
                return;

            if (Main.isFanOn)
            {
                __instance.engineTemp.AddNextValue(-Main.FAN_COOL * delta);
            }
        }
    }

    // increase fuel consumption
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateFuel")]
    class ShunterLocoSimulation_SimulateFuel_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn || __instance.fuel.value <= 0.0f)
                return;

            if (Main.isFanOn)
            {
                __instance.fuel.AddNextValue(Mathf.Lerp(0.025f, 1f, __instance.engineRPM.value) * -Main.FUEL_CONSUMPTION * delta);
            }
        }
    }

    // decrease power
    [HarmonyPatch(typeof(LocoControllerBase), "GetTotalAppliedForcePerBogie")]
    class LocoControllerBase_GetTotalAppliedForcePerBogie_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, ref float __result)
        {
            if (Main.isFanOn)
            {
                __result *= 1f - Main.POWER_LOSS;
            }
        }
    }

    // listen to fan switch
    [HarmonyPatch(typeof(ShunterDashboardControls), "OnEnable")]
    class ShunterDashboardControls_OnEnable_Patch
    {
        static ShunterDashboardControls instance;

        static void Postfix(ShunterDashboardControls __instance)
        {
            instance = __instance;

            __instance.StartCoroutine(AttachListeners());
        }

        static IEnumerator<object> AttachListeners()
        {
            yield return (object)null;

            DV.CabControls.ControlImplBase fanCtrl = instance.fanSwitchButton.GetComponent<DV.CabControls.ControlImplBase>();
            
            fanCtrl.ValueChanged += (e =>
            {
                Main.isFanOn = e.newValue >= 0.5f;
            });
        }
    }

    class Utility
    {
        public static AudioClip LoadDllResource(string resourceName)
        {
            var resource = ReadToEnd(Assembly.GetExecutingAssembly().GetManifestResourceStream(string.Format("DvShunterFanCoolingMod.Resources.{0}", resourceName)));
            
            WAV wav = new WAV(resource);

            AudioClip audioClip = AudioClip.Create("FanAudio", wav.SampleCount, 1, wav.Frequency, false);

            audioClip.SetData(wav.LeftChannel, 0);
            
            return audioClip;
        }

        static byte[] ReadToEnd(Stream stream)
        {
            long originalPosition = stream.Position;
            stream.Position = 0;

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;

                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }
    }

    public class WAV
    {

        // convert two bytes to one float in the range -1 to 1
        static float bytesToFloat(byte firstByte, byte secondByte)
        {
            // convert two bytes to one short (little endian)
            short s = (short)((secondByte << 8) | firstByte);
            // convert to range from -1 to (just below) 1
            return s / 32768.0F;
        }

        static int bytesToInt(byte[] bytes, int offset = 0)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                value |= ((int)bytes[offset + i]) << (i * 8);
            }
            return value;
        }

        private static byte[] GetBytes(string filename)
        {
            return File.ReadAllBytes(filename);
        }
        // properties
        public float[] LeftChannel { get; internal set; }
        public float[] RightChannel { get; internal set; }
        public int ChannelCount { get; internal set; }
        public int SampleCount { get; internal set; }
        public int Frequency { get; internal set; }

        // Returns left and right double arrays. 'right' will be null if sound is mono.
        public WAV(string filename) :
            this(GetBytes(filename))
        { }

        public WAV(byte[] wav)
        {

            // Determine if mono or stereo
            ChannelCount = wav[22];     // Forget byte 23 as 99.999% of WAVs are 1 or 2 channels

            // Get the frequency
            Frequency = bytesToInt(wav, 24);

            // Get past all the other sub chunks to get to the data subchunk:
            int pos = 12;   // First Subchunk ID from 12 to 16

            // Keep iterating until we find the data chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
            while (!(wav[pos] == 100 && wav[pos + 1] == 97 && wav[pos + 2] == 116 && wav[pos + 3] == 97))
            {
                pos += 4;
                int chunkSize = wav[pos] + wav[pos + 1] * 256 + wav[pos + 2] * 65536 + wav[pos + 3] * 16777216;
                pos += 4 + chunkSize;
            }
            pos += 8;

            // Pos is now positioned to start of actual sound data.
            SampleCount = (wav.Length - pos) / 2;     // 2 bytes per sample (16 bit sound mono)
            if (ChannelCount == 2) SampleCount /= 2;        // 4 bytes per sample (16 bit stereo)

            // Allocate memory (right will be null if only mono sound)
            LeftChannel = new float[SampleCount];
            if (ChannelCount == 2) RightChannel = new float[SampleCount];
            else RightChannel = null;

            // Write to double array/s:
            int i = 0;
            while (pos < wav.Length)
            {
                LeftChannel[i] = bytesToFloat(wav[pos], wav[pos + 1]);
                pos += 2;
                if (ChannelCount == 2)
                {
                    RightChannel[i] = bytesToFloat(wav[pos], wav[pos + 1]);
                    pos += 2;
                }
                i++;
            }
        }

        public override string ToString()
        {
            return string.Format("[WAV: LeftChannel={0}, RightChannel={1}, ChannelCount={2}, SampleCount={3}, Frequency={4}]", LeftChannel, RightChannel, ChannelCount, SampleCount, Frequency);
        }
    }
}
