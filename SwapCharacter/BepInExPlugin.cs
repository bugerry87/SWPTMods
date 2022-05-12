using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SwapCharacter
{
	[BepInPlugin("bugerry.SwapCharacter", "Swap Character", "0.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;
		public static ConfigEntry<string> selected;
		public static string match;

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 138, "Nexus mod ID for updates");
			selected = Config.Bind("Swap Character", "Select", "", "Name of the character to swap");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		[HarmonyPatch(typeof(Mainframe), nameof(Mainframe.LoadGame))]
		public static class Mainframe_LoadGame_Patch
		{
			public static void Prefix(string _foldername)
			{
				if (!modEnabled.Value || selected.Value == null || selected.Value.Length == 0) return;
				match = null;

				try
				{
					var names = ES2.LoadList<string>($"{_foldername}/Global.txt?tag=companionlist");

					foreach (var name in names)
					{
						if (name != null && name != "")
						{
							var characterName = ES2.Load<string>($"{_foldername}/{name}.txt?tag=characterName");
							if (characterName == selected.Value)
							{
								match = name;
								break;
							}
						}
					}

					if (match != null)
					{
						ES2.Rename($"{_foldername}/Player.txt", $"{_foldername}/Player.tmp");
						context.Logger.LogInfo($"{_foldername}/Player.txt {_foldername}/Player.tmp");
						ES2.Rename($"{_foldername}/{match}.txt", $"{_foldername}/Player.txt");
						context.Logger.LogInfo($"{_foldername}/{match}.txt {_foldername}/Player.txt");
						ES2.Rename($"{_foldername}/Player.tmp", $"{_foldername}/{match}.txt");
						context.Logger.LogInfo($"{_foldername}/Player.tmp {_foldername}/{match}.txt");

						foreach (var folder in ES2.GetFolders($"{_foldername}/"))
						{
							ES2.Rename($"{_foldername}/{folder}/Player.txt", $"{_foldername}/{folder}/Player.tmp");
							context.Logger.LogInfo($"{_foldername}/{folder}/Player.txt {_foldername}/{folder}/Player.tmp");
							ES2.Rename($"{_foldername}/{folder}/{match}.txt", $"{_foldername}/{folder}/Player.txt");
							context.Logger.LogInfo($"{_foldername}/{folder}/{match}.txt {_foldername}/{folder}/Player.txt");
							ES2.Rename($"{_foldername}/{folder}/Player.tmp", $"{_foldername}/{folder}/{match}.txt");
							context.Logger.LogInfo($"{_foldername}/{folder}/Player.tmp {_foldername}/{folder}/{match}.txt");
						}
					}
					else
					{
						context.Logger.LogInfo("No match!");
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}
		}

		/*
		[HarmonyPatch(typeof(Mainframe), "LoadPlayer")]
		public static class Mainframe_LoadPlayer_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("LoadPlayer");
			}

			public static void Prefix(Mainframe __instance)
			{
				if (!modEnabled.Value || selected.Value == null || selected.Value.Length == 0) return;
				match = null;

				try
				{
					var names = ES2.LoadList<string>(__instance.GetFolderName() + "Global.txt?tag=companionlist");
					foreach (var name in names)
					{
						if (name != null && name != "")
						{
							var characterName = ES2.Load<string>(__instance.GetFolderName() + name + ".txt?tag=characterName");
							if (characterName == selected.Value)
							{
								match = name;
								Player.code.customization.name = name;
								break;
							}
						}
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}

			public static void Postfix()
			{
				Player.code.customization.name = "Player";
			}
		}

		[HarmonyPatch(typeof(Mainframe), "LoadCompanion")]
		public static class Mainframe_LoadCompanion_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("LoadCompanion");
			}

			public static IEnumerator Dummy()
			{
				yield break;
			}

			public static bool Prefix(Transform companion, out IEnumerator __result)
			{
				if (companion)
				{
					__result = null;
				}
				else
				{
					__result = Dummy();
				}

				if (companion?.name == match)
				{
					companion.name = "Player";
				}

				return !!companion;
			}

			/*
			public static void Postfix(Transform companion)
			{
				if (!modEnabled.Value) return;

				if (companion?.name == "Player")
				{
					companion.name = match;
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "LoadCompanions")]
		public static class Mainframe_LoadCompanions_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("LoadCompanion");
			}

			public static void Postfix()
			{
				if (!modEnabled.Value) return;

				var comp = Global.code.companions.GetItemWithName("Player");
				if (comp) comp.name = match;
			}
		}

		[HarmonyPatch(typeof(Mainframe), "LoadCharacterCustomization")]
		public static class Mainframe_LoadCharacterCustomization_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("LoadCharacterCustomization");
			}

			public static void Prefix(CharacterCustomization gen)
			{
				if (gen != Player.code?.customization && gen?.name == match)
				{
					gen.name = "Player";
				}
			}
		}
		*/
	}
}
