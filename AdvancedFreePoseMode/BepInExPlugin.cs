using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RuntimeGizmos;

namespace AdvancedFreePoseMode
{
	[BepInPlugin("bugerry.AdvancedFreePoseMode", "Advanced Free Pose Mode", "1.0.1")]
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
		public static ConfigEntry<float> horizontalSensitivity;
		public static ConfigEntry<float> verticalSensitivity;
		public static ConfigEntry<float> rotationSensitivity;
		public static ConfigEntry<Vector2> placementTools;
		public static ConfigEntry<Vector2> toggleTools;
		public static ConfigEntry<float> gizmoSize;
		public static ConfigEntry<float> bubbleSize;
		public static ConfigEntry<int> numFilters;
		public static readonly List<ConfigEntry<string>> filters = new List<ConfigEntry<string>>();

		public Dictionary<MoveObject, Vector3> lastPositions = new Dictionary<MoveObject, Vector3>();
		public readonly HashSet<Transform> backup = new HashSet<Transform>();
		public int toggleGizmos = 0;
		public bool switch_pose = true;

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 108, "Nexus mod ID for updates");
			keepPose = Config.Bind("General", "Keep Pose", true, "Keep pose after Free Pose Mode (Stay That Way)");
			enableToggleTools = Config.Bind("General", "Enable Toggle Tools", true, "Enable the toggle tools to mount and dismount gears");
			useBetterMove = Config.Bind("General", "Use Better Translation", true, "Improves the vertical and horizontal translation");
			horizontalSensitivity = Config.Bind("Sensitivity", "Horizontal", 1f, "The sensitivity of the object placement horizontal");
			verticalSensitivity = Config.Bind("Sensitivity", "Vertical", 20f, "The sensitivity of the object placement vertical");
			rotationSensitivity = Config.Bind("Sensitivity", "Rotation", 20f, "The sensitivity of the object placement rotation");
			placementTools = Config.Bind("Free Pose Mode", "Placement Tool Position", new Vector2(171f, -51f), "Position of the placement tool bar");
			toggleTools = Config.Bind("Free Pose Mode", "Toogle Tool Position", new Vector2(164f, -140f), "Position of the cloth toogle tool bar");
			maxModels = Config.Bind("Free Pose Mode", "Number of Models", 8, "Number of models in Free Pose Mode.");
			gizmoSize = Config.Bind("Gizmos", "Gizmo Size", 0.05f, "The size of the rotation axis");
			bubbleSize = Config.Bind("Gizmos", "Bubble Size", 0.05f, "The size of the selectable bubbles");
			numFilters = Config.Bind("Filters", "Number of Filters", 3, "Increase the number to add more filters");
			filters.Add(Config.Bind("Filters", "Filter1", "hip|head|Bend|Hand|Foot|Shin|abdomenLower|neckLower|chestLower|lowerJaw"));
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
				filters.Add(Config.Bind("Filters", string.Format("Filter{0}", i+1), ""));
			}
		}

		private void Update()
		{
			if (!modEnabled.Value) return;
			if (!Global.code) return;
			if (!Global.code.uiFreePose) return;
			if (!Global.code.uiFreePose.isActiveAndEnabled) return;
			if (!Global.code.uiFreePose.selectedCharacter) return;
			if (Global.code.uiFreePose.selectedCharacter.TryGetComponent(out CharacterCustomization cc))
			{
				if (cc.weapon && Input.GetKeyDown(KeyCode.Alpha1))
				{
					if (cc.weaponIndex == 1 && cc.weapon.gameObject.activeSelf)
					{
						cc.weapon.gameObject.SetActive(false);
					}
					else if (cc.weaponIndex == 0)
					{
						cc.weapon.gameObject.SetActive(true);
						cc.weaponIndex = 1;
						cc.Draw();
					}
					else
					{
						cc.weapon.gameObject.SetActive(true);
						cc.Holster(cc.weapon);
						cc.Holster(cc.weapon2);
					}
				}
				else if (cc.weapon2 && Input.GetKeyDown(KeyCode.Alpha2))
				{
					if (cc.weaponIndex == 2 && cc.weapon2 && cc.weapon2.gameObject.activeSelf)
					{
						cc.weapon2.gameObject.SetActive(false);
					}
					else if (cc.weaponIndex == 0)
					{
						cc.weapon2.gameObject.SetActive(true);
						cc.weaponIndex = 2;
						cc.Draw();
					}
					else
					{
						cc.weapon2.gameObject.SetActive(true);
						cc.Holster(cc.weapon);
						cc.Holster(cc.weapon2);
					}
				}
			}
		}

		public void ToggleGizmos(CharacterCustomization cc)
		{
			toggleGizmos = toggleGizmos % filters.Count;
			var bones = new List<Transform>(
				cc.body.bones).FindAll(
				t => Regex.IsMatch(t.name, filters[toggleGizmos].Value) 
			);
			cc.bonesNeedRender.Clear();
			cc.bonesNeedRender.AddRange(bones);
			toggleGizmos += 1;
		}

		[HarmonyPatch(typeof(UIFreePose), "Open")]
		public static class UIFreePose_Open_Patch
		{
			public static void Postfix()
			{
				if (!modEnabled.Value) return;
				Global.code.freeCameraCollider.SetActive(false);
				context.toggleGizmos = 0;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), "Refresh")]
		public static class UIFreePose_Refresh_Patch
		{
			public static GameObject toggleTool = null;

			public static void Postfix(UIFreePose __instance)
			{
				if (!modEnabled.Value) return;

				if (enableToggleTools.Value)
				{
					var group = __instance.transform.Find("Left").Find("group pose");
					group.Find("tools bg").GetComponent<RectTransform>().anchoredPosition = placementTools.Value;
					if (toggleTool == null)
					{
						toggleTool = Instantiate(Global.code.uiPose.panelTakeOffClothes, group);
					}
					else
					{
						toggleTool.transform.parent = group.transform;
					}
					toggleTool.GetComponent<RectTransform>().anchoredPosition = toggleTools.Value;
					toggleTool.SetActive(true);
				}

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

		[HarmonyPatch(typeof(UIFreePose), "PoseButtonClicked")]
		public static class UIFreePose_PoseButtonClicked_Patch
		{
			public static void Prefix(UIFreePose __instance, Pose code)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				context.switch_pose = true;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), "LetRuntimeTransformRun")]
		public static class UIFreePose_LetRuntimeTransformRun_Patch
		{
			public static void Prefix(UIFreePose __instance)
			{
				if (!modEnabled.Value || !__instance.selectedCharacter) return;
				if (TransformGizmo.transformGizmo_)
				{
					TransformGizmo.transformGizmo_.oldOne = null;
					TransformGizmo.transformGizmo_.handleLength = gizmoSize.Value;
					TransformGizmo.transformGizmo_.templateOfTemp.localScale = Vector3.one * bubbleSize.Value;
				}
				var cc = __instance.selectedCharacter.GetComponent<CharacterCustomization>();
				context.ToggleGizmos(cc);
			}
		}

		[HarmonyPatch(typeof(TransformGizmo), "LetRuntimeTransformSleep")]
		public static class TransformGizmo_LetRuntimeTransformSleep_Patch
		{
			public static void Postfix()
			{
				if (!modEnabled.Value || !Global.code.uiFreePose.selectedCharacter) return;
				if (Global.code.uiFreePose.selectedCharacter.TryGetComponent(out Animator anim)) anim.enabled = context.switch_pose;
				context.switch_pose = false;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), "AddCharacter")]
		public static class UIFreePose_AddCharacter_Patch
		{
			public static void Postfix(Transform character, UIFreePose __instance)
			{
				if (!modEnabled.Value || !keepPose.Value) return;
				__instance.selectedCharacter = character;
				if (character.TryGetComponent(out ThirdPersonCharacter tpc)) tpc.enabled = false;
				if (character.TryGetComponent(out Animator anim)) anim.enabled = false;
				if (character.TryGetComponent(out Companion c)) c.enabled = false;
			}
		}

		[HarmonyPatch(typeof(UIFreePose), "Close")]
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
						if (t.TryGetComponent(out Companion c)) c.enabled = true;
						if (t.TryGetComponent(out Animator anim)) anim.enabled = true;
					}
				}
			}

			public static void Postfix()
			{
				if (!modEnabled.Value || !keepPose.Value) return;
				foreach (var transform in context.backup)
				{
					if (transform.TryGetComponent(out Animator anim)) anim.enabled = false;
				}
				Player.code.GetComponent<Animator>().enabled = true;
			}
		}

		[HarmonyPatch(typeof(UICombatParty), "Open")]
		public static class UICombatParty_Open_Patch
		{
			public static void Prefix()
			{
				if (!modEnabled.Value || !keepPose.Value) return;

				foreach (var transform in context.backup)
				{
					if (!transform) continue;
					CharacterCustomization component = transform.GetComponent<CharacterCustomization>();
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
					if (transform.TryGetComponent(out Animator anim)) anim.enabled = true;
					if (transform.TryGetComponent(out Companion c)) c.enabled = true;
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

		[HarmonyPatch(typeof(CustomizationSlider), nameof(CustomizationSlider.ValueChange))]
		public static class CustomizationSlider_ValueChange_Patch
		{
			public static bool Prefix(CustomizationSlider __instance, float val)
			{
				if (!modEnabled.Value || !Global.code.uiFreePose.gameObject.activeSelf || !__instance.isEmotionController) return true;
				var cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
				cc.body.SetBlendShapeWeight(__instance.index, val);
				cc.eyelash.SetBlendShapeWeight(__instance.index, val);
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

		[HarmonyPatch(typeof(UIPose), "ButtonTakeoffBra")]
		public static class UIPose_ButtonTakeoffBra
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
						cc.bra.gameObject.SetActive(true);
					}
				}
				else if (cc.bra && cc.bra.gameObject.activeSelf)
				{
					cc.bra.gameObject.SetActive(false);
				}
				else if (cc.armor) 
				{
					cc.armor.gameObject.SetActive(true);
				}
				else if (cc.bra)
				{
					cc.bra.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), "ButtonTakeoffPanties")]
		public static class UIPose_ButtonTakeoffPanties
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
				else if(cc.suspenders && cc.suspenders.gameObject.activeSelf)
				{
					cc.suspenders.gameObject.SetActive(false);
				}
				else 
				{
					if (cc.suspenders) cc.suspenders.gameObject.SetActive(true);
					if (cc.panties) cc.panties.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), "ButtonTakeoffStockings")]
		public static class UIPose_ButtonTakeoffStockings
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
					if (cc.leggings) cc.leggings.gameObject.SetActive(true);
					if (cc.stockings) cc.stockings.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), "ButtonTakeoffHeels")]
		public static class UIPose_ButtonTakeoffHeels
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
						cc.heels.gameObject.SetActive(true);
					}
				}
				else if (cc.heels && cc.heels.gameObject.activeSelf)
				{
					cc.heels.gameObject.SetActive(false);
				}
				else if (cc.shoes)
				{
					cc.shoes.gameObject.SetActive(true);
				}
				else if (cc.heels)
				{
					cc.heels.gameObject.SetActive(true);
				}
				return false;
			}
		}
	}
}
