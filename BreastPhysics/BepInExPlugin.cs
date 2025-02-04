﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Assets.DuckType.Jiggle;

namespace BreastPhysics
{
	[BepInPlugin("bugerry.BreastPhysics", "Breast Physics", "1.2.1")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;
		public static ConfigEntry<int> updateMode;

		public readonly string[] names = new string[] {
			"Jelly", "Spring", "Hold", "Mass", "Angle", "Limit"
		};

		public readonly Dictionary<string, float> values = new Dictionary<string, float>();
		public readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
		public readonly HashSet<Jiggle> jiggles = new HashSet<Jiggle>();
		public Transform viewport = null;

		public static void ApplyValue(Jiggle jiggle, string key, float val)
		{
			switch (key)
			{
				case "Jelly": jiggle.Dampening = 1f - val; break;
				case "Spring": jiggle.SpringStrength = val; break;
				case "Hold": jiggle.SoftLimitInfluence = 1f - val; break;
				case "Mass": jiggle.SoftLimitStrength = 1f - val; break;
				case "Angle": jiggle.HingeAngle = 150f * val; break;
				case "Limit": jiggle.AngleLimit = 90f * val; break;
				default: break;
			}
		}

		public static float GetValue(Jiggle jiggle, string key)
		{
			switch (key)
			{
				case "Jelly": return 1f - jiggle.Dampening;
				case "Spring": return jiggle.SpringStrength;
				case "Hold": return 1f - jiggle.SoftLimitInfluence;
				case "Mass": return 1f - jiggle.SoftLimitStrength;
				case "Angle": return jiggle.HingeAngle / 150f;
				case "Limit": return jiggle.AngleLimit / 90f;
				default: return 0f;
			}
		}

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 106, "Nexus mod ID for updates");
			updateMode = Config.Bind("General", "Update Mode", 0, "0 = Auto, 1 = Per Frame, 2 = Post Frame, 3 = On Physics Update");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		private void Update()
		{
			if (!modEnabled.Value || updateMode.Value != 1) return;
			foreach (var jiggle in jiggles)
			{
				jiggle.ScheduledUpdate(Time.deltaTime);
			}
		}

		public void ApplyValue(string key, float val)
		{
			var cc = Global.code.uiCustomization.curCharacterCustomization;
			if (!cc) return;

			foreach (var jiggle in cc.body.rootBone.GetComponentsInChildren<Jiggle>())
			{
				if (jiggle.name.EndsWith("Pectoral"))
				{
					ApplyValue(jiggle, key, val);
				}
			}
			values[$"{cc.name}/{key}"] = val;
		}

		public void AddSlider(UICustomization __instance, Transform viewport, string key, float init)
		{
			var slider = __instance.panelSkin.GetComponentInChildren<Slider>();
			var name = key.Replace('_', ' ');
			slider = Instantiate(slider, viewport);
			slider.onValueChanged = new Slider.SliderEvent();
			slider.name = name;
			slider.minValue = 0f;
			slider.maxValue = 1f;
			slider.value = init;
			slider.onValueChanged.AddListener((float val) => ApplyValue(key, val));
			slider.transform.GetComponentInChildren<Text>().text = name;
			Destroy(slider.GetComponentInChildren<LocalizationText>());
			sliders[key] = slider;
		}

		[HarmonyPatch(typeof(UICustomization), "Start")]
		public static class UICustomization_Start_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(UICustomization).GetMethod("Start");
			}

			public static void Postfix(UICustomization __instance)
			{
				var cc = __instance.curCharacterCustomization;
				if (!modEnabled.Value || cc == null) return;

				context.viewport = __instance.panelBreasts.transform.GetChild(0).GetChild(0).GetChild(0);
				var title = Instantiate(context.viewport.GetChild(0), context.viewport);
				title.GetComponent<Text>().text = "Breast Physics";
				title.name = "Breast Physics";
				Destroy(title.GetComponent<LocalizationText>());

				foreach (var jiggle in cc.body.rootBone.GetComponentsInChildren<Jiggle>())
				{
					if (jiggle.name.EndsWith("Pectoral"))
					{
						context.AddSlider(__instance, context.viewport, "Jelly", 1f - jiggle.Dampening);
						context.AddSlider(__instance, context.viewport, "Spring", jiggle.SpringStrength);
						context.AddSlider(__instance, context.viewport, "Hold", 1f - jiggle.SoftLimitInfluence);
						context.AddSlider(__instance, context.viewport, "Mass", 1f - jiggle.SoftLimitStrength);
						context.AddSlider(__instance, context.viewport, "Angle", jiggle.HingeAngle / 150f);
						context.AddSlider(__instance, context.viewport, "Limit", jiggle.AngleLimit / 90f);
						break;
					}
				}
			}
		}

		[HarmonyPatch(typeof(UICustomization), "Open")]
		public static class UICustomization_Open_Patch
		{
			public static void Postfix(CharacterCustomization customization, bool isOpenChangeName = true)
			{
				if (!modEnabled.Value || customization == null) return;

				foreach (var jiggle in customization.body.rootBone.GetComponentsInChildren<Jiggle>())
				{
					if (jiggle.name.EndsWith("Pectoral"))
					{
						foreach (var slider in context.sliders)
						{
							slider.Value.value = GetValue(jiggle, slider.Key);
						}
						break;
					}
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "SaveCharacterCustomization")]
		public static class Mainframe_SaveCharacterCustomization_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("SaveCharacterCustomization");
			}

			public static void Postfix(Mainframe __instance, CharacterCustomization customization)
			{
				if (!modEnabled.Value) return;
				try
				{
					foreach (var entry in context.values)
					{
						var key = entry.Key.Split('/');
						if (customization.name != key[0]) continue;
						var id = $"{__instance.foldername}/BreastPhysics/{customization.name}.txt?tag={key[1]}";
						ES2.Save(entry.Value, id);
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError("OnSave: " + e.Message);
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "LoadCharacterCustomization")]
		public static class Mainframe_LoadCharacterCustomization_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("LoadCharacterCustomization");
			}

			public static void Postfix(Mainframe __instance, CharacterCustomization gen)
			{
				if (!modEnabled.Value) return;

				var item = $"{__instance.foldername}/BreastPhysics/{gen.name}.txt";
				if (ES2.Exists(item))
				{
					var data = ES2.LoadAll($"{__instance.foldername}/BreastPhysics/{gen.name}.txt");
					foreach (var jiggle in gen.body.rootBone.GetComponentsInChildren<Jiggle>())
					{
						if (jiggle.name.EndsWith("Pectoral"))
						{
							foreach (var d in data.loadedData)
							{
								ApplyValue(jiggle, d.Key, (float)d.Value);
								context.values[$"{gen.name}/{d.Key}"] = (float)d.Value;
							}
						}
					}
				}
				else
				{
					context.Logger.LogWarning("Missing: " + item);
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "SaveCharacterPreset")]
		public static class Mainframe_SaveCharacterPreset_Patch
		{
			public static void Postfix(CharacterCustomization customization, string presetname, string creator, Texture2D profile)
			{
				if (!modEnabled.Value) return;
				try
				{
					foreach (var jiggle in customization.body.rootBone.GetComponentsInChildren<Jiggle>())
					{
						if (jiggle.name.EndsWith("Pectoral"))
						{
							foreach (var name in context.names)
							{
								var val = GetValue(jiggle, name);
								ES2.Save(val, $"Character Presets/{presetname}/BreastPhysics.txt?tag={name}");
							}
							break;
						}
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "LoadCharacterPreset")]
		public static class Mainframe_LoadCharacterPreset_Patch
		{
			public static void Postfix(CharacterCustomization gen, string presetname)
			{
				if (!modEnabled.Value) return;
				var item = $"Character Presets/{presetname}/BreastPhysics.txt";
				if (ES2.Exists(item))
				{
					var data = ES2.LoadAll(item);
					foreach (var jiggle in gen.body.rootBone.GetComponentsInChildren<Jiggle>())
					{
						if (jiggle.name.EndsWith("Pectoral"))
						{
							foreach (var d in data.loadedData)
							{
								ApplyValue(jiggle, d.Key, (float)d.Value);
								if (context.sliders.TryGetValue(d.Key, out Slider slider))
								{
									slider.value = (float)d.Value;
								}
							}
						}
					}
				}
				else
				{
					context.Logger.LogWarning("Missing: " + item);
				}
			}
		}

		[HarmonyPatch(typeof(Jiggle), "Start")]
		public static class Jiggle_Start_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Jiggle).GetMethod("Start");
			}

			public static void Prefix(Jiggle __instance)
			{
				if (!modEnabled.Value) return;
				var cc = __instance.GetComponentInParent<CharacterCustomization>();
				if (cc && __instance.name.EndsWith("Pectoral"))
				{
					foreach (var name in context.names)
					{
						if (context.values.TryGetValue($"{cc.name}/{name}", out float val))
						{
							ApplyValue(__instance, name, val);
						}
					}
					__instance.transform.localRotation = Quaternion.identity;
				}
			}

			public static void Postfix(Jiggle __instance)
			{
				if (!modEnabled.Value) return;
				context.jiggles.Add(__instance);
				JiggleScheduler.Deregister(__instance);
			}
		}

		[HarmonyPatch(typeof(Jiggle), "OnDestroy")]
		public static class Jiggle_OnDestroy_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Jiggle).GetMethod("OnDestroy");
			}

			public static void Postfix(Jiggle __instance)
			{
				if (!modEnabled.Value) return;
				context.jiggles.Remove(__instance);
			}
		}

		[HarmonyPatch(typeof(Jiggle), "LateUpdate")]
		public static class Jiggle_LateUpdate_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Jiggle).GetMethod("LateUpdate");
			}

			public static bool Prefix(Jiggle __instance)
			{
				if (!modEnabled.Value) return true;
				if (updateMode.Value == 0 || updateMode.Value == 2) __instance.ScheduledUpdate(Time.deltaTime);
				return false;
			}
		}

		[HarmonyPatch(typeof(Jiggle), "FixedUpdate")]
		public static class Jiggle_FixedUpdate_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Jiggle).GetMethod("FixedUpdate");
			}

			public static bool Prefix(Jiggle __instance)
			{
				if (!modEnabled.Value) return true;
				if (updateMode.Value == 3) __instance.ScheduledUpdate(Time.deltaTime);
				return false;
			}
		}
	}
}
