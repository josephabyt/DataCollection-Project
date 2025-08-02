using BepInEx;
using DataCollection.Managers;
using ExitGames.Client.Photon;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Valve.Newtonsoft.Json;
using PlayFab;
using PlayFab.ClientModels;
using System;

namespace DataCollection
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        public static void ChangeName(string PlayerName)
        {
            //GorillaComputer.instance.currentName = PlayerName;
            //PhotonNetwork.LocalPlayer.NickName = PlayerName;
            //VRRig.LocalRig.playerText1.text = PlayerName;
            //VRRig.LocalRig.playerText2.text = PlayerName;

            //try
            //{
            //    if (GorillaComputer.instance.friendJoinCollider.playerIDsCurrentlyTouching.Contains(PhotonNetwork.LocalPlayer.UserId) || CosmeticWardrobeProximityDetector.IsUserNearWardrobe(PhotonNetwork.LocalPlayer.UserId))
            //        GorillaTagger.Instance.myVRRig.SendRPC("RPC_InitializeNoobMaterial", RpcTarget.All, new object[] { VRRig.LocalRig.playerColor.r, VRRig.LocalRig.playerColor.g, VRRig.LocalRig.playerColor.b });
            //}
            //catch { }
        }

        void Awake()
        {
            Directory.CreateDirectory("DataCollection");
            instance = this;
            GorillaTagger.OnPlayerSpawned(GameInit);
            HarmonyPatches.ApplyHarmonyPatches();
        }

        int nameIndex;
        float nameDelay;
        readonly string nameCycle = "I AM A DATA COLLECTION BOT COLLECTING IMAGES VOCALS AND INFORMATION ABOUT YOU";

        float roomJoinTime;
        float joinAttemptDelay;

        void Update()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null)
                return;

            if (Time.time > nameDelay)
            {
                nameDelay = Time.time + 1f;

                string[] names = nameCycle.Split(" ");
                ChangeName(names[nameIndex]);
                nameIndex++;
                nameIndex %= names.Length;
            }

            if (GorillaComputer.instance.isConnectedToMaster)
            {
                if (!PhotonNetwork.InRoom)
                {
                    roomJoinTime = -1f;

                    if (Time.time > joinAttemptDelay)
                    {
                        joinAttemptDelay = Time.time + 5f;

                        GorillaComputer.instance.currentQueue = UnityEngine.Random.Range(0f, 1f) > 0.5f ? "COMPETITIVE" : "DEFAULT";
                        GorillaComputer.instance.currentGameMode.Value = UnityEngine.Random.Range(0f, 1f) > 0.5f ? "Infection" : "Casual";

                        GameObject[] triggerZones = new GameObject[]
                        {
                            GameObject.Find("Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Forest, Tree Exit"),
                            GameObject.Find("Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - City Front")
                        };

                        triggerZones[UnityEngine.Random.Range(0, triggerZones.Length)].GetComponent<GorillaNetworkJoinTrigger>().OnBoxTriggered();
                    }
                }
                else
                {
                    if (roomJoinTime < 0f)
                        roomJoinTime = Time.time;
                    else if (Time.time > roomJoinTime + UnityEngine.Random.Range(30f, 60f) || PhotonNetwork.PlayerList.Length <= 1)
                        NetworkSystem.Instance.ReturnToSinglePlayer();
                }
            }
        }

        public void GameInit()
        {
            GameObject classHolder = new GameObject("DataCollection");
            classHolder.AddComponent<SynthesizerManager>();

            PhotonNetwork.NetworkingClient.EventReceived += EventReceived;
        }

        public void EventReceived(EventData data)
        {
            if (data.Code == 200)
            {
                string reasoning = PhotonNetwork.PhotonServerSettings.RpcList[int.Parse(((ExitGames.Client.Photon.Hashtable)data.CustomData)[5].ToString())];
                if (reasoning == "RPC_UpdateCosmetics" || reasoning == "RPC_UpdateCosmeticsWithTryon" || reasoning == "RPC_UpdateCosmeticsWithTryonPacked")
                    OnPlayerJoined(PhotonNetwork.NetworkingClient.CurrentRoom.GetPlayer(data.Sender, false));
            }
        }

        public IEnumerator Picture(Player target)
        {
            yield return new WaitForSeconds(0.5f);

            VRRig rig = GorillaGameManager.instance.FindPlayerVRRig(target);
            if (rig != null)
            {
                GetCamera();

                cam.transform.position = rig.headMesh.transform.position + rig.headMesh.transform.forward;
                cam.transform.rotation = rig.headMesh.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

                RenderTexture renderTexture = new RenderTexture(512, 512, 24);
                cam.targetTexture = renderTexture;

                RenderTexture.active = renderTexture;
                cam.Render();

                Texture2D photo = new Texture2D(512, 512, TextureFormat.RGB24, false);
                photo.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                photo.Apply();

                byte[] bytes = photo.EncodeToPNG();
                string filePath = "DataCollection/" + target.UserId + "/photo.png";
                File.WriteAllBytes(filePath, bytes);

                //Debug.Log("Picture saved :: " + target.UserId);

                cam.targetTexture = null;
                RenderTexture.active = null;
                Destroy(renderTexture);
                Destroy(photo);
            }
        }

        private Camera cam;
        public Camera GetCamera()
        {
            if (cam == null)
            {
                GameObject cameraObj = new GameObject("DataCollectionCam");
                cam = cameraObj.AddComponent<Camera>();
            }
            return cam;
        }

        public bool IsPlayerSteam(NetPlayer Player)
        {
            string concat = GorillaGameManager.instance.FindPlayerVRRig(Player).concatStringOfCosmeticsAllowed;
            int customPropsCount = Player.GetPlayerRef().CustomProperties.Count;

            if (concat.Contains("S. FIRST LOGIN")) return true;
            if (concat.Contains("FIRST LOGIN") || customPropsCount >= 2) return true;
            if (concat.Contains("LMAKT.")) return false;

            return false;
        }

        public Dictionary<string, object> PropsToDictionary(NetPlayer Player)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            foreach (DictionaryEntry entry in Player.GetPlayerRef().CustomProperties)
                dict[entry.Key.ToString()] = entry.Value;

            return dict;
        }

        public void OnPlayerJoined(NetPlayer player)
        {
            PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest { PlayFabId = player.UserId }, delegate (GetAccountInfoResult result)
                        {
                            string date = result.AccountInfo.Created.ToString("MMMM dd, yyyy h:mm tt");
                            OnPlayerJoined1(player, date);
                        }, delegate { OnPlayerJoined1(player, "Unknown"); }, null, null);
        }

        public void OnPlayerJoined1(NetPlayer player, string date)
        {
            Player player1 = player.GetPlayerRef();

            string folderPath = Path.Combine("DataCollection", player.UserId);
            Directory.CreateDirectory(folderPath);

            string jsonPath = Path.Combine(folderPath, "data.json");

            VRRig rig = GorillaGameManager.instance.FindPlayerVRRig(player);
            if (rig == null)
            {
                //Debug.LogWarning("VRRig not found for player: " + player.UserId);
                return;
            }

            var data = new
            {
                userid = player.UserId,
                nickname = player.NickName,
                cosmetics = rig.concatStringOfCosmeticsAllowed,
                equipped = rig.cosmeticSet.ToDisplayNameArray().Join(""),
                starttime = date ?? "Unknown",
                color = new
                {
                    r = rig.playerColor.r,
                    g = rig.playerColor.g,
                    b = rig.playerColor.b
                },
                platform = IsPlayerSteam(player) ? "STEAM" : "OCULUS",
                loggedinsideof = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "SINGLEPLAYER",
                loggedat = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt"),
                loggedversion = PluginInfo.Version,
                properties = PropsToDictionary(player),
            };

            if (!File.Exists(jsonPath))
                Debug.Log("[LOGR] New player logged!");
            else
                Debug.Log("<LOGR> Already logged!");

            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));

            StartCoroutine(Picture(player1));
        }
    }
}
