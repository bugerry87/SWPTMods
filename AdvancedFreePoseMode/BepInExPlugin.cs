using System;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using RuntimeGizmos;

namespace AdvancedFreePoseMode
{
	[BepInPlugin("bugerry.AdvancedFreePoseMode", "Advanced Free Pose Mode", "1.4.3")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static BepInExPlugin context;

		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<bool> keepPose;
		public static ConfigEntry<bool> enableToggleTools;
		public static ConfigEntry<bool> useBetterMove;
		public static ConfigEntry<int> maxModels;
		public static ConfigEntry<int> nexusID;
		public static ConfigEntry<int> updateMode;
		public static ConfigEntry<int> mouseButton;
		public static ConfigEntry<float> horizontalSensitivity;
		public static ConfigEntry<float> verticalSensitivity;
		public static ConfigEntry<float> rotationSensitivity;
		public static ConfigEntry<float> animationSpeed;
		public static ConfigEntry<Vector2> placementTools;
		public static ConfigEntry<Vector2> toggleToolsPos;
		public static ConfigEntry<Vector2> toggleGizmoPos;
		public static ConfigEntry<Vector2> resetButtonPos;
		public static ConfigEntry<float> gizmoSize;
		public static ConfigEntry<float> bubbleSize;
		public static ConfigEntry<int> numFilters;
		public static ConfigEntry<bool> allTransformers;
		public static ConfigEntry<bool> extractPoses;
		public static ConfigEntry<bool> enableForAll;
		public static ConfigEntry<bool> saveAllBones;
		public static readonly List<ConfigEntry<string>> filters = new List<ConfigEntry<string>>();

		public Dictionary<MoveObject, Vector3> lastPositions = new Dictionary<MoveObject, Vector3>();
		public readonly HashSet<Transform> backup = new HashSet<Transform>();
		public int toggleGizmos = 0;
		public int toggleGizmoType = (int)TransformType.Rotate;
		public bool switch_pose = true;
		public GameObject toggleTool = null;
		public Button toggleButton = null;

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 108, "Nexus mod ID for updates");
			keepPose = Config.Bind("General", "Keep Pose", true, "Keep pose after Free Pose Mode (Stay That Way)");
			mouseButton = Config.Bind("General", "Mouse Button", 0, "Mouse Button for camera control [0,1,2]");
			enableToggleTools = Config.Bind("General", "Enable Toggle Tools", true, "Enable the toggle tools to mount and dismount gears");
			useBetterMove = Config.Bind("General", "Use Better Translation", true, "Improves the vertical and horizontal translation");
			updateMode = Config.Bind("General", "Update Mode", 0, "0 = Auto, 1 = Post Frame, 2 = On Physics Update");
			horizontalSensitivity = Config.Bind("Sensitivity", "Horizontal", 20f, "The sensitivity of the object placement horizontal");
			verticalSensitivity = Config.Bind("Sensitivity", "Vertical", 20f, "The sensitivity of the object placement vertical");
			rotationSensitivity = Config.Bind("Sensitivity", "Rotation", 5000f, "The sensitivity of the object placement rotation");
			animationSpeed = Config.Bind("Free Pose Mode", "Animation Speed", 1f, "The sensitivity of the object placement rotation");
			placementTools = Config.Bind("Free Pose Mode", "Placement Tool Position", new Vector2(171f, -51f), "Position of the placement tool bar");
			toggleToolsPos = Config.Bind("Free Pose Mode", "Toogle Tool Position", new Vector2(164f, -140f), "Position of the cloth toogle tool bar");
			toggleGizmoPos = Config.Bind("Free Pose Mode", "Gizmo Button Position", new Vector2(0f, -80f), "Position of the toggle button for gizmo types");
			maxModels = Config.Bind("Free Pose Mode", "Number of Models", 8, "Number of models in Free Pose Mode");
			saveAllBones = Config.Bind("Free Pose Mode", "Save All Bones", true, "Pose presets will save and load all bones");
			gizmoSize = Config.Bind("Gizmos", "Gizmo Size", 0.05f, "The size of the rotation axis");
			bubbleSize = Config.Bind("Gizmos", "Bubble Size", 0.05f, "The size of the selectable bubbles");
			allTransformers = Config.Bind("Filters", "All Transformers", false, "Whether to consider all Transformers or (default) Bones only");
			extractPoses = Config.Bind("Furniture", "Extract Poses", true, "Extract all poses from furniture to Free Pose Mode");
			enableForAll = Config.Bind("Furniture", "Enable For All", true, "Allow companions to use all furniture");
			numFilters = Config.Bind("Filters", "Number of Filters", 3, "Increase the number to add more filters");
			filters.Add(Config.Bind("Filters", "Filter1", "hip|head|Bend|Hand|Foot|Shin|abdomenLower|abdomenUpper|Collar|neckLower|chestLower|lowerJaw"));
			filters.Add(Config.Bind("Filters", "Filter2", "Hand|Thumb|Index|rMid|lMid|Ring|Pinky|Toe"));
			filters.Add(Config.Bind("Filters", "Filter3", "Labium"));
			Config.SettingChanged += UpdateFilters;
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		private void UpdateFilters(object source, SettingChangedEventArgs args)
		{
			if (args.ChangedSetting != numFilters) return;
			for (var i = filters.Count; i < numFilters.Value; ++i)
			{
				filters.Add(Config.Bind("Filters", string.Format("Filter{0}", i + 1), ""));
			}
		}

		private void Update()
		{
			if (!modEnabled.Value) return;
			if (!Global.code?.uiFreePose) return;
			if (!Global.code.uiFreePose.isActiveAndEnabled) return;
			if (!Global.code.uiFreePose.selectedCharacter) return;
			if (Global.code.uiFreePose.selectedCharacter.TryGetComponent(out CharacterCustomization cc))
			{
				AnimationCurve ac = new AnimationCurve();
				cc.anim.speed = animationSpeed.Value;
				if (Input.GetKeyDown(KeyCode.Alpha1))
				{
					if (cc.weaponIndex == 2 && cc.shield)
					{
						cc.shield.gameObject.SetActive(!cc.shield.gameObject.activeSelf);
					}
					else if (cc.weaponIndex == 1 && cc.weapon && cc.weapon.gameObject.activeSelf)
					{
						cc.weapon.gameObject.SetActive(false);
						cc.weapon2.gameObject.SetActive(false);
						cc.shield?.gameObject.SetActive(false);
					}
					else if (cc.weapon && cc.weaponIndex == 0)
					{
						cc.weapon.gameObject.SetActive(true);
						cc.weaponIndex = 1;
						cc.canDualWielding = cc.dualWielding;
						cc.Draw();
					}
					else
					{
						cc.weapon?.gameObject.SetActive(true);
						cc.weapon2?.gameObject.SetActive(true);
						cc.Holster(cc.weapon);
						cc.Holster(cc.weapon2);
						cc.HolsterShield();
						cc.weaponIndex = 0;
					}
				}
				else if (Input.GetKeyDown(KeyCode.Alpha2))
				{
					if (cc.weaponIndex == 1 && cc.shield)
					{
						cc.shield.gameObject.SetActive(!cc.shield.gameObject.activeSelf);
					}
					else if (cc.weaponIndex == 2 && cc.weapon2 && cc.weapon2.gameObject.activeSelf)
					{
						cc.weapon2.gameObject.SetActive(false);
						cc.shield?.gameObject.SetActive(false);
					}
					else if (cc.weapon2 && cc.weaponIndex == 0)
					{
						cc.weapon2?.gameObject.SetActive(true);
						cc.weaponIndex = 2;
						cc.Draw();
					}
					else
					{
						cc.weapon?.gameObject.SetActive(true);
						cc.weapon2?.gameObject.SetActive(true);
						cc.Holster(cc.weapon);
						cc.Holster(cc.weapon2);
						cc.HolsterShield();
						cc.weaponIndex = 0;
					}
				}
			}
		}

		public void ToggleGizmos(CharacterCustomization cc)
		{
			toggleGizmos %= filters.Count;
			var bones = new List<Transform>(
				allTransformers.Value ? cc.GetComponentsInChildren<Transform>() : cc.body.bones).FindAll(
				t => Regex.IsMatch(t.name, filters[toggleGizmos].Value)
			);

			cc.bonesNeedRender.Clear();
			cc.bonesNeedRender.AddRange(bones);

			if (!allTransformers.Value)
			{
				foreach (var light in cc.GetComponentsInChildren<Light>())
				{
					cc.bonesNeedRender.Add(light.transform);
				}
			}
		}

		public void ToggleGizmoType()
		{
			if (toggleButton && TransformGizmo.transformGizmo_)
			{
				toggleGizmoType %= 4;
				var gizmoType = (TransformType)toggleGizmoType;
				TransformGizmo.transformGizmo_.transformType = gizmoType;
				toggleButton.GetComponentInChildren<Text>().text = gizmoType.ToString();
			}
		}

		public static void DestroyToys(CharacterCustomization cc)
		{
			foreach (var toy in cc.rh.GetComponentsInChildren<SexToy>())
			{
				Destroy(toy.gameObject);
			}

			foreach (var toy in cc.lh.GetComponentsInChildren<SexToy>())
			{
				Destroy(toy.gameObject);
			}

			foreach (var toy in cc.anal.GetComponentsInChildren<SexToy>())
			{
				Destroy(toy.gameObject);
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.Open))]
		public static class UIFreePose_Open_Patch
		{
			public static void Postfix()
			{
				if (!modEnabled.Value) return;
				Global.code.freeCameraCollider.SetActive(false);
				context.toggleGizmos = 0;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.Refresh))]
		public static class UIFreePose_Refresh_Patch
		{
			public static void Postfix(UIFreePose __instance)
			{
				if (!modEnabled.Value) return;

				if (enableToggleTools.Value)
				{
					var group = __instance.transform.Find("Left").Find("group pose");
					group.Find("tools bg").GetComponent<RectTransform>().anchoredPosition = placementTools.Value;
					if (context.toggleTool == null)
					{
						context.toggleTool = Instantiate(Global.code.uiPose.panelTakeOffClothes, group);
					}
					else
					{
						context.toggleTool.transform.SetParent(group.transform);
					}
					context.toggleTool.GetComponent<RectTransform>().anchoredPosition = toggleToolsPos.Value;
					context.toggleTool.SetActive(true);
				}

				var temp = __instance.transform.Find("FreePose").GetComponent<Button>();
				if (context.toggleButton == null)
				{
					context.toggleButton = Instantiate(temp, temp.transform.parent);
					context.toggleButton.GetComponent<RectTransform>().anchoredPosition += toggleGizmoPos.Value;
					Destroy(context.toggleButton.GetComponentInChildren<LocalizationText>());
					context.toggleButton.onClick.RemoveAllListeners();
					context.toggleButton.onClick.AddListener(() => context.toggleGizmoType += 1);
					context.toggleButton.onClick.AddListener(__instance.LetRuntimeTransformRun);
					temp.onClick.RemoveAllListeners();
					temp.onClick.AddListener(() => context.toggleGizmos += 1);
					temp.onClick.AddListener(__instance.LetRuntimeTransformRun);
				}
				context.toggleButton.transform.SetParent(temp.transform.parent);
				context.toggleButton.gameObject.SetActive(false);

				for (int j = 4; j < maxModels.Value; j++)
				{
					Transform transform = Instantiate(__instance.companionIconPrefab);
					transform.SetParent(__instance.companionIconHolder);
					transform.localScale = Vector3.one;
					if (j < __instance.characters.items.Count)
					{
						Transform transform2 = __instance.characters.items[j];
						transform.GetComponent<FreeposeCompanionIcon>().Initiate(transform2.GetComponent<CharacterCustomization>());
					}
					else
					{
						transform.GetComponent<FreeposeCompanionIcon>().Initiate(null);
					}
				}
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.PoseButtonClicked))]
		public static class UIFreePose_PoseButtonClicked_Patch
		{
			public static void Prefix(UIFreePose __instance, Pose code)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				context.switch_pose = true;
			}

			public static void Postfix(UIFreePose __instance, Pose code)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				var cc = __instance.selectedCharacter.GetComponent<CharacterCustomization>();
				DestroyToys(cc);

				if (code.sexToy)
				{
					var toy = Instantiate(code.sexToy);
					toy.gameObject.SetActive(true);
					if (toy.GetComponent<SexToy>()?.leftHand == true)
					{
						toy.SetParent(cc.lh);
					}
					else if (toy.GetComponent<SexToy>()?.anal == true)
					{
						toy.SetParent(cc.anal);
					}
					else
					{
						toy.SetParent(cc.rh);
					}
					toy.localPosition = Vector3.zero;
					toy.localEulerAngles = Vector3.zero;
					toy.localScale = Vector3.one;
				}
				if (code.sexToy2)
				{
					var toy = Instantiate(code.sexToy2);
					toy.gameObject.SetActive(true);
					if (code.sexToy2.GetComponent<SexToy>()?.leftHand == true)
					{
						toy.SetParent(cc.lh);
					}
					else if (toy.GetComponent<SexToy>()?.anal == true)
					{
						toy.SetParent(cc.anal);
					}
					else
					{
						toy.SetParent(cc.rh);
					}
					toy.localPosition = Vector3.zero;
					toy.localEulerAngles = Vector3.zero;
					toy.localScale = Vector3.one;
				}
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.LetRuntimeTransformRun))]
		public static class UIFreePose_LetRuntimeTransformRun_Patch
		{
			public static void Prefix(UIFreePose __instance)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				if (TransformGizmo.transformGizmo_)
				{
					TransformGizmo.transformGizmo_.oldOne = null;
					TransformGizmo.transformGizmo_.handleLength = gizmoSize.Value;
				}
				var cc = __instance.selectedCharacter.GetComponent<CharacterCustomization>();
				context.toggleButton?.gameObject.SetActive(true);
				context.ToggleGizmos(cc);
				context.ToggleGizmoType();
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.LetRuntimeTransformSleep))]
		public static class UIFreePose_LetRuntimeTransformSleep_Patch
		{
			public static void Postfix(UIFreePose __instance)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				if (__instance.selectedCharacter.TryGetComponent(out Animator anim)) anim.enabled = context.switch_pose;
				context.toggleButton?.gameObject.SetActive(false);
				context.switch_pose = false;
			}
		}

		private static void DoUpdate(TransformGizmo __instance)
		{
			if (__instance.runTransformGizmo)
			{
				foreach (var keyValuePair in __instance.bonesAndTemp)
				{
					if (__instance.selectNow != null && __instance.selectNow == keyValuePair.Key)
					{
						keyValuePair.Value.position = keyValuePair.Key.position;
						keyValuePair.Value.rotation = keyValuePair.Key.rotation;
						keyValuePair.Value.localScale = keyValuePair.Key.localScale / bubbleSize.Value;
					}
					else
					{
						keyValuePair.Key.position = keyValuePair.Value.position;
						keyValuePair.Key.rotation = keyValuePair.Value.rotation;
						keyValuePair.Key.localScale = keyValuePair.Value.localScale * bubbleSize.Value;
					}
				}
			}
		}

		[HarmonyPatch(typeof(TransformGizmo), "LateUpdate")]
		public static class TransformGizmo_LateUpdate_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(TransformGizmo).GetMethod("LateUpdate");
			}

			public static void Postfix(TransformGizmo __instance)
			{
				if (!modEnabled.Value || updateMode.Value != 0) return;
				DoUpdate(__instance);
			}
		}

		[HarmonyPatch(typeof(TransformGizmo), "FixedUpdate")]
		public static class TransformGizmo_FixedUpdate_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(TransformGizmo).GetMethod("FixedUpdate");
			}

			public static bool Prefix(TransformGizmo __instance)
			{
				if (!modEnabled.Value) return true;
				if (updateMode.Value == 1) DoUpdate(__instance);
				return false;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.AddCharacter))]
		public static class UIFreePose_AddCharacter_Patch
		{
			public static void Postfix(Transform character, UIFreePose __instance)
			{
				if (!modEnabled.Value || !keepPose.Value) return;
				__instance.selectedCharacter = character;
				if (character.TryGetComponent(out ThirdPersonCharacter tpc)) tpc.enabled = false;
				if (character.TryGetComponent(out Animator anim)) anim.enabled = false;
				if (character.TryGetComponent(out Companion c))
				{
					c.enabled = false;
					c.CancelInvoke("CS");
				}
			}
		}

		[HarmonyPatch(typeof(UIFreePose), nameof(UIFreePose.Close))]
		public static class UIFreePose_Close_Patch
		{
			public static void Prefix(UIFreePose __instance)
			{
				if (!modEnabled.Value) return;
				if (Player.code.TryGetComponent(out ThirdPersonCharacter tcp)) tcp.enabled = true;

				if (keepPose.Value && Global.code.curlocation.locationType == LocationType.home)
				{
					foreach (var t in __instance.characters.items)
					{
						if (!t) continue;
						if (t.TryGetComponent(out Animator anim) && anim.enabled) continue;
						context.backup.Add(t);
					}
					__instance.characters.ClearItems();
					__instance.characters.AddItem(Player.code.transform);
				}
				else
				{
					foreach (Transform t in __instance.characters.items)
					{
						if (!t) continue;
						if (t.TryGetComponent(out ThirdPersonCharacter tpc)) tpc.enabled = true;
						if (t.TryGetComponent(out Animator anim)) anim.enabled = true;
						if (t.TryGetComponent(out CharacterCustomization cc)) DestroyToys(cc);
						if (t.TryGetComponent(out Companion c))
						{
							c.enabled = true;
							if (!c.IsInvoking("CS"))
							{
								c.InvokeRepeating("CS", 1f, 1f);
							}
						}
					}
				}
			}

			public static void Postfix()
			{
				if (!modEnabled.Value) return;
				foreach (var t in context.backup)
				{
					if (t && t.TryGetComponent(out Animator anim)) anim.enabled = false;
				}
				Player.code.GetComponent<Animator>().enabled = true;
			}
		}

		[HarmonyPatch(typeof(UICombatParty), nameof(UICombatParty.Open))]
		public static class UICombatParty_Open_Patch
		{
			public static void Prefix()
			{
				if (!modEnabled.Value || !keepPose.Value) return;

				foreach (var transform in context.backup)
				{
					if (!transform) continue;
					CharacterCustomization component = transform.GetComponent<CharacterCustomization>();
					component.interactingObject = null;
					component.anim.runtimeAnimatorController = RM.code.combatController;
					component.anim.avatar = RM.code.flatFeetAvatar;
					component.anim.enabled = true;
					if (transform.TryGetComponent(out Rigidbody rig))
					{
						rig.isKinematic = false;
						rig.constraints = RigidbodyConstraints.FreezeRotation;
					}
					if (transform.TryGetComponent(out NavMeshAgent nav)) nav.enabled = true;
					if (transform.TryGetComponent(out ThirdPersonCharacter tcp)) tcp.enabled = true;
					if (transform.TryGetComponent(out Companion c))
					{
						c.enabled = true;
						if (!c.IsInvoking("CS"))
						{
							c.InvokeRepeating("CS", 1f, 1f);
						}
					}
					component.RefreshClothesVisibility();
					component.characterLightGroup.transform.localEulerAngles = Vector3.zero;
				}
				context.backup.Clear();
			}
		}

		[HarmonyPatch(typeof(MoveObject), "Update")]
		public static class MoveObject_Update_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(MoveObject).GetMethod("Update");
			}

			public static bool Prefix(MoveObject __instance)
			{
				if (!modEnabled.Value || !useBetterMove.Value) return true;
				Vector3 lastPos;
				Vector3 mousePosition = Input.mousePosition;
				if (__instance.canGo && Global.code.uiFreePose.selectedCharacter && context.lastPositions.TryGetValue(__instance, out lastPos))
				{
					int width = Screen.width;
					int height = Screen.height;
					__instance.deltaX = (float)(mousePosition.x - lastPos.x) / width;
					__instance.deltaY = (float)(mousePosition.y - lastPos.y) / height;

					var component = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
					__instance.mover = Global.code.uiFreePose.selectedCharacter;
					if (__instance.move)
					{
						Vector3 normalized = Vector3.Scale(Global.code.freeCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
						Vector3 a = __instance.deltaY * normalized + __instance.deltaX * Global.code.freeCamera.transform.right;
						__instance.mover.transform.position += a * horizontalSensitivity.Value * Time.deltaTime;
					}
					else if (__instance.moveY)
					{
						__instance.mover.transform.position += new Vector3(0f, __instance.deltaY * Time.deltaTime * verticalSensitivity.Value, 0f);
					}
					else if (__instance.rotate)
					{
						__instance.mover.transform.eulerAngles += new Vector3(0f, -__instance.deltaX * Time.deltaTime * rotationSensitivity.Value, 0f);
					}
					else if (__instance.rotatelighting)
					{
						component.characterLightGroup.transform.eulerAngles += new Vector3(0f, -__instance.deltaX * Time.deltaTime * rotationSensitivity.Value, 0f);
					}
					MoveObject.CursorPoint cursorPoint;
					MoveObject.GetCursorPos(out cursorPoint);
					if (cursorPoint.X <= 0)
					{
						MoveObject.SetCursorPos(width - 2, cursorPoint.Y);
						mousePosition.x = width - 2;
					}
					if (cursorPoint.X > width - 2)
					{
						MoveObject.SetCursorPos(1, cursorPoint.Y);
						mousePosition.x = 1f;
					}
					if (cursorPoint.Y <= 0)
					{
						MoveObject.SetCursorPos(cursorPoint.X, height - 2);
						mousePosition.y = 1f;
					}
					if (cursorPoint.Y > height - 2)
					{
						MoveObject.SetCursorPos(cursorPoint.X, 1);
						mousePosition.y = height - 2;
					}
				}
				context.lastPositions[__instance] = mousePosition;
				return false;
			}
		}

		[HarmonyPatch(typeof(ThirdPersonCharacter), "Snap")]
		public static class ThirdPersonCharacter_Snap_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(ThirdPersonCharacter).GetMethod("Snap");
			}

			public static bool Prefix(ThirdPersonCharacter __instance)
			{
				if (!modEnabled.Value || __instance.enabled) return true;
				__instance.m_IsGrounded = true;
				return true;
			}
		}

		[HarmonyPatch(typeof(ThirdPersonCharacter), "CheckGroundStatus")]
		public static class ThirdPersonCharacter_CheckGroundStatus_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(ThirdPersonCharacter).GetMethod("CheckGroundStatus");
			}

			public static bool Prefix(ThirdPersonCharacter __instance)
			{
				if (!modEnabled.Value || __instance.isActiveAndEnabled) return true;
				__instance.m_IsGrounded = true;
				return true;
			}
		}

		[HarmonyPatch(typeof(UIPose), nameof(UIPose.ButtonTakeoffBra))]
		public static class UIPose_ButtonTakeoffBra_Patch
		{
			public static bool Prefix(UIPose __instance)
			{
				if (!modEnabled.Value || !enableToggleTools.Value) return true;

				var cc = Global.code.uiFreePose.selectedCharacter ?
					Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>() :
					__instance.curCustomization;
				if (cc.armor && cc.armor.gameObject.activeSelf)
				{
					cc.armor.gameObject.SetActive(false);
					if (cc.bra)
					{
						cc.bra.GetComponent<Item>()?.InstantiateModel(cc);
						cc.bra.gameObject.SetActive(true);
					}
				}
				else if (cc.bra && cc.bra.gameObject.activeSelf)
				{
					cc.bra.gameObject.SetActive(false);
				}
				else if (cc.armor)
				{
					cc.armor.GetComponent<Item>()?.InstantiateModel(cc);
					cc.armor.gameObject.SetActive(true);
				}
				else if (cc.bra)
				{
					cc.bra.GetComponent<Item>()?.InstantiateModel(cc);
					cc.bra.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), nameof(UIPose.ButtonTakeoffPanties))]
		public static class UIPose_ButtonTakeoffPanties_Patch
		{
			public static bool Prefix(UIPose __instance)
			{
				if (!modEnabled.Value || !enableToggleTools.Value) return true;

				var cc = Global.code.uiFreePose.selectedCharacter ?
					Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>() :
					__instance.curCustomization;
				if (cc.panties && cc.panties.gameObject.activeSelf)
				{
					cc.panties.gameObject.SetActive(false);
				}
				else if (cc.suspenders && cc.suspenders.gameObject.activeSelf)
				{
					cc.suspenders.gameObject.SetActive(false);
				}
				else
				{
					cc.suspenders?.GetComponent<Item>()?.InstantiateModel(cc);
					cc.panties?.GetComponent<Item>()?.InstantiateModel(cc);
					cc.suspenders?.gameObject.SetActive(true);
					cc.panties?.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), nameof(UIPose.ButtonTakeoffStockings))]
		public static class UIPose_ButtonTakeoffStockings_Patch
		{
			public static bool Prefix(UIPose __instance)
			{
				if (!modEnabled.Value || !enableToggleTools.Value) return true;

				var cc = Global.code.uiFreePose.selectedCharacter ?
					Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>() :
					__instance.curCustomization;
				if (cc.leggings && cc.leggings.gameObject.activeSelf)
				{
					cc.leggings.gameObject.SetActive(false);
				}
				if (cc.stockings && cc.stockings.gameObject.activeSelf)
				{
					cc.stockings.gameObject.SetActive(false);
				}
				else
				{
					cc.leggings?.GetComponent<Item>()?.InstantiateModel(cc);
					cc.stockings?.GetComponent<Item>()?.InstantiateModel(cc);
					cc.leggings?.gameObject.SetActive(true);
					cc.stockings?.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), nameof(UIPose.ButtonTakeoffHeels))]
		public static class UIPose_ButtonTakeoffHeels_Patch
		{
			public static bool Prefix(UIPose __instance)
			{
				if (!modEnabled.Value || !enableToggleTools.Value) return true;

				var cc = Global.code.uiFreePose.selectedCharacter ?
					Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>() :
					__instance.curCustomization;
				if (cc.shoes && cc.shoes.gameObject.activeSelf)
				{
					cc.shoes.gameObject.SetActive(false);
					if (cc.heels)
					{
						cc.heels.GetComponent<Item>()?.InstantiateModel(cc);
						cc.heels.gameObject.SetActive(true);
					}
				}
				else if (cc.heels && cc.heels.gameObject.activeSelf)
				{
					cc.heels.gameObject.SetActive(false);
				}
				else if (cc.shoes)
				{
					cc.shoes.GetComponent<Item>()?.InstantiateModel(cc);
					cc.shoes.gameObject.SetActive(true);
				}
				else if (cc.heels)
				{
					cc.heels.GetComponent<Item>()?.InstantiateModel(cc);
					cc.heels.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(FreelookCamera), nameof(FreelookCamera.FixedUpdate))]
		public static class FreelookCamera_FixedUpdate_Patch
		{
			public static bool Prefix(FreelookCamera __instance)
			{
				if (!modEnabled.Value) return true;

				float num = __instance.Speed;
				if (__instance.EnableBoostSpeed && Input.GetKey(__instance.BoostKey))
				{
					num = __instance.BoostSpeed;
				}
				float num2 = 0f;
				if (Input.GetKey(__instance.UpKey))
				{
					num2 = 1f;
				}
				if (Input.GetKey(__instance.DownKey))
				{
					num2 = -1f;
				}
				if (Global.code.uiFreePose.CheckMoveCamera())
				{
					return false;
				}
				num *= Time.deltaTime;
				num2 *= num;
				float d = Input.GetAxis("Vertical") * num;
				float d2 = Input.GetAxis("Horizontal") * num;
				Vector3 forward = __instance.transform.forward;
				Vector3 right = __instance.transform.right;
				__instance.transform.position += forward * d + right * d2 + Vector3.up * num2;
				if (!Input.GetMouseButton(mouseButton.Value))
				{
					return false;
				}
				if (Global.code.isAdjustPosture)
				{
					return false;
				}
				if (__instance.transformGizmo && __instance.transformGizmo.moveOrRotateNow)
				{
					return false;
				}
				__instance.rotationX += Input.GetAxis("Mouse X") * __instance.MouseSensitivity;
				__instance.rotationY += Input.GetAxis("Mouse Y") * __instance.MouseSensitivity;
				__instance.rotationY = Mathf.Clamp(__instance.rotationY, -89f, 89f);
				Quaternion rhs = Quaternion.AngleAxis(__instance.rotationX, Vector3.up);
				Quaternion rhs2 = Quaternion.AngleAxis(__instance.rotationY, -Vector3.right);
				__instance.transform.rotation = __instance.originalRotation * rhs * rhs2;
				return false;
			}
		}

		[HarmonyPatch(typeof(Furniture), "DoInteract")]
		public static class Furniture_DoInteracts_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Furniture).GetMethod("DoInteract");
			}

			public static void Prefix(Furniture __instance, CharacterCustomization customization)
			{
				if (!modEnabled.Value) return;
				__instance.dontRandomPose = !!__instance.GetComponent<Mirror>();
			}
		}

		[HarmonyPatch(typeof(Mainframe), nameof(Mainframe.LoadFurnitures))]
		public static class Mainframe_LoadFurnitures_Patch
		{
			public static void Postfix()
			{
				if (!modEnabled.Value) return;
				try
				{
					foreach (var item in RM.code.allBuildings.items)
					{
						if (!item || item.GetComponent<Mirror>())
						{
							continue;
						}
						else if (item.TryGetComponent(out Furniture f) && f.poses?.items.Count > 0)
						{
							f.dontRandomPose = false;
							f.notInteractableByCompanion |= enableForAll.Value;
							if (!extractPoses.Value) continue;
							foreach (var p in f.poses.items)
							{
								if (p && p.TryGetComponent(out Pose pose))
								{
									if (pose.categoryName == null || pose.categoryName.Length == 0) pose.categoryName = "Furniture";
									RM.code.allFreePoses.AddItem(pose.transform);
								}
							}
						}
					}
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}
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
				if (!modEnabled.Value || !saveAllBones.Value) return;
				__instance.bonesNeedSave = new List<Transform>(__instance.body.bones);
			}
		}
	}
}
