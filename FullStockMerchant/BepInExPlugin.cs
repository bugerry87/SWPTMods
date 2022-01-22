using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FullStockMerchant
{
	[BepInPlugin("bugerry.FullStockMerchant", "Full Stock Merchant", "1.0.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<float> spawn_rate;

		public static ConfigEntry<int> nexusID;

		private void Awake()
		{
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 129, "Nexus mod ID for updates");
			spawn_rate = Config.Bind("General", "Spawn Rate", 50.0f, "The average number of items to be spawend to the shop");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}	   
			   
		[HarmonyPatch(typeof(Merchant), "GetMatchLevelItems")]
		public static class Merchant_GetMatchLevelItems_Patch
		{
			public static void Postfix(List<Transform> list, out List<Transform> __result)
			{
				if (!modEnabled.Value)
				{
					__result = list;
					return;
				}
				var prob = Mathf.Abs(spawn_rate.Value) / Mathf.Max(spawn_rate.Value, list.Count);
				__result = list.FindAll((Transform t) => Random.value < prob);
			}
		}
	}
}
