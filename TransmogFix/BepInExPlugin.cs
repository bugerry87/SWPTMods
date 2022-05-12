using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace TransmogFix
{
    [BepInPlugin("bugerry.TransmogFix", "Transmog Fix", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        private void Awake()
        {
            context = this;
            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
            nexusID = Config.Bind("General", "NexusID", 137, "Nexus mod ID for updates");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(TransitionDropZone), nameof(TransitionDropZone.OnClick))]
        public static class TransitionDropZone_OnClick_Patch
        {
            public static bool Prefix(TransitionDropZone __instance)
            {
                if (!modEnabled.Value) return true;
				if (!Global.code.selectedItem || !Global.code.selectedItem.TryGetComponent(out Item item))
				{
					return false;
				}

				switch (item.slotType)
				{
					case SlotType.armor: break;
					case SlotType.gloves: break;
					case SlotType.helmet: break;
					case SlotType.legging: break;
					case SlotType.shoes: break;
					case SlotType.necklace: break;
					case SlotType.ring: break;
					case SlotType.weapon: break;
					case SlotType.shield: break;
					default: return false;
				}

				if (__instance.Sign == "Left")
				{
					if (Global.code.uiTransiiton.Target)
					{
						var target = Global.code.uiTransiiton.Target.GetComponent<Item>();
						if (target.itemType != item.itemType || 
							target.slotType != item.slotType ||
							target.TryGetComponent(out Weapon target_weapon) &&
							item.TryGetComponent(out Weapon item_weapon) &&
							target_weapon.weaponType != item_weapon.weaponType
						)
						{
							Global.code.uiCombat.AddPrompt(Localization.GetContent("Type must same"));
							return false;
						}
					}
					if (Global.code.uiTransiiton.Original != null)
					{
						var original = Global.code.uiTransiiton.Original;
						Global.code.uiTransiiton.Original = Global.code.selectedItem;
						Global.code.selectedItem = original;
					}
					else
					{
						Global.code.uiTransiiton.Original = Global.code.selectedItem;
						Global.code.selectedItem = null;
					}
					Global.code.uiTransiiton.Refresh();
				}
				else if (__instance.Sign == "Right")
				{
					if (Global.code.uiTransiiton.Original)
					{
						Item original = Global.code.uiTransiiton.Original.GetComponent<Item>();
						if (original.itemType != item.itemType || 
							original.slotType != item.slotType ||
							original.TryGetComponent(out Weapon original_weapon) &&
							item.TryGetComponent(out Weapon item_weapon) &&
							original_weapon.weaponType != item_weapon.weaponType
						)
						{
							Global.code.uiCombat.AddPrompt(Localization.GetContent("Type must same"));
							return false;
						}
					}
					if (Global.code.uiTransiiton.Target != null)
					{
						var target = Global.code.uiTransiiton.Target;
						Global.code.uiTransiiton.Target = Global.code.selectedItem;
						Global.code.selectedItem = target;
					}
					else
					{
						Global.code.uiTransiiton.Target = Global.code.selectedItem;
						Global.code.selectedItem = null;
					}
					Global.code.uiTransiiton.Refresh();
				}
				Player.code.customization.storage.inventory.Refresh();
				return false;
            }
        }
    }
}
