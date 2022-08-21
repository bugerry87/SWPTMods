using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DyeKit
{
	[BepInPlugin("bugerry.AdvancedBuildingMode", "Advanced Building Mode", "0.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;

		public static ConfigEntry<float> increment;


		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 135, "Nexus mod ID for updates");
			increment = Config.Bind("Advanced Building Mode", "Rotation Increment", 45f, "Defines by what angle in degree the furniture rotates per step");

			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		[HarmonyPatch(typeof(UIBuildingMode), "Update")]
		public static class UIBuildingMode_Update_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(UIBuildingMode).GetMethod("Update");
			}

			public static bool Prefix(UIBuildingMode __instance)
			{
				if (!__instance.placingFurniture || Input.GetAxis("Mouse ScrollWheel") == 0f) return true;

				if (Input.GetKey(KeyCode.LeftAlt))
				{
					__instance.placingFurniture.localEulerAngles += new Vector3(0f, 0f, Input.GetAxis("Mouse ScrollWheel") * increment.Value);
				}
				else if (Input.GetKey(KeyCode.LeftControl))
				{
					__instance.placingFurniture.localEulerAngles += new Vector3(Input.GetAxis("Mouse ScrollWheel") * increment.Value, 0f, 0f);
				}
				else
				{
					__instance.placingFurniture.localEulerAngles += new Vector3(0f, Input.GetAxis("Mouse ScrollWheel") * increment.Value, 0f);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(Global), nameof(Global.ToggleBuilding))]
		public static class Global_ToggleBuilding_Patch
		{
			public static bool Prefix(Global __instance)
			{
				if (!modEnabled.Value) return true;

				if (__instance.selectedItem || Mainframe.code.uILoading.gameObject.activeSelf || Mainframe.code.uISettings.gameObject.activeSelf)
				{
					return false;
				}

				if (!__instance.uiBuildingMode.gameObject.activeSelf)
				{
					__instance.CloseAllUI();
					__instance.uiBuildingMode.Open();
				}
				else if (__instance.uiBuildingMode.buildingListPanel.gameObject.activeSelf)
				{
					__instance.uiBuildingMode.buildingListPanel.gameObject.SetActive(false);
				}
				else
				{
					__instance.uiBuildingMode.OpenBuildingList();
				}
				
				return false;
			}
		}

		[HarmonyPatch(typeof(BuildingIcon), nameof(BuildingIcon.ButtonnPurchase))]
		public static class BuildingIcon_ButtonnPurchase_Patch
		{
			public static void Postfix(BuildingIcon __instance)
			{
				if (!modEnabled.Value) return;
				if (Global.code.curlocation?.locationType != LocationType.home)
				{
					Global.code.uiBuildingMode.placingFurniture?.SetParent(null);
				}
			}
		}
	}
}
