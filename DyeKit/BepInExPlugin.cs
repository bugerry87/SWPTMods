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
	[BepInPlugin("bugerry.DyeKit", "Dye Kit", "0.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static readonly Vector2 BOTTOMLEFT = Vector2.zero;
		private static readonly Vector2 TOPLEFT = Vector2.up;
		private static readonly Vector2 TOPRIGHT = Vector2.one;
		private static readonly Vector2 BOTTOMRIGHT = Vector2.right;
		private static readonly Vector2 MID = Vector2.one * 0.5f;
		private static readonly Vector2 MIDLEFT = Vector2.up * 0.5f;
		private static readonly Vector2 BOTTOMMID = Vector2.right * 0.5f;
		private static readonly Vector2 TOPMID = new Vector2(0.5f, 1f);

		public static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;

		private static ConfigEntry<Vector2> windowSize;
		private static ConfigEntry<Vector2> windowPos;
		private static ConfigEntry<float> scrollSens;

		private static readonly List<Toggle> toggles = new List<Toggle>();
		private static DyeKit orgDyeKit = null;
		private static DyeKit dispDyeKit = null;
		private static string currentPlace = null;
		private static bool isbuild = false;

		private static RectTransform content = null;
		private static Toggle toggleTemplate = null;
		private static Slider alphaSlider = null;
		private static Slider metalSlider = null;
		private static Slider specSlider = null;
		private static Slider emissionSlider = null;
		private static Toggle toggleAll = null;
		private static readonly Button[] slots = new Button[] { null, null, null, null };

		[Serializable]
		public struct DyeKitItem
		{
			public Color color;
			public float metal;
			public float spec;
		}

		public class DyeKit : MonoBehaviour
		{
			[SerializeField]
			public DyeKitItem[] items;
			public readonly List<Material> materials = new List<Material>();

			public void Start()
			{
				materials.Clear();
				foreach (var renderer in GetComponentsInChildren<Renderer>())
				{
					materials.AddRange(renderer.materials);
				}
				SyncDye();
			}

			public void SyncDye(DyeKit other = null)
			{
				var i = 0;
				other = other ? other : this;
				if (other != this)
				{
					items = other.items;
				}

				if (items == null || items.Length == 0)
				{
					items = new DyeKitItem[materials.Count];
					foreach (var mat in materials)
					{
						items[i].color = mat.GetColor("_BaseColor");
						items[i].metal = mat.GetFloat("_Metallic");
						items[i].spec = mat.GetFloat("_Smoothness");
						++i;
					}
				}
				else
				{
					foreach (var mat in materials)
					{
						if (i < items.Length)
						{
							var dye = items[i];
							mat.SetColor("_BaseColor", dye.color);
							mat.SetFloat("_Metallic", dye.metal);
							mat.SetFloat("_Smoothness", dye.spec);
							++i;
						}
						else
						{
							break;
						}
					}
				}
			}

			public void Colorize(DyeKitItem dye)
			{
				var i = 0;
				foreach (var mat in materials)
				{
					if (toggleAll.isOn || (i < toggles.Count && toggles[i].isOn))
					{
						mat.SetColor("_BaseColor", dye.color);
						mat.SetFloat("_Metallic", dye.metal);
						mat.SetFloat("_Smoothness", dye.spec);
						items[i] = dye;
					}
					++i;
				}
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
			if (action != null) button.onClick.AddListener(action);
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
			var color = __instance.Paint;
			var image = __instance.gameObject.AddComponent<Image>();
			var click = __instance.GetComponentInChildren<ColorPickClick>();

			image.sprite = bg.sprite;
			image.color = bg.color;
			bg.raycastTarget = true;
			
			content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
			var viewport = Instantiate(bg, bg.transform);
			var scroll = bg.gameObject.AddComponent<ScrollRect>();

			viewport.gameObject.AddComponent<Mask>();

			scroll.viewport = viewport.GetComponent<RectTransform>();
			scroll.content = content;
			scroll.movementType = ScrollRect.MovementType.Elastic;
			scroll.vertical = true;
			scroll.scrollSensitivity = scrollSens.Value;

			for (var i = 0; i < slots.Length; ++i)
			{
				var slot = slots[i] = AddButton(button, "", null, color.transform);
				var sprites = slot.spriteState;
				sprites.disabledSprite = color.sprite;
				sprites.highlightedSprite = color.sprite;
				sprites.selectedSprite = color.sprite;
				sprites.pressedSprite = color.sprite;
				slot.image.sprite = color.sprite;
				slot.spriteState = sprites;
				slot.onClick.AddListener(() => {
					__instance.Paint = slot.image;
					var sat = __instance.GetSaturationXY(slot.image.color);
					click.ClickPoint = click.ClickPoint = Vector3.up * __instance.GetHueY(slot.image.color);
					__instance.OnHueClick(click);
					click.ClickPoint = new Vector3(sat.x, sat.y, 0f);
					__instance.OnStaurationClick(click);
				});

				SetPosition(
					slot.GetComponent<RectTransform>(),
					color.transform,
					Vector2.zero, Vector2.up, MIDLEFT,
					new Vector2(windowSize.Value.x * 0.20f, 0f),
					new Vector2(windowSize.Value.x * 0.24f * i, 0f),
					Vector3.one
				);
			}
			__instance.Paint = slots[0].image;

			SetPosition(
				viewport.GetComponent<RectTransform>(),
				bg.transform,
				Vector2.zero, Vector2.one, MID,
				Vector2.zero, Vector2.zero,
				Vector3.one
			);

			SetPosition(
				content,
				viewport.transform,
				Vector2.up, Vector2.one, TOPMID,
				Vector2.up, Vector2.zero,
				Vector3.one
			);

			var toggleMenue = AddButton(
				button,
				"Select Mat",
				() => bg.gameObject.SetActive(!bg.gameObject.activeSelf),
				__instance.transform
			);
			toggleMenue.transform.SetAsFirstSibling();

			toggleAll = AddToggle(
				toggleTemplate,
				"All",
				null,
				content,
				true
			);

			alphaSlider = AddSlider(
				Global.code.uiCustomization.panelSkin.GetComponentInChildren<Slider>(),
				"A", (float val) => __instance.UpdateColor(),
				color.transform
			);
			alphaSlider.value = 1;

			SetPosition(
				alphaSlider.GetComponent<RectTransform>(),
				color.transform,
				BOTTOMLEFT, BOTTOMRIGHT, TOPLEFT,
				new Vector2(-20f, 20f),
				Vector2.zero,
				Vector3.one
			);

			metalSlider = AddSlider(
				alphaSlider,
				"M", (float val) => __instance.UpdateColor(),
				color.transform
			);
			metalSlider.value = 0f;
			metalSlider.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 20f;

			specSlider = AddSlider(
				alphaSlider,
				"S", (float val) => __instance.UpdateColor(),
				color.transform, 0f, 0.9f
			);
			specSlider.value = 0f;
			specSlider.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 40f;

			emissionSlider = AddSlider(
				alphaSlider,
				"E", (float val) => __instance.UpdateColor(),
				color.transform, 1f, 10f
			);
			emissionSlider.value = 1f;
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
				content,
				TOPLEFT, TOPLEFT, TOPLEFT,
				toggleAll.GetComponent<RectTransform>().sizeDelta,
				Vector2.zero,
				Vector3.one
			);

			color.enabled = false;
		}

		public static void UpdateColorPicker(UIColorPick __instance)
		{
			var bg = __instance.transform.Find("bg");
			var panel = __instance.transform.Find("Paint (1)");
			var saturation = __instance.Saturation; //panel.transform.Find("saturation");
			var hue = __instance.Hue; //panel.transform.Find("Hue");

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
			__instance.Point_Stauration.sizeDelta = Vector2.one * 5f;

			sizeDelta = new Vector2(windowSize.Value.x * 0.10f, windowSize.Value.y * 0.4f);
			SetPosition(
				hue.GetComponent<RectTransform>(),
				panel.transform,
				TOPRIGHT, TOPRIGHT, MID,
				sizeDelta,
				sizeDelta * -0.5f,
				Vector3.one
			);
			__instance.Point_Hue.sizeDelta = new Vector2(sizeDelta.x, 5f);

			if (orgDyeKit)
			{
				var i = 0;
				foreach (var mat in orgDyeKit.materials)
				{
					if (i < toggles.Count)
					{
						toggles[i].GetComponentInChildren<Text>().text = mat.name.Replace(" (Instance)", "");
						toggles[i].gameObject.SetActive(true);
						++i;
					}
					else
					{
						var toggle = AddToggle(
							toggleAll,
							mat.name.Replace(" (Instance)", ""),
							null,
							toggleAll.transform.parent,
							false
						);
						++i;
						toggle.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 20f * i;
						toggles.Add(toggle);
					}
				}

				content.sizeDelta = Vector2.up * 20f * (i + 1);

				for (; i < toggles.Count; ++i)
				{
					toggles[i].gameObject.SetActive(false);
				}
			}
			else
			{
				content.sizeDelta = Vector2.up;
				foreach (var toggle in toggles)
				{
					toggle.gameObject.SetActive(false);
				}
			}
		}

		public static IEnumerator SyncDisplayRoutine()
		{
			while (!Global.code.uiInventory.display)
			{
				yield return new WaitForEndOfFrame();
			}

			while (Global.code.uiInventory.display)
			{
				yield return new WaitForEndOfFrame();

				var display = Global.code.uiInventory.display.GetComponent<CharacterCustomization>();
				var original = Global.code.uiInventory.curCustomization;

				display.armor?.GetComponent<DyeKit>()?.SyncDye(original.armor?.GetComponent<DyeKit>());
				display.helmet?.GetComponent<DyeKit>()?.SyncDye(original.helmet?.GetComponent<DyeKit>());
				display.gloves?.GetComponent<DyeKit>()?.SyncDye(original.gloves?.GetComponent<DyeKit>());
				display.shoes?.GetComponent<DyeKit>()?.SyncDye(original.shoes?.GetComponent<DyeKit>());
				display.leggings?.GetComponent<DyeKit>()?.SyncDye(original.leggings?.GetComponent<DyeKit>());
				display.necklace?.GetComponent<DyeKit>()?.SyncDye(original.necklace?.GetComponent<DyeKit>());
				display.ring?.GetComponent<DyeKit>()?.SyncDye(original.ring?.GetComponent<DyeKit>());
				display.panties?.GetComponent<DyeKit>()?.SyncDye(original.panties?.GetComponent<DyeKit>());
				display.bra?.GetComponent<DyeKit>()?.SyncDye(original.bra?.GetComponent<DyeKit>());
				display.stockings?.GetComponent<DyeKit>()?.SyncDye(original.stockings?.GetComponent<DyeKit>());
				display.lingerieGloves?.GetComponent<DyeKit>()?.SyncDye(original.lingerieGloves?.GetComponent<DyeKit>());
				display.shield?.GetComponent<DyeKit>()?.SyncDye(original.shield?.GetComponent<DyeKit>());
				display.weapon?.GetComponent<DyeKit>()?.SyncDye(original.weapon?.GetComponent<DyeKit>());
				display.weapon2?.GetComponent<DyeKit>()?.SyncDye(original.weapon2?.GetComponent<DyeKit>());
				display.suspenders?.GetComponent<DyeKit>()?.SyncDye(original.suspenders?.GetComponent<DyeKit>());
				display.heels?.GetComponent<DyeKit>()?.SyncDye(original.heels?.GetComponent<DyeKit>());
			}
		}

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 135, "Nexus mod ID for updates");

			windowSize = Config.Bind("UI", "Color Picker Size", new Vector2(130f, 240f), "Control the window size of the Color Picker");
			windowPos = Config.Bind("UI", "Color Picker Position", new Vector2(-160f, 50f), "Control the window size of the Color Picker");
			scrollSens = Config.Bind("UI", "Scroll Sensitivity", 20f, "Step width for the scroll sensitivity");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		[HarmonyPatch(typeof(UIInventory), nameof(UIInventory.Start))]
		public static class UIInventory_Start_Patch
		{
			public static void Postfix(UIInventory __instance)
			{
				if (!modEnabled.Value) return;
				if (!toggleTemplate) toggleTemplate = __instance.GetComponentInChildren<Toggle>();
			}
		}

		[HarmonyPatch(typeof(UIInventory), "GenerateDisplay")]
		public static class UIInventory_GenerateDisplay_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(UIInventory).GetMethod("GenerateDisplay");
			}

			public static void Postfix(UIInventory __instance)
			{
				if (!modEnabled.Value) return;
				__instance.StartCoroutine(SyncDisplayRoutine());
			}
		}

		[HarmonyPatch(typeof(Item), nameof(Item.InstantiateModel))]
		public static class Item_InstantiateModel_Patch
		{
			public static void Postfix(Item __instance)
			{
				if (!modEnabled.Value) return;
				if (__instance.TryGetComponent(out DyeKit dye))
				{
					dye.Start();
				}
				else
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
				orgDyeKit = null;
				dispDyeKit = null;

				if (__instance?.item)
				{
					if (__instance.item.TryGetComponent(out DyeKit dye))
					{
						orgDyeKit = dye;
					}
					else
					{
						orgDyeKit = __instance.item.gameObject.AddComponent<DyeKit>();
					}
				}
				else
				{
					return false;
				}

				var display = Global.code.uiInventory.display;
				foreach (var dyekit in display.GetComponentsInChildren<DyeKit>())
				{
					if (dyekit.GetComponent<Item>().slotType == __instance.slotType)
					{
						dispDyeKit = dyekit;
						break;
					}
				}

				Global.code.uiColorPick.Open(Global.code.uiColorPick.Paint.color, "EquipmentSlot");
				return false;
			}
		}

		[HarmonyPatch(typeof(InventoryClosetIcon), nameof(InventoryClosetIcon.Initiate))]
		public static class InventoryClosetIcon_Initiate_Patch
		{
			public static void OnClick(InventoryClosetIcon __instance)
			{
				var cc = Global.code.uiInventory.curCustomization;
				var display = Global.code.uiInventory.display.GetComponent<CharacterCustomization>();
				var slot = __instance.item.GetComponent<Item>().slotType;
				orgDyeKit = null;
				dispDyeKit = null;
				Transform org = null;
				Transform disp = null;

				switch (slot)
				{
					case SlotType.bra:
						org = cc?.bra;
						disp = display?.bra;
						break;
					case SlotType.lingeriegloves:
						org = cc?.lingerieGloves;
						disp = display?.lingerieGloves;
						break;
					case SlotType.panties:
						org = cc?.panties;
						disp = display?.panties;
						break;
					case SlotType.stockings:
						org = cc?.stockings;
						disp = display?.stockings;
						break;
					case SlotType.suspenders:
						org = cc?.suspenders;
						disp = display?.suspenders;
						break;
					case SlotType.heels:
						org = cc?.heels;
						disp = display?.heels;
						break;
					default:
						break;
				}
				DyeKit dye;
				orgDyeKit = org && org.TryGetComponent(out dye) ? dye : org?.gameObject.AddComponent<DyeKit>();
				dispDyeKit = org && org.TryGetComponent(out dye) ? dye : disp?.gameObject.AddComponent<DyeKit>();
				var color = orgDyeKit.materials.Count > 0 ? orgDyeKit.materials[0].color : Color.red;
				Global.code.uiColorPick.Open(Global.code.uiColorPick.Paint.color, "EquipmentSlot");
			}

			public static void Postfix(InventoryClosetIcon __instance, Transform _item)
			{
				if (!modEnabled.Value) return;
				if (!__instance.selection.activeSelf) return;

				var button = AddButton(
					Global.code.uiColorPick.GetComponentInChildren<Button>(),
					"Color",
					() => OnClick(__instance),
					__instance.transform
				);
				SetPosition(
					button.GetComponent<RectTransform>(),
					__instance.transform,
					Vector2.zero, Vector2.right, BOTTOMMID,
					Vector2.up * 10f,
					Vector2.zero,
					Vector3.one
				);
			}
		}

		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.Open))]
		public static class UIColorPick_Open_Patch
		{
			public static void Prefix(UIColorPick __instance, Color C, string place)
			{
				if (!modEnabled.Value) return;
				currentPlace = place;

				if (place != "EquipmentSlot")
				{
					orgDyeKit = null;
					dispDyeKit = null;
				}

				var cc = Global.code.uiMakeup.curCustomization;
				if (cc?.hair && place == "Hair Color Picker")
				{
					orgDyeKit = cc.hair.TryGetComponent(out DyeKit dye) ? dye : cc.hair.gameObject.AddComponent<DyeKit>();
					orgDyeKit?.Start();
				}

				if (!isbuild)
				{
					BuildColorPicker(__instance);
					isbuild = true;
				}
				UpdateColorPicker(__instance);
			}
		}

		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.GetSaturationXY))]
		public static class UIColorPick_GetSaturationXY_Patch
		{
			public static bool Prefix(Color color, UIColorPick __instance, out ValueTuple<float, float> __result)
			{
				Color.RGBToHSV(color, out float H, out float S, out float V);
				var sat = __instance.Saturation.rectTransform.sizeDelta * (new Vector2(S, V) - MID);
				__result = new ValueTuple<float, float>(sat.x, sat.y);
				return !modEnabled.Value;
			}
		}

		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.GetHueY))]
		public static class UIColorPick_GetHueY_Patch
		{
			public static bool Prefix(Color color, UIColorPick __instance, out float __result)
			{
				Color.RGBToHSV(color, out float H, out float S, out float V);
				__result = __instance.Hue.rectTransform.sizeDelta.y * (H - 0.5f);
				return !modEnabled.Value;
			}
		}


		[HarmonyPatch(typeof(UIColorPick), nameof(UIColorPick.UpdateColor))]
		public static class UIColorPick_UpdateColor_Patch
		{
			public static void Postfix(UIColorPick __instance)
			{
				if (!modEnabled.Value || !orgDyeKit || !isbuild) return;

				var color = __instance.Paint.color;
				color.a = alphaSlider.value;
				__instance.Paint.color = color;

				if (currentPlace == "Hair Color Picker")
				{
					var cc = Global.code.uiMakeup.curCustomization;
					cc.hair.TryGetComponent(out orgDyeKit);
					orgDyeKit?.Start();
				}

				var dye = new DyeKitItem()
				{
					color = color * emissionSlider.value,
					metal = metalSlider.value,
					spec = specSlider.value
				};
				orgDyeKit?.Colorize(dye);
				dispDyeKit?.Colorize(dye);
			}
		}

		[HarmonyPatch(typeof(Mainframe), "SaveItem")]
		public static class Mainframe_SaveItem_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Mainframe).GetMethod("SaveItem");
			}

			public static void Postfix(Mainframe __instance, Transform item)
			{
				if (!modEnabled.Value) return;
				try
				{
					var dyeKit = item.GetComponent<DyeKit>();
					if (dyeKit)
					{
						var i = 0;
						var colors = new Color[dyeKit.items.Length];
						var metals = new float[dyeKit.items.Length];
						var specs = new float[dyeKit.items.Length];
						foreach (var dye in dyeKit.items)
						{
							colors[i] = dye.color;
							metals[i] = dye.metal;
							specs[i] = dye.spec; 
							++i;
						}
						ES2.Save(colors, $"{__instance.GetFolderName()}DyeKit.txt?tag=colors{item.GetInstanceID()}");
						ES2.Save(metals, $"{__instance.GetFolderName()}DyeKit.txt?tag=metals{item.GetInstanceID()}");
						ES2.Save(specs, $"{__instance.GetFolderName()}DyeKit.txt?tag=specs{item.GetInstanceID()}");
					}
					
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}

			[HarmonyPatch(typeof(Mainframe), "LoadItem")]
			public static class Mainframe_LoadItem_Patch
			{
				public static MethodBase TargetMethod()
				{
					return typeof(Mainframe).GetMethod("LoadItem");
				}

				public static void Postfix(int id, Mainframe __instance, Transform __result)
				{
					if (!modEnabled.Value) return;
					if (!__result || !ES2.Exists($"{__instance.GetFolderName()}DyeKit.txt?tag=colors{id}"))
					{
						return;
					}

					try
					{
						var dyeKit = __result.gameObject.AddComponent<DyeKit>();
						var colors = ES2.LoadArray<Color>($"{__instance.GetFolderName()}DyeKit.txt?tag=colors{id}");
						var metals = ES2.LoadArray<float>($"{__instance.GetFolderName()}DyeKit.txt?tag=metals{id}");
						var specs = ES2.LoadArray<float>($"{__instance.GetFolderName()}DyeKit.txt?tag=specs{id}");
						dyeKit.items = new DyeKitItem[colors.Length];

						for (var i = 0; i < dyeKit.items.Length; ++i)
						{
							dyeKit.items[i] = new DyeKitItem()
							{
								color = colors[i],
								metal = metals[i],
								spec = specs[i]
							};
						}

						if (isDebug.Value) context.Logger.LogInfo($"DyeKit loaded: {dyeKit.name} {id} with {dyeKit.items.Length} items");
					}
					catch (Exception e)
					{
						context.Logger.LogError(e);
					}
				}
			}

			[HarmonyPatch(typeof(Mainframe), nameof(Mainframe.LoadGame))]
			public static class Mainframe_LoadGame_Patch
			{
				public static void Prefix()
				{
					isbuild = false;
				}
			}
		}
	}
}
