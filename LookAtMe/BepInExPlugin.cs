using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace LookAtMe
{
    [BepInPlugin("bugerry.LookAtMe", "Look At Me", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        public static ConfigEntry<float> yawLimit;
        public static ConfigEntry<float> rollLimit;
        public static ConfigEntry<float> yawCorrection;
        public static ConfigEntry<float> rollCorrection;

        public class EyeContoller : MonoBehaviour
		{
            private void LateUpdate()
			{
                if (!modEnabled.Value || !Camera.main) return;

                var dir = Vector3.Normalize(Camera.main.transform.position - transform.parent.parent.position);
                var forward = Vector3.Dot(dir, transform.parent.parent.forward);
                var right = Vector3.Dot(dir, transform.parent.parent.right);
                var up = Vector3.Dot(dir, transform.parent.parent.up);

                if (forward > 0f && Mathf.Abs(right) * 90f <= yawLimit.Value && Mathf.Abs(up) * 90f <= rollLimit.Value)
				{
                    dir = Vector3.Normalize(Camera.main.transform.position - transform.parent.position);
                    right = Vector3.Dot(dir, transform.parent.right);
                    up = Vector3.Dot(dir, transform.parent.up);
                    transform.parent.localEulerAngles = new Vector3(
                        up * 90f * rollCorrection.Value,
                        right * 90f * yawCorrection.Value,
                        0f
                    );
                    transform.LookAt(Camera.main.transform, transform.parent.parent.up);
                }
                else
				{
                    transform.parent.localEulerAngles = Vector3.zero;
                    transform.localEulerAngles = Vector3.zero;
                }
			}
		}

        private void Awake()
        {
            context = this;
            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
            nexusID = Config.Bind("General", "NexusID", 137, "Nexus mod ID for updates");

            yawLimit = Config.Bind("LookAtMe", "Yaw Limit", 60f, "Limit in degree for left/right");
            rollLimit = Config.Bind("LookAtMe", "Roll Limit", 30f, "Limit in degree for up/down");
            yawCorrection = Config.Bind("LookAtMe", "Yaw Correction", 0.7f, "Horizontal translation to keep eyes in socket");
            rollCorrection = Config.Bind("LookAtMe", "Roll Correction", -0.5f, "Vertical translation to keep eyes in socket");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(CharacterCustomization), "Start")]
        public static class CharacterCustomization_Start_Patch
		{
            public static MethodBase TargetMethod()
            {
                return typeof(CharacterCustomization).GetMethod("Start");
            }

            public static void Postfix(CharacterCustomization __instance)
            {
                if (!modEnabled.Value) return;
                var bones = new List<Transform>(__instance.body.bones);
                var lEye = bones.Find((Transform t) => t.name == "lEye");
                var rEye = bones.Find((Transform t) => t.name == "rEye");

                foreach(var animator in __instance.GetComponents<Animator>())
				{
                    context.Logger.LogInfo(animator.name);
				}

                lEye.gameObject.AddComponent<EyeContoller>();
                rEye.gameObject.AddComponent<EyeContoller>();
            }
        }
    }
}
