using GorillaTag.Audio;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace DataCollection.Classes
{
    public class SpeakerHook : MonoBehaviour
    {
        public GTSpeaker SpeakerInstance;
        public VRRig OwnerRig;

        public void Start()
        {
            if (Managers.SynthesizerManager.instance == null)
            {
                Debug.LogError("Synthesizer manager is null");
                Destroy(this);
                return;
            }

            SpeakerInstance = gameObject.GetComponent<GTSpeaker>();
            if (gameObject.GetComponent<GTSpeaker>() == null)
            {
                Debug.LogError("GameObject has no speaker");
                Destroy(this);
                return;
            }

            if (SpeakerInstance.remoteVoiceLink == null)
            {
                Debug.LogError("remoteVoiceLink is null");
                Destroy(this);
                return;
            }

            OwnerRig = transform.parent.parent.parent.parent.parent.parent.GetComponent<VRRig>();
        }

        public void Update()
        {
            if (Time.time > voiceDataTimestamp && audioBuffer.Count > 0)
            {
                float[] audioData = audioBuffer.ToArray();
                audioBuffer.Clear();

                _ = ProcessAudioAsync(audioData);
            }
        }

        private List<float> audioBuffer = new List<float>();
        private float voiceDataTimestamp;

        public void ProcessAudioFrame(float[] frame)
        {
            voiceDataTimestamp = Time.time + 0.5f;
            audioBuffer.AddRange(frame);
        }

        private async Task ProcessAudioAsync(float[] audioData)
        {
            byte[] byteData = new byte[audioData.Length * sizeof(short)];
            for (int i = 0; i < audioData.Length; i++)
            {
                short sample = (short)(Math.Clamp(audioData[i], -1f, 1f) * short.MaxValue);
                byteData[i * 2] = (byte)(sample & 0xff);
                byteData[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
            }

            string audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
            WriteWavFile(audioPath, byteData, 16000);

            string output = await Managers.SynthesizerManager.instance.SynthesizeAudio(audioPath);

            if (string.IsNullOrWhiteSpace(output))
                output = Guid.NewGuid().ToString();

            output = Regex.Replace(output, @"[^a-zA-Z0-9_\- ]+", ""); 

            Directory.CreateDirectory(Path.Combine("DataCollection", OwnerRig.Creator.UserId, "audio"));

            File.Move(audioPath, Path.Combine("DataCollection", OwnerRig.Creator.UserId, "audio", $"{output}.wav"));
        }

        private void WriteWavFile(string path, byte[] pcmData, int sampleRate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + pcmData.Length);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);

                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(pcmData.Length);
                bw.Write(pcmData);
            }
        }
    }
}
