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
	[BepInPlugin("bugerry.DyeKit", "Dye Kit", "1.2.1")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static readonly Vector2 BOTTOMLEFT = Vector2.zero;
		private static readonly Vector2 TOPLEFT = Vector2.up;
		private static readonly Vector2 TOPRIGHT = Vector2.one;
		private static readonly Vector2 BOTTOMRIGHT = Vector2.right;
		private static readonly Vector2 MID = Vector2.one * 0.5f;
		private static readonly Vector2 MIDLEFT = Vector2.up * 0.5f;
		private static readonly Vector2 MIDRIGHT = new Vector2(1f, 0.5f);
		private static readonly Vector2 BOTTOMMID = Vector2.right * 0.5f;
		private static readonly Vector2 TOPMID = new Vector2(0.5f, 1f);

		public static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;

		private static ConfigEntry<Vector2> windowSize;
		private static ConfigEntry<Vector2> windowPos;
		private static ConfigEntry<float> scrollSens;

		private static readonly List<Toggle> matToggles = new List<Toggle>();
		private static readonly List<Toggle> meshToggles = new List<Toggle>();
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
			public float metal_default;
			public float spec_default;
		}

		public class DyeKit : MonoBehaviour
		{
			[SerializeField]
			public DyeKitItem[] items;
			[SerializeField]
			public bool[] meshes;
			public readonly List<Material> materials = new List<Material>();

			public void Start()
			{
				materials.Clear();
				var renderers = GetComponentsInChildren<Renderer>();
				
				foreach (var renderer in renderers)
				{
					materials.AddRange(renderer.materials);
				}

				if (meshes?.Length != renderers.Length)
				{
					meshes = new bool[renderers.Length];
					for (var i = 0; i < meshes.Length; ++i)
					{
						meshes[i] = renderers[i].enabled;
					}
				}
				else
				{
					for (var i = 0; i < meshes.Length; ++i)
					{
						renderers[i].enabled = meshes[i];
						renderers[i].GetComponent<ParticleSystem>()?.gameObject.SetActive(meshes[i]);
					}
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
						items[i].metal_default = mat.GetFloat("_Metallic");
						items[i].spec_default = mat.GetFloat("_Smoothness");
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
							mat.color = dye.color;
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
					if (toggleAll.isOn || (i < matToggles.Count && matToggles[i].isOn))
					{
						mat.SetColor("_BaseColor", dye.color);
						mat.SetFloat("_Metallic", dye.metal);
						mat.SetFloat("_Smoothness", dye.spec);
						items[i] = dye;
					}
					++i;
				}
			}

			public void UpdateMeshVisibility()
			{
				var i = 0;
				foreach (var renderer in GetComponentsInChildren<Renderer>())
				{
					if (i < meshToggles.Count)
					{
						renderer.enabled = meshToggles[i].isOn;
						renderer.GetComponent<ParticleSystem>()?.gameObject.SetActive(meshToggles[i].isOn);
					}
					if (i < meshes.Length) meshes[i] = renderer.enabled;
					++i;
				}
			}

			public void Reset()
			{
				var i = 0;
				foreach (var renderer in GetComponentsInChildren<Renderer>())
				{
					foreach (var mat in renderer.sharedMaterials)
					{
						if (toggleAll.isOn || (i < matToggles.Count && matToggles[i].isOn))
						{
							materials[i].SetColor("_BaseColor", mat.color);
							materials[i].SetFloat("_Metallic", items[i].metal_default);
							materials[i].SetFloat("_Smoothness", items[i].spec_default);
							items[i].color = mat.color;
							items[i].metal = items[i].metal_default;
							items[i].spec = items[i].spec_default;
						}
						++i;
					}
				}
			}
			
			public void Save(string prefix, string id)
			{
				if (items != null)
				{
					var i = 0;
					var colors = new Color[items.Length];
					var metals = new float[items.Length];
					var specs = new float[items.Length];

					foreach (var dye in items)
					{
						colors[i] = dye.color;
						metals[i] = dye.metal;
						specs[i] = dye.spec;
						++i;
					}

					ES2.Save(colors, $"{prefix}?tag=colors{id}");
					ES2.Save(metals, $"{prefix}?tag=metals{id}");
					ES2.Save(specs, $"{prefix}?tag=specs{id}");
					if (meshes != null) ES2.Save(meshes, $"{prefix}?tag=meshes{id}");
				}
			}

			public void Load(string prefix, string id)
			{
				var colors = ES2.LoadArray<Color>($"{prefix}?tag=colors{id}");
				var metals = ES2.LoadArray<float>($"{prefix}?tag=metals{id}");
				var specs = ES2.LoadArray<float>($"{prefix}?tag=specs{id}");
				if (ES2.Exists($"{prefix}?tag=meshes{id}"))
				{
					meshes = ES2.LoadArray<bool>($"{prefix}?tag=meshes{id}");
				}
				items = new DyeKitItem[colors.Length];

				for (var i = 0; i < items.Length; ++i)
				{
					items[i] = new DyeKitItem()
					{
						color = colors[i],
						metal = metals[i],
						spec = specs[i]
					};
				}

				if (isDebug.Value) context.Logger.LogInfo($"DyeKit loaded: {name} {id} with {items.Length} items");
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

		public static Text AddLabel(
			Text template,
			string label,
			Transform parent = null)
		{
			var text = Instantiate(template, parent ? parent : template.transform.parent);
			text.text = label;
			text.name = label;
			Destroy(text.GetComponentInChildren<LocalizationText>());
			return text;
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
			var click = __instance.GetComponentsInChildren<ColorPickClick>();

			click[0].Click.AddListener(() => __instance.OnStaurationClick(click[0]));
			click[1].Click.AddListener(() => __instance.OnHueClick(click[1]));

			windowSize.Value = new Vector2(
				Mathf.Max(windowSize.Value.x, 130f),
				Mathf.Max(windowSize.Value.y, 280f)
			);

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
					click[1].ClickPoint = click[1].ClickPoint = Vector3.up * __instance.GetHueY(slot.image.color);
					__instance.OnHueClick(click[1]);
					click[0].ClickPoint = new Vector3(sat.x, sat.y, 0f);
					__instance.OnStaurationClick(click[0]);
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

			var reset = AddButton(
				button,
				"Reset",
				() => {
					orgDyeKit?.Reset();
					dispDyeKit?.Reset();
				},
				button.transform
			);

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
				reset.GetComponent<RectTransform>(),
				__instance.transform,
				BOTTOMLEFT, BOTTOMRIGHT, BOTTOMMID,
				new Vector2(-6f, 20f),
				new Vector2(0f, 23f),
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
			var saturation = __instance.Saturation;
			var hue = __instance.Hue;

			windowSize.Value = new Vector2(
				Mathf.Max(windowSize.Value.x, 130f),
				Mathf.Max(windowSize.Value.y, 280f)
			);

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
					if (i < matToggles.Count)
					{
						matToggles[i].GetComponentInChildren<Text>().text = mat.name.Replace(" (Instance)", "");
						matToggles[i].gameObject.SetActive(true);
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
						matToggles.Add(toggle);
					}
					matToggles[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20f * (i + 1));
					++i;
				}

				var j = 0;
				foreach (var renderer in orgDyeKit.GetComponentsInChildren<Renderer>())
				{
					if (j < meshToggles.Count)
					{
						meshToggles[j].GetComponentInChildren<Text>().text = renderer.name.Replace(" (Instance)", "");
						meshToggles[j].isOn = orgDyeKit.meshes[j];
						meshToggles[j].gameObject.SetActive(true);
					}
					else
					{
						var toggle = AddToggle(
							toggleAll,
							renderer.name.Replace(" (Instance)", ""),
							(bool _) =>
							{
								orgDyeKit?.UpdateMeshVisibility();
								dispDyeKit?.UpdateMeshVisibility();
							},
							toggleAll.transform.parent,
							orgDyeKit.meshes[j]
						);
						meshToggles.Add(toggle);
					}
					meshToggles[j].GetComponent<RectTransform>().anchoredPosition = Vector2.down * 20f * (i + j + 2);
					++j;
				}
				content.sizeDelta = Vector2.up * 20f * (i + j + 2);

				for (; j < meshToggles.Count; ++j)
				{
					meshToggles[j].gameObject.SetActive(false);
				}

				for (; i < matToggles.Count; ++i)
				{
					matToggles[i].gameObject.SetActive(false);
				}
			}
			else
			{
				content.sizeDelta = Vector2.up;
				foreach (var toggle in matToggles)
				{
					toggle.gameObject.SetActive(false);
				}

				foreach (var toggle in meshToggles)
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
			windowSize = Config.Bind("UI", "Color Picker Size", new Vector2(130f, 280f), "Control the window size of the Color Picker");
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
				if (!toggleTemplate) {
					var toggles = __instance.transform.GetComponentsInChildren<Toggle>();
					toggleTemplate = toggles[toggles.Length - 1];
				}
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
					if (dyekit.GetComponent<Item>()?.slotType == __instance.slotType)
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
			public static void Prefix(UIColorPick __instance, Color C, ref string place)
			{
				if (!modEnabled.Value) return;

				if (place != "EquipmentSlot")
				{
					orgDyeKit = null;
					dispDyeKit = null;
				}
				
				if (place == "Hair Color Picker")
				{
					place = "Dye Kit Hair Color Picker";
					var cc = Global.code.uiMakeup.curCustomization;
					orgDyeKit = cc.hair ? (cc.hair.TryGetComponent(out DyeKit dye) ? dye : cc.hair.gameObject.AddComponent<DyeKit>()) : null;
					orgDyeKit?.Start();
				}

				if (place == "Dye Kit Wings Color Picker")
				{
					var cc = Global.code.uiCustomization.curCharacterCustomization;
					orgDyeKit = cc.wing ? (cc.wing.TryGetComponent(out DyeKit dye) ? dye : cc.wing.gameObject.AddComponent<DyeKit>()) : null;
					orgDyeKit?.Start();
				}

				if (place == "Dye Kit Horns Color Picker")
				{
					var cc = Global.code.uiCustomization.curCharacterCustomization;
					orgDyeKit = cc.horn ? (cc.horn.TryGetComponent(out DyeKit dye) ? dye : cc.horn.gameObject.AddComponent<DyeKit>()) : null;
					orgDyeKit?.Start();
				}

				currentPlace = place;

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

				if (currentPlace == "Dye Kit Hair Color Picker")
				{
					var cc = Global.code.uiMakeup.curCustomization;
					cc.hair?.TryGetComponent(out orgDyeKit);
					cc.hairColor = color * emissionSlider.value;
				}

				if (currentPlace == "Dye Kit Wings Color Picker")
				{
					var cc = Global.code.uiCustomization.curCharacterCustomization;
					cc.wing?.TryGetComponent(out orgDyeKit);
				}

				if (currentPlace == "Dye Kit Horns Color Picker")
				{
					var cc = Global.code.uiCustomization.curCharacterCustomization;
					cc.horn?.TryGetComponent(out orgDyeKit);
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


		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonWings))]
		public static class UICustomization_ButtonWings_Patch
		{
			public static void Prefix(UICustomization __instance)
			{
				if (!modEnabled.Value || __instance.panelWings.transform.Find("Dye Kit Wings Color Picker")) return;

				var title = __instance.panelWings.transform.Find("title").GetComponent<RectTransform>();
				var colorPicker = AddButton(
					__instance.panelSkin.transform.Find("Skin Color Picker").GetComponent<Button>(),
					"Color Picker",
					() => Global.code.uiColorPick.Open(Color.white, "Dye Kit Wings Color Picker"),
					title.transform.parent
				);
				colorPicker.name = "Dye Kit Wings Color Picker";
				var rect = colorPicker.GetComponent<RectTransform>();

				SetPosition(
					rect,
					title.transform.parent,
					MIDRIGHT, MIDRIGHT, MIDRIGHT,
					rect.sizeDelta,
					title.anchoredPosition,
					rect.localScale
				);
			}
		}

		[HarmonyPatch(typeof(UICustomization), nameof(UICustomization.ButtonHorns))]
		public static class UICustomization_ButtonHorns_Patch
		{
			public static void Prefix(UICustomization __instance)
			{
				if (!modEnabled.Value || __instance.panelHorns.transform.Find("Dye Kit Horns Color Picker")) return;

				var title = __instance.panelHorns.transform.Find("title").GetComponent<RectTransform>();
				var colorPicker = AddButton(
					__instance.panelSkin.transform.Find("Skin Color Picker").GetComponent<Button>(),
					"Color Picker",
					() => Global.code.uiColorPick.Open(Color.white, "Dye Kit Horns Color Picker"),
					title.transform.parent
				);
				colorPicker.name = "Dye Kit Horns Color Picker";
				var rect = colorPicker.GetComponent<RectTransform>();

				SetPosition(
					rect,
					title.transform.parent,
					MIDRIGHT, MIDRIGHT, MIDRIGHT,
					rect.sizeDelta,
					title.anchoredPosition,
					rect.localScale
				);
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
					item.GetComponent<DyeKit>()?.Save($"{__instance.foldername}/DyeKit.txt", item.GetInstanceID().ToString());
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
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

				try
				{
					if (ES2.Exists($"{__instance.foldername}/DyeKit.txt?tag=colors{id}"))
					{
						__result.gameObject.AddComponent<DyeKit>()?.Load($"{__instance.foldername}/DyeKit.txt", id.ToString());
					}
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
					var prefix = $"{__instance.foldername}/DyeKit/{customization.name}.txt";
					customization.horn?.GetComponent<DyeKit>()?.Save(prefix, "Horns");
					customization.wing?.GetComponent<DyeKit>()?.Save(prefix, "Wings");
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
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
				try
				{
					var prefix = $"{__instance.foldername}/DyeKit/{gen.name}.txt";
					if (ES2.Exists(prefix))
					{
						gen.horn?.gameObject.AddComponent<DyeKit>().Load(prefix, "Horns");
						gen.wing?.gameObject.AddComponent<DyeKit>().Load(prefix, "Wings");
					}
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
