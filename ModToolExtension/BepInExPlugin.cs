using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UMod;
using SkinnedDecals;

namespace ModToolExtension
{
	[BepInPlugin("bugerry.ModToolExtension", "Mod Tool Extension", "1.0.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		private static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;

		public readonly Dictionary<string, BodyData> bodies = new Dictionary<string, BodyData>();
		public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

		private void Awake()
		{
			context = this;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 107, "Nexus mod ID for updates");
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		public static void LoadItems(GameObject asset)
		{
			foreach (var data in asset.GetComponentsInChildren<ItemData>())
			{
				Item item = data.gameObject.AddComponent<Item>();
				item.itemType = ItemType.item;
				item.slotType = data.slotType;
				item.rarity = data.rarity;
				item.icon = data.icon;
				item.itemType = data.itemType;
				item.closetIcon = data.closetIcon;
				item.autoPickup = data.autoPickup;
				item.levelrequirement = data.levelrequirement;
				item.auraPrefab = data.auraPrefab;
				item.projectilePower = data.projectilePower;
				item.shieldGrip = data.shieldGrip;
				item.addHealth = data.addHealth;
				item.addMana = data.addMana;
				item.sndPickup = data.sndPickup;
				item.sndDrop = data.sndDrop;
				item.sndUse = data.sndUse;
				item.level = Mathf.Max(data.level, 1);
				item.x = Mathf.Clamp(data.inventoryWidth, 1, 5);
				item.y = Mathf.Clamp(data.inventoryHeight, 1, 5);

				if (data.TryGetComponent(out WeaponData weaponData))
				{
					Weapon weapon = data.gameObject.AddComponent<Weapon>();
					item.slotType = SlotType.weapon;
					weapon.weaponType = weaponData.weaponType;
					weapon.projectile = weaponData.projectile;
					weapon.projectileFiringLoc = weaponData.projectileFiringLoc;
					weapon.trail = weaponData.trail;

					if (data.UseBuitinCalculator)
					{
						RM.code.balancer.GetItemStats(data.transform, 0);
					}
					else
					{
						item.cost = data.cost;
						item.crystals = data.crystals;
						item.damage = data.damage;
						item.defence = data.defence;
						item.balanceDamage = data.balanceDamage;
						item.staminaCost = data.staminaCost;
						item.rage = data.rage;
						item.shieldImmunePercent = data.shieldImmunePercent;
						item.fireDamage = data.fireDamage;
						item.coldDamage = data.coldDamage;
						item.lighteningDamage = data.lighteningDamage;
						item.poisonDamage = data.poisonDamage;
					}
					RM.code.allWeapons.AddItem(data.transform);
				}

				if (data.TryGetComponent(out AppealData appealData))
				{
					Appeal appeal = appealData.gameObject.AddComponent<Appeal>();
					appeal.highheel = appealData.Highheel;
					appeal.isFromMOD = true;

					switch (data.slotType)
					{
						case SlotType.armor: //Fall through
						case SlotType.gloves:
						case SlotType.helmet:
						case SlotType.legging:
						case SlotType.shoes:
							RM.code.allArmors.AddItem(appealData.transform); break;
						case SlotType.bra: //Fall through
						case SlotType.heels:
						case SlotType.lingeriegloves:
						case SlotType.panties:
						case SlotType.stockings:
						case SlotType.suspenders:
							RM.code.allLingeries.AddItem(appealData.transform); break;
						default:
							break;
					}
				}
				RM.code.allItems.AddItem(data.transform);
			}
		}

		public static void LoadCustomization(GameObject asset)
		{
			foreach (var data in asset.GetComponentsInChildren<CustomizationData>())
			{
				if (!data.GetComponent<CustomizationItem>())
				{
					CustomizationItem customizationItem = data.gameObject.AddComponent<CustomizationItem>();
					customizationItem.icon = data.Icon;
					customizationItem.face = data.Face;
					customizationItem.torso = data.Torso;
					customizationItem.arms = data.Arms;
					customizationItem.legs = data.Legs;
					customizationItem.eyes = data.Texture;
					customizationItem.eyesMat = data.Mat;
				}

				switch (data.Type)
				{
					case CustomizationType.Eye: RM.code.allEyes.AddItem(data.transform); break;
					case CustomizationType.Eyebrow: break;
					case CustomizationType.EyesMakeupShape: break;
					case CustomizationType.Hair: RM.code.allHairs.AddItem(data.transform); break;
					case CustomizationType.Horn: RM.code.allHorns.AddItem(data.transform); break;
					case CustomizationType.Nail: RM.code.allNails.AddItem(data.transform); break;
					case CustomizationType.PubicHair: RM.code.allPubicHairs.AddItem(data.transform); break;
					case CustomizationType.Skins: RM.code.allSkins.AddItem(data.transform); break;
					case CustomizationType.TatooArms: RM.code.allArmsTatoos.AddItem(data.transform); break;
					case CustomizationType.TatooFace: RM.code.allFaceTatoos.AddItem(data.transform); break;
					case CustomizationType.TatooLegs: RM.code.allLegsTatoos.AddItem(data.transform); break;
					case CustomizationType.TatooTorso: RM.code.allBodyTatoos.AddItem(data.transform); break;
					case CustomizationType.Toenails: RM.code.allToeNails.AddItem(data.transform); break;
					case CustomizationType.Wing: RM.code.allWings.AddItem(data.transform); break;
					case CustomizationType.WombTattoos: RM.code.allWombTattoos.AddItem(data.transform); break;
					default: break;
				}
			}
		}

		public static void LoadFurniture(GameObject asset)
		{
			foreach (var data in asset.GetComponentsInChildren<FurnitureData>())
			{
				data.gameObject.AddComponent<MapIcon>();
				data.gameObject.AddComponent<Interaction>();

				var building = data.gameObject.AddComponent<Building>();
				building.categoryName = data._categroyName.Length > 0 ? data._categroyName : data._category.ToString();
				building.icon = data._icon;
				building.cost = data._crystals;

				var furniture = data.gameObject.AddComponent<Furniture>();
				furniture.camerasGroup = data._camerasGroup;
				furniture.posesGroup = data._posesGroup;
				furniture.poses = new CommonArray();
				furniture.cameras = new CommonArray();
				furniture.dontRandomPose = true;

				if (furniture.posesGroup)
				{
					foreach (var pose in furniture.posesGroup.GetComponentsInChildren<Pose>())
					{
						furniture.poses.AddItem(pose.transform);
					}
				}

				if (furniture.camerasGroup)
				{
					foreach (var cam in furniture.camerasGroup.GetComponentsInChildren<Camera>())
					{
						furniture.cameras.AddItem(cam.transform);
					}
				}

				furniture.notInteractableByCompanion = furniture.poses.items.Count == 0;
				RM.code.allBuildings.AddItem(data.transform);
			}
		}

		public static void LoadPoses(GameObject asset)
		{
			foreach (var pose in asset.GetComponentsInChildren<Pose>())
			{
				pose.loc = pose.loc ? pose.loc : pose.transform;
				pose.controller = pose.controller ? pose.controller : pose.GetComponent<RuntimeAnimatorController>();
				RM.code.allFreePoses.AddItem(pose.transform);
			}
		}

		public static void LoadBodies(GameObject asset)
		{
			foreach (var data in asset.GetComponentsInChildren<BodyData>())
			{
				context.bodies[data.name] = data;
			}
		}

		public static void LoadEnemies(GameObject asset)
		{
			foreach (var data in asset.GetComponentsInChildren<EnemyData>())
			{
				data.gameObject.AddComponent<SkinnedDecalSystem>();
				data.gameObject.AddComponent<LootDrop>();
				var monster = data.gameObject.AddComponent<Monster>();
				monster.healthGrade = data.GetComponent<EnemyData>().healthGrade;
				monster.damageGrade = data.GetComponent<EnemyData>().damageGrade;
				monster.enemyRarity = data.GetComponent<EnemyData>().enemyRarity;
				monster.attackDist = data.GetComponent<EnemyData>().attackDist;
				monster.specialAttackDist = data.GetComponent<EnemyData>().specialAttackDist;
				monster.kickDist = data.GetComponent<EnemyData>().kickDist;
				monster.backOffDistance = data.GetComponent<EnemyData>().backOffDistance;
				monster.skillDistance = data.GetComponent<EnemyData>().skillDistance;
				monster.skillInterval = data.GetComponent<EnemyData>().skillInterval;
				monster.skillCD = data.GetComponent<EnemyData>().skillCD;
				monster.weapon = data.GetComponent<EnemyData>().weapon;
				if (monster.weapon.GetComponent<ItemData>())
				{
					Item item2 = monster.weapon.gameObject.AddComponent<Item>();
					item2.itemType = ItemType.item;
					item2.slotType = monster.weapon.GetComponent<ItemData>().slotType;
					item2.rarity = monster.weapon.GetComponent<ItemData>().rarity;
					item2.icon = monster.weapon.GetComponent<ItemData>().icon;
					item2.level = monster.weapon.GetComponent<ItemData>().Level;
				}
				if (monster.weapon.GetComponent<WeaponData>())
				{
					Weapon weapon2 = monster.weapon.gameObject.AddComponent<Weapon>();
					weapon2.weaponType = monster.weapon.GetComponent<WeaponData>().weaponType;
					weapon2.projectile = monster.weapon.GetComponent<WeaponData>().projectile;
					weapon2.projectileFiringLoc = monster.weapon.GetComponent<WeaponData>().projectileFiringLoc;
					weapon2.trail = monster.weapon.GetComponent<WeaponData>().trail;
					monster.weapon.GetComponent<Item>().slotType = SlotType.weapon;
				}
				data.gameObject.AddComponent<ID>().eye = data.GetComponent<EnemyData>().Head;
				data.gameObject.AddComponent<Bodypart>();
				data.gameObject.AddComponent<NavMeshAgent>();
				data.gameObject.GetComponent<Animator>().runtimeAnimatorController = RM.code.TestController;
				RM.code.allEnemies.AddItem(data.transform);
			}
		}

		public static void ApplyPose(CharacterCustomization cc, Pose pose)
		{
			cc.anim.runtimeAnimatorController = pose.controller;
			if (pose.TryGetComponent(out AvatarData data))
			{
				cc.anim.avatar = data.avatar;
			}
			else if (pose.TryGetComponent(out Animator anim))
			{
				cc.anim.avatar = anim.avatar;
			}
			else
			{
				cc.anim.avatar = RM.code.genericAvatar;
			}

			if (cc.anim.layerCount > 1)
			{
				if (cc.IsWearingHighHeels())
				{
					cc.anim.SetLayerWeight(1, 1f);
				}
				else
				{
					cc.anim.SetLayerWeight(1, 0f);
				}
			}
		}

		[HarmonyPatch(typeof(ModManager), "LoadMod")]
		public static class ModManager_LoadMod_Patch
		{
			public static bool Prefix(Uri uri, bool IsScene = false)
			{
				if (!modEnabled.Value || IsScene) return true;

				ModHost modHost = Mod.Load(uri, true);
				for (int i = 0; i < modHost.Assets.AssetCount; i++)
				{
					GameObject asset = modHost.Assets.Load(i) as GameObject;
					if (!asset) continue;

					LoadItems(asset);
					LoadCustomization(asset);
					LoadFurniture(asset);
					LoadPoses(asset);
					LoadBodies(asset);
					LoadEnemies(asset);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(Furniture), "DoPose")]
		public static class Furniture_DoPose_Patch
		{
			public static bool Prefix(Furniture __instance, Pose code)
			{
				if (!modEnabled.Value) return true;
				if (!__instance.user || !code) return false;

				if (__instance.GetComponent<Mirror>())
				{
					foreach (var mesh in __instance.user.body.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>())
					{
						if (mesh.name.EndsWith("(Clone)"))
						{
							mesh.renderingLayerMask = 0;
							Destroy(mesh);
						}
						else
						{
							mesh.renderingLayerMask = 0xFFFFFFFF;
						}
					}
				}

				if (code.TryGetComponent(out BodyData body))
				{
					foreach (var bone in __instance.user.body.bones)
					{
						context.bones[bone.name] = bone;
					}

					foreach (var mesh in body.GetComponentsInChildren<SkinnedMeshRenderer>())
					{
						if (mesh.GetComponent<ItemData>()) continue;
						if (mesh.GetComponent<CustomizationData>()) continue;
						var overlay = Instantiate(mesh, __instance.user.body.transform.parent);
						overlay.rootBone = __instance.user.body.rootBone;
						var bones = new Transform[overlay.bones.Length];
						for (var i = 0; i < overlay.bones.Length; ++i)
						{
							if (context.bones.TryGetValue(overlay.bones[i].name, out Transform bone))
							{
								bones[i] = bone;
							}
							else
							{
								context.Logger.LogError("Bone is missing: " + overlay.bones[i].name);
							}
						}
						overlay.bones = bones;
					}
					__instance.user.body.renderingLayerMask = body.hideBody ? 0 : 0xFFFFFFFF;
					__instance.user.eyelash.renderingLayerMask = body.hideEyelash ? 0 : 0xFFFFFFFF;
				}
				else
				{
					__instance.user.curInteractionLoc = code.loc;
					ApplyPose(__instance.user, code);
					foreach (Transform transform in __instance.poses.items)
					{
						if (transform && transform != code.transform)
						{
							transform.gameObject.SetActive(false);
						}
					}
					code.gameObject.SetActive(true);
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(FreePoseIcon), "Click")]
		public static class FreePoseIcon_Click_Patch
		{
			public static bool Prefix(FreePoseIcon __instance)
			{
				if (!modEnabled.Value) return true;
				Pose pose = __instance.pose.GetComponent<Pose>();
				CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
				ApplyPose(cc, pose);
				return false;
			}
		}

		[HarmonyPatch(typeof(UIPose), "Open")]
		public static class UIPose_Open_Patch
		{
			public static void Postfix(UIPose __instance, Furniture furniture, CharacterCustomization customization)
			{
				if (!modEnabled.Value || !furniture.GetComponent<Mirror>()) return;

				foreach (var body in context.bodies.Values)
				{
					var icon = Instantiate(__instance.poseIconPrefab);
					icon.SetParent(__instance.poseIconGroup);
					icon.localScale = Vector3.one;
					if (!body.TryGetComponent(out Pose pose))
					{
						pose = body.gameObject.AddComponent<Pose>();
						pose.categoryName = "Models";
						if (furniture.poses.items.Count > 0 && furniture.poses.items[0].TryGetComponent(out Pose org))
						{
							pose.icon = body.icon;
							pose.loc = org.loc;
							pose.mood = org.mood;
							pose.controller = org.controller;
							pose.crystals = org.crystals;
							pose.locked = org.locked;
							pose.notshown = org.notshown;
						}
					}
					icon.GetComponent<PoseIcon>().Initiate(pose.transform);
				}
			}
		}
	}
}
