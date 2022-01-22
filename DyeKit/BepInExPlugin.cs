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
	[BepInPlugin("bugerry.DyeKit", "Dye Kit", "0.0.1")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static readonly Vector2 BOTTOMLEFT = Vector2.zero;
		private static readonly Vector2 TOPLEFT = Vector2.up;
		private static readonly Vector2 TOPRIGHT = Vector2.one;
		private static readonly Vector2 BOTTOMRIGHT = Vector2.right;
		private static readonly Vector2 MID = Vector2.one * 0.5f;
		private static readonly Vector2 BOTTOMMID = Vector2.right * 0.5f;
		private static readonly Vector2 TOPMID = new Vector2(0.5f, 1f);

		public static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;

		private static ConfigEntry<Vector2> windowSize;
		private static ConfigEntry<Vector2> windowPos;

		private static readonly Dictionary<string, Renderer> rendererMap = new Dictionary<string, Renderer>();
		private static readonly List<Toggle> toggles = new List<Toggle>();
		private static DyeKit currentDyeKit = null;
		private static string currentPlace = null;
		private static bool isbuild = false;

		private static Slider alphaSlider = null;
		private static Slider metalSlider = null;
		private static Slider specSlider = null;
		private static Slider emissionSlider = null;
		private static Toggle toggleAll = null;

		[Serializable]
		public struct DyeKitItem
		{
			public string name;
			public Color color;
			public float metal;
			public float spec;
		}

		public class DyeKit : MonoBehaviour
		{
			public DyeKitItem[] dyeKitItems;

			public void Start()
			{
				var renderers = GetComponentsInChildren<Renderer>();
				var dyeKitList = new List<DyeKitItem>();
				
				foreach (var renderer in renderers)
				{
					foreach (var mat in renderer.materials)
					{
						if (mat.shader.name == "HDRP/Lit")
						{
							dyeKitList.Add(new DyeKitItem() {
								name = mat.name.Replace(" (Instance)", ""),
								color = mat.GetColor("_BaseColor"),
								metal = mat.GetFloat("_Metallic"),
								spec = mat.GetFloat("_Smoothness")
							});
						}
					}
				}
				dyeKitItems = dyeKitList.ToArray();
			}

			public void Refresh()
			{
				if (dyeKitItems == null) return;
				var i = 0;
				var renderers = GetComponentsInChildren<Renderer>();
				foreach (var renderer in renderers)
				{
					foreach (var mat in renderer.materials)
					{
						if (i < dyeKitItems.Length && mat.shader.name == "HDRP/Lit")
						{
							var dye = dyeKitItems[i];
							mat.SetColor("_BaseColor", dye.color);
							mat.SetFloat("_Metallic", dye.metal);
							mat.SetFloat("_Smoothness", dye.spec);
							++i;
						}
					}
				}
			}

			public void OnEnable()
			{
				Refresh();
			}
		}

		public static Button AddButton(
			Button template,
			string name,
			UnityAction action,
			Transform parent = null)
		{
			var button = Instantiate(template, parent ? parent : template.transform.parent);
			button.name = $"{name} Button";
			button.GetComponentInChildren<Text>().name = $"{name} Text";
			button.GetComponentInChildren<Text>().text = name;
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(action);
			Destroy(button.GetComponentInChildren<LocalizationText>());
			return button;
		}

		public static Toggle AddToggle(
			Toggle template,
			string name,
			UnityAction<bool> action = null,
			Transform parent = null,
			bool current = false)
		{
			var toggle = Instantiate(template, parent ? parent : template.transform.parent);
			toggle.name = $"{name} Toggle";
			toggle.GetComponentInChildren<Text>().name = $"{name} Text";
			toggle.GetComponentInChildren<Text>().text = name;
			toggle.onValueChanged = new Toggle.ToggleEvent();
			if (action != null) toggle.onValueChanged.AddListener(action);
			toggle.isOn = current;
			Destroy(toggle.GetComponentInChildren<LocalizationText>());
			return toggle;
		}

		public static Slider AddSlider(
			Slider template,
			string name,
			UnityAction<float> action,
			Transform parent = null,
			float min = 0f,
			float max = 1f)
		{
			var slider = Instantiate(template, parent ? parent : template.transform.parent);
			slider.name = $"{name} Slider";
			slider.GetComponentInChildren<Text>().name = $"{name} Text";
			slider.GetComponentInChildren<Text>().text = name;
			slider.onValueChanged = new Slider.SliderEvent();
			slider.onValueChanged.AddListener(action);
			slider.minValue = min;
			slider.maxValue = max;
			Destroy(slider.GetComponentInChildren<LocalizationText>());
			return slider;
		}

		public static RectTransform SetPosition(
			RectTransform rect,
			Transform parent,
			Vector2 anchorMin,
			Vector2 ancorMax,
			Vector2 pivot,
			Vector2 sizeDelta,
			Vector2 anchoredPosition,
			Vector3 scale)
		{
			rect.SetParent(parent);
			rect.anchorMin = anchorMin;
			rect.anchorMax = ancorMax;
			rect.pivot = pivot;
			rect.sizeDelta = sizeDelta;
			rect.anchoredPosition = anchoredPosition;
			rect.localScale = scale;
			return rect;
		}

		public static void BuildColorPicker(UIColorPick __instance)
		{
			var button = __instance.GetComponentInChildren<Button>();
			var bg = __instance.transform.Find("bg").GetComponent<Image>();
			var panel = __instance.transform.Find("Paint (1)");
			var color = panel.transform.Find("Paint");
			var image = __instance.gameObject.AddComponent<Image>();
			image.sprite = bg.sprite;
			image.color = bg.color;
			bg.raycastTarget = true;

			var toggleMenue = AddButton(
				button,
				"Select Mat",
				() => bg.gameObject.SetActive(!bg.gameObject.activeSelf),
				__instance.transform
			);

			toggleAll = AddToggle(
				Global.code.uiInventory.GetComponentInChildren<Toggle>(),
				"All",
				(bool check) =>
				{
					foreach (var toggle in bg.GetComponentsInChildren<Toggle>())
					{
						toggle.isOn = check;
					}
				},
				bg.transform,
				true
			);

			alphaSlider = AddSlider(
				Global.code.uiCustomization.panelSkin.GetComponentInChildren<Slider>(),
				"A", (float val) => __instance.UpdateColor(),
				color
			);
			alphaSlider.value = 1;

			SetPosition(
				alphaSlider.GetComponent<RectTransform>(),
				color,
				BOTTOMLEFT, BOTTOMRIGHT, TOPLEFT,
				new Vector2(-20f, 20f),
				Vector2.zero,
				Vector3.one
			);

			metalSlider = AddSlider(
				alphaSlider,
				"M", (float val) => __instance.UpdateColor(),
				color
			);
			metalSlider.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 20f;

			specSlider = AddSlider(
				alphaSlider,
				"S", (float val) => __instance.UpdateColor(),
				color
			);
			specSlider.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 40f;

			emissionSlider = AddSlider(
				alphaSlider,
				"E", (float val) => __instance.UpdateColor(),
				color, 1f, 10f
			);
			emissionSlider.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 60f;

			SetPosition(
				button.GetComponent<RectTransform>(),
				__instance.transform,
				BOTTOMLEFT, BOTTOMRIGHT, BOTTOMMID,
				new Vector2(-6f, 20f),
				new Vector2(0f, 3f),
				Vector3.one
			);

			SetPosition(
				toggleMenue.GetComponent<RectTransform>(),
				__instance.transform,
				TOPLEFT, TOPRIGHT, TOPMID,
				new Vector2(-6f, 20f),
				new Vector2(0f, -3f),
				Vector3.one
			);

			SetPosition(
				color.GetComponent<RectTransform>(),
				panel.transform,
				BOTTOMLEFT, BOTTOMRIGHT, TOPMID,
				new Vector2(0, 15f),
				new Vector2(0f, -3f),
				Vector3.one
			);

			SetPosition(
				toggleAll.GetComponent<RectTransform>(),
				bg.transform,
				TOPLEFT, TOPLEFT, TOPLEFT,
				toggleAll.GetComponent<RectTransform>().sizeDelta,
				Vector2.zero,
				Vector3.one
			);
		}

		public static void UpdateColorPicker(UIColorPick __instance)
		{
			var bg = __instance.transform.Find("bg");
			var panel = __instance.transform.Find("Paint (1)");
			var saturation = panel.transform.Find("saturation");
			var hue = panel.transform.Find("Hue");

			SetPosition(
				__instance.GetComponent<RectTransform>(),
				__instance.transform.parent,
				MID, MID, MID,
				windowSize.Value,
				windowPos.Value,
				Vector3.one
			);

			SetPosition(
				panel.GetComponent<RectTransform>(),
				__instance.transform,
				TOPLEFT, TOPRIGHT, TOPMID,
				new Vector2(-6f, windowSize.Value.y * 0.4f),
				new Vector2(0f, -26f),
				Vector3.one
			);

			SetPosition(
				bg.GetComponent<RectTransform>(),
				__instance.transform,
				TOPLEFT, TOPLEFT, TOPRIGHT,
				__instance.GetComponent<RectTransform>().sizeDelta,
				Vector2.zero,
				Vector3.one
			);
			bg.gameObject.SetActive(false);

			var sizeDelta = new Vector2(windowSize.Value.x * 0.80f, windowSize.Value.y * 0.4f);
			SetPosition(
				saturation.GetComponent<RectTransform>(),
				panel.transform,
				TOPLEFT, TOPLEFT, MID,
				sizeDelta,
				new Vector2(sizeDelta.x, -sizeDelta.y) * 0.5f,
				Vector3.one
			);
			saturation.Find("Image").GetComponent<RectTransform>().sizeDelta = Vector2.one * 5f;

			sizeDelta = new Vector2(windowSize.Value.x * 0.10f, windowSize.Value.y * 0.4f);
			SetPosition(
				hue.GetComponent<RectTransform>(),
				panel.transform,
				TOPRIGHT, TOPRIGHT, MID,
				sizeDelta,
				sizeDelta * -0.5f,
				Vector3.one
			);
			hue.Find("Image").GetComponent<RectTransform>().sizeDelta = new Vector2(sizeDelta.x, 5f);

			if (currentDyeKit && currentDyeKit.dyeKitItems != null)
			{
				for (var i = 0; i < currentDyeKit.dyeKitItems.Length; ++i)
				{
					if (i < toggles.Count)
					{
						toggles[i].GetComponentInChildren<Text>().text = currentDyeKit.dyeKitItems[i].name;
						toggles[i].gameObject.SetActive(true);
					}
					else
					{
						toggles.Add(AddToggle(
							toggleAll,
							currentDyeKit.dyeKitItems[i].name,
							null,
							toggleAll.transform.parent,
							false
						));
					}
					toggles[i].GetComponent<RectTransform>().anchoredPosition = Vector2.down * 20f * (i + 1);
				}

				for (var i = currentDyeKit.dyeKitItems.Length; i < toggles.Count; ++i)
				{
					toggles[i].gameObject.SetActive(false);
				}
			}
		}

		public static IEnumerator UpdateDisplayRoutine()
		{
			if (Global.code.uiInventory.isActiveAndEnabled && Global.code.uiColorPick.isActiveAndEnabled)
			{
				yield return new WaitForEndOfFrame();
				rendererMap.Clear();
				var original = Global.code.uiInventory.curCustomization;
				var display = Global.code.uiInventory.display;
				if (!original || !display)
				{
					yield break;
				}

				foreach (var renderer in original.GetComponentsInChildren<Renderer>())
				{
					rendererMap[renderer.name] = renderer;
				}

				foreach (var disp in display.GetComponentsInChildren<Renderer>())
				{
					if (rendererMap.TryGetValue(disp.name, out Renderer org))
					{
						disp.materials = org.materials;
						context.Logger.LogInfo($"{disp.name} - {org.name}");
					}
				}
			}
			yield break;
		}

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 21, "Nexus mod ID for updates");

			windowSize = Config.Bind("UI", "Color Picker Size", new Vector2(130f, 240f), "Control the window size of the Color Picker");
			windowPos = Config.Bind("UI", "Color Picker Position", new Vector2(0f, 0f), "Control the window size of the Color Picker");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		[HarmonyPatch(typeof(Item), nameof(Item.InstantiateModel))]
		public static class Item_InstantiateModel_Patch
		{
			public static void Postfix(Item __instance)
			{
				if (!__instance.GetComponent<DyeKit>())
				{
					__instance.gameObject.AddComponent<DyeKit>();
				}
			}
		}

		[HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.Click))]
		public static class EquipmentSlot_Click_Patch
		{
			public static bool Prefix(EquipmentSlot __instance)
			{
				if (!modEnabled.Value) return true;
				if (!Input.GetMouseButton(1)) return true;
				currentDyeKit = null;

				if (__instance?.item)
				{
					if (__instance.item.TryGetComponent(out DyeKit dye))
					{
						currentDyeKit = dye;
					}
					else
					{
						currentDyeKit = __instance.item.gameObject.AddComponent<DyeKit>();
					}
				}
				else
				{
					return false;
				}
				var color = currentDyeKit.dyeKitItems.Length > 0 ? currentDyeKit.dyeKitItems[0].color : Color.red;
				Global.code.uiColorPick.Open(color, "EquipmentSlot");
				return false;
			}
		}

		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.Open))]
		public static class UIColorPick_Open_Patch
		{
			public static void Prefix(UIColorPick __instance, Color C, string place)
			{
				if (!modEnabled.Value) return;
				currentPlace = place;

				if (!isbuild)
				{
					BuildColorPicker(__instance);
					isbuild = true;
				}
				UpdateColorPicker(__instance);
			}

			public static void Postfix(UIColorPick __instance)
			{
				__instance.StartCoroutine(UpdateDisplayRoutine());
			}
		}

		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.UpdateColor))]
		public static class UIColorPick_UpdateColor_Patch
		{
			public static void Postfix(UIColorPick __instance)
			{
				if (!modEnabled.Value || !currentDyeKit || !isbuild) return;

				var color = __instance.Paint.color;
				color.a = alphaSlider.value;
				__instance.Paint.color = color;

				if (currentPlace == "EquipmentSlot")
				{
					for (var i = 0; i < toggles.Count; ++i)
					{
						var toggle = toggles[i];
						if (i < currentDyeKit.dyeKitItems?.Length && toggleAll.isOn || toggle.isOn)
						{
							currentDyeKit.dyeKitItems[i].color = color * emissionSlider.value;
							currentDyeKit.dyeKitItems[i].metal = metalSlider.value;
							currentDyeKit.dyeKitItems[i].spec = specSlider.value;
						}
					}
					currentDyeKit.Refresh();
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), nameof(Mainframe.SaveGame))]
		public static class Mainframe_SaveGame_Patch
		{
			public static void Postfix()
			{

			}
		}

		[HarmonyPatch(typeof(Mainframe), nameof(Mainframe.LoadGame))]
		public static class Mainframe_LoadGame_Patch
		{
			public static void Postfix()
			{
				isbuild = false;
			}
		}
	}
}
