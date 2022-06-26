using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FullStockMerchant
{
	[BepInPlugin("bugerry.FullStockMerchant", "Full Stock Merchant", "1.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<float> ArmourerSpawnRate;
		public static ConfigEntry<float> BlacksmithSpawnRate;
		public static ConfigEntry<float> SmugglerSpawnRate;
		public static ConfigEntry<float> MaidSpawnRate;
		public static ConfigEntry<int> refreshTime;
		public static ConfigEntry<int> nexusID;

		private Dictionary<string, ConfigEntry<float>> spawnRates = new Dictionary<string, ConfigEntry<float>>();

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 129, "Nexus mod ID for updates");
			ArmourerSpawnRate = Config.Bind("Full Stock Merchant", "Armourer Spawn Rate", 30f, "The average number of items to be spawend to the shop");
			BlacksmithSpawnRate = Config.Bind("Full Stock Merchant", "Blacksmith Spawn Rate", 20f, "The average number of items to be spawend to the shop");
			SmugglerSpawnRate = Config.Bind("Full Stock Merchant", "Smuggler Spawn Rate", 70f, "The average number of items to be spawend to the shop");
			MaidSpawnRate = Config.Bind("Full Stock Merchant", "Maid Spawn Rate", 70f, "The average number of items to be spawend to the shop");
			refreshTime = Config.Bind("Full Stock Merchant", "Refresh Time", 1000, "Refresh Time of the stocks in seconds");
			spawnRates["Armorer"] = ArmourerSpawnRate;
			spawnRates["Blacksmith"] = BlacksmithSpawnRate;
			spawnRates["Smuggler"] = SmugglerSpawnRate;
			spawnRates["Maid"] = MaidSpawnRate;
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}	   
			   
		[HarmonyPatch(typeof(Merchant), nameof(Merchant.GetMatchLevelItems))]
		public static class Merchant_GetMatchLevelItems_Patch
		{
			public static void Postfix(Merchant __instance, List<Transform> list, out List<Transform> __result)
			{
				if (!modEnabled.Value)
				{
					__result = list;
					return;
				}

				context.Logger.LogInfo(__instance.name);
				if (context.spawnRates.TryGetValue(__instance.name, out ConfigEntry<float> spawnRate))
				{
					var prob = Mathf.Abs(spawnRate.Value) / Mathf.Max(spawnRate.Value, list.Count);
					__result = list.FindAll((Transform t) => Random.value < prob);
				}
				else
				{
					__result = list;
				}
			}
		}

		[HarmonyPatch(typeof(Merchant), nameof(Merchant.Refresh))]
		public static class Merchant_Refresh_Patch
		{
			public static void Postfix(Merchant __instance)
			{
				if (!modEnabled.Value) return;
				__instance.refreshTimer = Mathf.Min(__instance.refreshTimer, refreshTime.Value);
			}
		}
	}
}
