using System;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace CustomizationFix
{
	[BepInPlugin("bugerry.CustomizationFix", "CustomizationFix", "1.3.2")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;
		public static ConfigEntry<bool> fixNipples;

		public static readonly Dictionary<string, Transform> bone_register = new Dictionary<string, Transform>();
		public readonly Dictionary<string, float> stats = new Dictionary<string, float>();

		private void Awake()
		{
			context = this;
			Config.SaveOnConfigSet = false;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 60, "Nexus mod ID for updates");
			fixNipples = Config.Bind("General", "Fix Nipples", true, "Prevents nipples from poking through bras and armour");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.Open))]
		public static class UICustomization_Open_Patch
		{
			public static void Postfix(UICustomization __instance, CharacterCustomization customization, bool isOpenChangeName = true)
			{
				if (!modEnabled.Value) return;
				var slider = __instance.panelSkin.GetComponentInChildren<Slider>();
				if (slider && customization)
				{
					slider.minValue = -1f;
					slider.maxValue = -0.25f;
					slider.value = customization.skinGlossiness;
				}
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonBody))]
		public static class UICustomization_ButtonBody_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonBreasts))]
		public static class UICustomization_ButtonBreasts_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonEyes))]
		public static class UICustomization_ButtonEyes_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonFace))]
		public static class UICustomization_ButtonFace_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonMouth))]
		public static class UICustomization_ButtonMouth_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonNose))]
		public static class UICustomization_ButtonNose_Patch
		{
			public static void Postfix(UICustomization __instance)
			{
				if (!modEnabled.Value) return;
				__instance.SyncSliders();
			}
		}

		[HarmonyPatch(typeof(CharacterCustomization), nameof(CharacterCustomization.RefreshClothesVisibility))]
		public static class CharacterCustomization_RefreshClothesVisibility_Patch
		{
			public static void Postfix(CharacterCustomization __instance)
			{
				if (!modEnabled.Value && !fixNipples.Value) return;
				
				if (__instance.armor && __instance.armor.gameObject.activeSelf)
				{
					__instance.body.SetBlendShapeWeight(Player.code.nipplesLargeIndex, 0f);
					__instance.body.SetBlendShapeWeight(Player.code.nipplesDepthIndex, 0f);
				}
				else if (__instance.bra && __instance.bra.gameObject.activeSelf)
				{
					__instance.body.SetBlendShapeWeight(Player.code.nipplesLargeIndex, 0f);
					__instance.body.SetBlendShapeWeight(Player.code.nipplesDepthIndex, 0f);
				}
				else
				{
					var name = __instance.body.sharedMesh.GetBlendShapeName(Player.code.nipplesLargeIndex);
					if (context.stats.TryGetValue($"{__instance.name}/{name}", out float nippleSize))
					{
						__instance.body.SetBlendShapeWeight(Player.code.nipplesLargeIndex, nippleSize);
					}

					name = __instance.body.sharedMesh.GetBlendShapeName(Player.code.nipplesDepthIndex);
					if (context.stats.TryGetValue($"{__instance.name}/{name}", out float nippleDepth))
					{
						__instance.body.SetBlendShapeWeight(Player.code.nipplesDepthIndex, nippleDepth);
					}
				}
			}
		}

		[HarmonyPatch(typeof(CharacterCustomization), nameof(CharacterCustomization.SyncBlendshape))]
		public static class CharacterCustomization_SyncBlendshapes_Patch
		{
			public static bool Prefix(CharacterCustomization __instance)
			{
				if (!modEnabled.Value || !Player.code) return true;
				var cc = __instance;
				var sizeIndex = Player.code.nipplesLargeIndex;
				var depthIndex = Player.code.nipplesDepthIndex;

				var name = cc.body.sharedMesh.GetBlendShapeName(sizeIndex);
				context.stats.TryGetValue($"{cc.name}/{name}", out float nippleSize);
				name = cc.body.sharedMesh.GetBlendShapeName(depthIndex);
				context.stats.TryGetValue($"{cc.name}/{name}", out float nippleDepth);

				foreach (var mesh in cc.GetComponentsInChildren<SkinnedMeshRenderer>())
				{
					if (!mesh || mesh == cc.body) continue;
					for (int i = 0; i < mesh.sharedMesh.blendShapeCount; ++i)
					{
						if (i >= cc.body.sharedMesh.blendShapeCount) break;
						if (fixNipples.Value && i == sizeIndex)
						{
							mesh.SetBlendShapeWeight(sizeIndex, nippleSize);
						}
						else if (fixNipples.Value && i == depthIndex)
						{
							mesh.SetBlendShapeWeight(depthIndex, nippleDepth);
						}
						else
						{
							mesh.SetBlendShapeWeight(i, cc.body.GetBlendShapeWeight(i));
						}
					}
					mesh.shadowCastingMode = ShadowCastingMode.Off;
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(CustomizationSlider), "Start")]
		public static class CustomizationSlider_Start_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(CustomizationSlider).GetMethod("Start");
			}

			public static bool Prefix(CustomizationSlider __instance)
			{
				if (modEnabled.Value)
				{
					if (Player.code == null) return false;
					__instance.index = Player.code.customization.body.sharedMesh.GetBlendShapeIndex(__instance.blendshapename);
					if (__instance.isFacePreset)
					{
						Global.code.uiCustomization.facePresetIndexes.Add(__instance.index);
					}
					__instance.Refresh();
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(CustomizationSlider), nameof(CustomizationSlider.Refresh))]
		public static class CustomizationSlider_Refresh_Patch
		{
			public static bool Prefix(CustomizationSlider __instance)
			{
				if (!modEnabled.Value) return true;
				var cc = Global.code.uiCustomization.curCharacterCustomization;
				if (cc && __instance.blendshapename.Length > 0 && !__instance.isEmotionController)
				{
					var name = $"{cc.name}/{__instance.blendshapename}";
					if (__instance.index >= 0 && __instance.index < cc.body.sharedMesh.blendShapeCount)
					{
						if (!context.stats.TryGetValue(name, out float val))
						{
							val = cc.body.GetBlendShapeWeight(__instance.index);
						}
						__instance.GetComponent<Slider>().value = val;
					}
					else
					{
						__instance.GetComponent<Slider>().value = 0f;
					}
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(CustomizationSlider), nameof(CustomizationSlider.ValueChange))]
		public static class CustomizationSlider_ValueChange_Patch
		{
			public static void Postfix(float val, CustomizationSlider __instance)
			{
				if (!modEnabled.Value || __instance.isEmotionController) return;

				var cc = Global.code.uiCustomization.curCharacterCustomization;
				if (cc && __instance.index >= 0 && __instance.index < cc.body.sharedMesh.blendShapeCount)
				{
					var name = cc.body.sharedMesh.GetBlendShapeName(__instance.index);
					name = $"{cc.name}/{name}";
					context.stats[name] = val;
					cc.SyncBlendshape();
				}
			}
		}

		[HarmonyPatch(typeof(Appeal), nameof(Appeal.SyncBreathing))]
		public static class Appeal_SyncBreathing_Patch
		{
			public static bool Prefix(Appeal __instance)
			{
				if (!Player.code || !modEnabled.Value) return true;
				foreach (var mesh in __instance.allRenderers)
				{
					if (mesh && Player.code.stomachDepthIndex < mesh.sharedMesh.blendShapeCount && Player.code.chestWidthIndex < mesh.sharedMesh.blendShapeCount)
					{
						mesh.SetBlendShapeWeight(Player.code.stomachDepthIndex, __instance._CharacterCustomization.body.GetBlendShapeWeight(Player.code.stomachDepthIndex));
						mesh.SetBlendShapeWeight(Player.code.chestWidthIndex, __instance._CharacterCustomization.body.GetBlendShapeWeight(Player.code.chestWidthIndex));
					}
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(Appeal), "GetAllRenderers")]
		public static class Appeal_GetAllRenderers_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("GetAllRenderers");
			}

			public static bool Prefix(Appeal __instance)
			{
				if (!modEnabled.Value) return true;
				if (__instance.allRenderers == null)
				{
					__instance.allRenderers = new List<SkinnedMeshRenderer>();
				}
				else
				{
					__instance.allRenderers.Clear();
				}
				__instance.allRenderers.AddRange(__instance.GetComponentsInChildren<SkinnedMeshRenderer>());
				return false;
			}
		}

		[HarmonyPatch(typeof(Appeal), nameof(Appeal.InstantiateSet))]
		public static class Appeal_InstantiateSet_Patch
		{
			public static bool Prefix(Appeal __instance, CharacterCustomization _character)
			{
				if (!modEnabled.Value || !_character || !__instance) return true;

				__instance._CharacterCustomization = _character;
				__instance.gameObject.SetActive(true);

				foreach (var bone in __instance._CharacterCustomization.body.bones)
				{
					bone_register[bone.name] = bone;
				}

				foreach (var mesh in __instance.GetComponentsInChildren<SkinnedMeshRenderer>())
				{
					mesh.rootBone = __instance._CharacterCustomization.body.rootBone;
					var bones = new Transform[mesh.bones.Length];
					for (var i = 0; i < mesh.bones.Length; ++i)
					{
						if (mesh.bones[i] && bone_register.TryGetValue(mesh.bones[i].name, out Transform bone))
						{
							bones[i] = bone;
						}
					}
					mesh.bones = bones;
				}

				if (__instance.hideHair && __instance._CharacterCustomization.hair)
				{
					__instance._CharacterCustomization.hair.gameObject.SetActive(false);
				}

				__instance._CharacterCustomization.RefreshClothesVisibility();
				Appeal_GetAllRenderers_Patch.Prefix(__instance);
				__instance.SyncBlendshape();
				return false;
			}
		}

		[HarmonyPatch(typeof(LoadPresetIcon), nameof(LoadPresetIcon.ButtonLoad))]
		public static class LoadPresetIcon_ButtonLoad_Patch
		{
			public static bool Prefix(LoadPresetIcon __instance)
			{
				if (!modEnabled.Value) return true;
				var cc = Global.code.uiCustomization.curCharacterCustomization;
				Mainframe.code.LoadCharacterPreset(cc, __instance.foldername);
				Global.code.uiCustomization.panelLoadPreset.SetActive(false);
				Global.code.uiCombat.ShowHeader("Character Loaded");
				return false;
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

				try
				{
					var key = gen.body.sharedMesh.GetBlendShapeName(Player.code.nipplesLargeIndex);
					context.stats[$"{gen.name}/{key}"] = ES2.Load<float>(__instance.GetFolderName() + gen.name + ".txt?tag=nippleLarge");
					key = gen.body.sharedMesh.GetBlendShapeName(Player.code.nipplesDepthIndex);
					context.stats[$"{gen.name}/{key}"] = ES2.Load<float>(__instance.GetFolderName() + gen.name + ".txt?tag=nippleDepth");
					gen.SyncBlendshape();
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
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
					var name = customization.body.sharedMesh.GetBlendShapeName(Player.code.nipplesLargeIndex);
					if (context.stats.TryGetValue($"{customization.name}/{name}", out float nippleLarge))
					{
						ES2.Save(nippleLarge, __instance.GetFolderName() + customization.name + ".txt?tag=nippleLarge");
					}

					name = customization.body.sharedMesh.GetBlendShapeName(Player.code.nipplesDepthIndex);
					if (context.stats.TryGetValue($"{customization.name}/{name}", out float nippleDepth))
					{
						ES2.Save(nippleDepth, __instance.GetFolderName() + customization.name + ".txt?tag=nippleDepth");
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}
		}
	}
}
