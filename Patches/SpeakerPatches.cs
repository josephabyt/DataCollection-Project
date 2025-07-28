using DataCollection.Classes;
using GorillaTag.Audio;
using HarmonyLib;
using Photon.Voice;
using Photon.Voice.Unity;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DataCollection.Patches
{
    [HarmonyPatch(typeof(Speaker))]
    [HarmonyPatch("StartPlaying", MethodType.Normal)]
    public class OnSpeakerStart
    {
        static void Postfix(Speaker __instance)
        {
            if (__instance.gameObject.GetComponent<SpeakerHook>() != null)
                return;

            __instance.gameObject.AddComponent<SpeakerHook>();
        }
    }

    [HarmonyPatch(typeof(Speaker))]
    [HarmonyPatch("StopPlaying", MethodType.Normal)]
    public class OnSpeakerEnd
    {
        static void Postfix(Speaker __instance, bool force)
        {
            if (__instance.gameObject.GetComponent<SpeakerHook>() == null)
                return;

            SpeakerHook SpeakerHookInstance = __instance.gameObject.GetComponent<SpeakerHook>();
            SpeakerHookInstance.enabled = false;
            UnityEngine.Object.Destroy(SpeakerHookInstance);
        }
    }

    [HarmonyPatch(typeof(GTSpeaker))]
    [HarmonyPatch("OnAudioFrame", MethodType.Normal)]
    public class OnAudioFramePatch
    {
        static void Postfix(GTSpeaker __instance, FrameOut<float> frame)
        {
            if (__instance.gameObject.GetComponent<SpeakerHook>() == null)
                return;

            __instance.gameObject.GetComponent<SpeakerHook>().ProcessAudioFrame(frame.Buf);
        }
    }
}
