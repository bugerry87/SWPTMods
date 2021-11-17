﻿using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MegaMorph
{
	[BepInPlugin("bugerry.MegaMorph", "MegaMorph", "1.3.3")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public struct Offset
		{
			public CharacterCustomization cc;
			public Transform bone;
			public Vector3 offset;
			public Vector3 _default;
			public bool isScale;

			public void Apply()
			{
				if (cc.anim && !cc.anim.enabled)
				{
					//skip
				}
				else if (isScale)
				{
					bone.localScale = offset / 100f;
				}
				else if (bone.name == "hip")
				{
					if (!cc.interactingObject && !Global.code.uiFreePose.isActiveAndEnabled)
					{
						bone.localPosition += offset / 100f;
					}
				}
				else
				{
					bone.localPosition = offset / 100f;
				}
			}
		}

		public struct Preset
		{
			public Slider slider;
			public Vector3 preset;
		}

		private static BepInExPlugin context;
		public static ConfigEntry<bool> modEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<int> nexusID;
		public static ConfigEntry<string> presetName;
		const float threshold = 0.0625f;

		public readonly Dictionary<string, Offset> offsets = new Dictionary<string, Offset>();
		public readonly Dictionary<string, Vector3> presets = new Dictionary<string, Vector3>();
		public readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
		public readonly Dictionary<string, float> values = new Dictionary<string, float>();
		public Transform viewport = null;
		private bool isScanning = false;
		public string xml_file;

		public static XmlElement CreateAttribute(XmlDocument xml, string name, float x, float y, float z)
		{
			var elmt = xml.CreateElement(name);
			elmt.SetAttribute("x", x.ToString());
			elmt.SetAttribute("y", y.ToString());
			elmt.SetAttribute("z", z.ToString());
			return elmt;
		}

		public static XmlElement CreateAttribute(XmlDocument xml, string name, Vector3 vec)
		{
			return CreateAttribute(xml, name, vec.x, vec.y, vec.z);
		}

		public static Slider CloneSlider(UICustomization __instance, Transform viewport, string key)
		{
			var slider = __instance.panelSkin.GetComponentInChildren<Slider>();
			var name = key.Replace('_', ' ');
			slider = Instantiate(slider, viewport);
			slider.onValueChanged = new Slider.SliderEvent();
			slider.name = name;
			slider.minValue = 0f;
			slider.maxValue = 1f;
			slider.value = 0f;
			slider.onValueChanged.AddListener((float val) => context.ApplyPreset(key, val));
			slider.transform.GetComponentInChildren<Text>().text = name;
			Destroy(slider.GetComponentInChildren<LocalizationText>());
			return slider;
		}

		private void Awake()
		{
			context = this;
			Config.SaveOnConfigSet = false;
			modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
			isDebug = Config.Bind("General", "IsDebug", true, "Enable debug logs");
			nexusID = Config.Bind("General", "NexusID", 50, "Nexus mod ID for updates");
			presetName = Config.Bind("Preset", "Name", "Custom", "The name of your preset");
			Config.Bind("Preset", "Save", false, "Toggle for saving the current setting");
			Config.Save();

			var xml = new XmlDocument();
			xml_file = Config.ConfigFilePath.Replace(".cfg", ".xml");
			try
			{
				if (!File.Exists(xml_file))
				{
					var presets = xml.CreateElement("Presets");

					var preset = xml.CreateElement("Childish");
					preset.AppendChild(CreateAttribute(xml, "hip_pos", 0f, -20f, 0f));
					preset.AppendChild(CreateAttribute(xml, "hip_scale", 80f, 80f, 80f));
					preset.AppendChild(CreateAttribute(xml, "head_scale", 110f, 110f, 110f));
					presets.AppendChild(preset);

					preset = xml.CreateElement("Elph_Ears");
					preset.AppendChild(CreateAttribute(xml, "rEar_scale", 500f, 100f, 100f));
					preset.AppendChild(CreateAttribute(xml, "lEar_scale", 500f, 100f, 100f));
					presets.AppendChild(preset);

					preset = xml.CreateElement("Badonkas");
					preset.AppendChild(CreateAttribute(xml, "lPectoral_scale", 150f, 200f, 150f));
					preset.AppendChild(CreateAttribute(xml, "lPectoral_pos", 0f, -9f, -5f));
					preset.AppendChild(CreateAttribute(xml, "rPectoral_scale", 150f, 200f, 150f));
					preset.AppendChild(CreateAttribute(xml, "rPectoral_pos", 0f, -9f, -5f));
					presets.AppendChild(preset);

					xml.AppendChild(presets);
					xml.Save(xml_file);
				}
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		private void LateUpdate()
		{
			if (!modEnabled.Value || offsets.Count == 0) return;
			if (!Mainframe.code)
			{
				offsets.Clear();
			}
			else
			{
				foreach (var offset in offsets)
				{
					offset.Value.Apply();
				}
			}
		}

		public void ScanModel(CharacterCustomization cc)
		{
			if (!modEnabled.Value || cc == null) return;
			isScanning = true;
			foreach (var bone in cc.body.bones)
			{
				var key = new ConfigDefinition("Bones", bone.name + "_scale");
				if (!Config.ContainsKey(key))
				{
					Config.Bind(key, Vector3.one * 100f);
				}
				key = new ConfigDefinition("Bones", bone.name + "_pos");
				if (!Config.ContainsKey(key))
				{
					Config.Bind(key, bone.name == "hip" ? Vector3.zero : bone.localPosition * 100f);
				}
			}
			foreach (var obj in FindObjectsOfType<PhysicBonesCore>())
			{
				Logger.LogInfo(obj.name);
			}
			foreach (var obj in FindObjectsOfType<PhysicBonesCollider>())
			{
				Logger.LogInfo(obj.name);
			}
			isScanning = false;
		}

		public void SavePreset()
		{
			var xml = new XmlDocument();
			xml.Load(xml_file);
			var name = presetName.Value.Replace(' ', '_');
			var old_node = xml.DocumentElement.SelectSingleNode(name);
			var node = xml.CreateElement(name);

			foreach (var config in Config)
			{
				if (config.Key.Section != "Bones") continue;
				var vec = (Vector3)config.Value.BoxedValue;
				var delta = vec - (Vector3)config.Value.DefaultValue;
				if (Vector3.SqrMagnitude(delta) < threshold) continue;
				var key = config.Key.Key.Replace(' ', '-');
				var old = node.SelectSingleNode(key);
				vec = config.Key.Key.EndsWith("pos") ? delta : vec;

				if (old == null)
				{
					node.AppendChild(CreateAttribute(xml, key, vec));
				}
				else
				{
					node.ReplaceChild(CreateAttribute(xml, key, vec), old);
				}
			}

			if (old_node == null)
			{
				xml.DocumentElement.AppendChild(node);
			}
			else
			{
				xml.DocumentElement.ReplaceChild(node, old_node);
			}
				
			xml.Save(xml_file);
		}

		public void AddSlider(string name)
		{
			var preset = presetName.Value.Replace(' ', '_');
			var key = $"{name}/{preset}";
			var ui = Global.code.uiCustomization;
			if (ui && viewport && !sliders.ContainsKey(preset))
			{
				var slider = CloneSlider(ui, viewport, presetName.Value);
				slider.value = 0f;
				sliders[preset] = slider;
			}
			values[key] = 0f;
		}

		public void ApplyConfig(CharacterCustomization cc, SettingChangedEventArgs args)
		{
			if (!modEnabled.Value || isScanning || cc == null) return;
			
			if (args.ChangedSetting.Definition.Key == "Save")
			{
				try
				{
					SavePreset();
					AddSlider(cc.name);
				}
				catch (Exception e)
				{
					Logger.LogError(e);
				}
			}

			if (args.ChangedSetting.Definition.Section != "Bones") return;
			var key = $"{cc.name}/{args.ChangedSetting.Definition.Key}";
			foreach (var bone in cc.body.bones)
			{
				if (!args.ChangedSetting.Definition.Key.StartsWith(bone.name)) continue;
				var isScale = args.ChangedSetting.Definition.Key.EndsWith("scale");
				var vec = (Vector3)args.ChangedSetting.BoxedValue;
				var vec_default = (Vector3)args.ChangedSetting.DefaultValue;
				if (args.ChangedSetting.Definition.Key.StartsWith("hip") || Vector3.SqrMagnitude(vec - vec_default) >= threshold)
				{
					offsets[key] = new Offset
					{
						cc = cc,
						bone = bone,
						offset = vec,
						_default = vec_default,
						isScale = isScale
					};
				}
				else
				{
					offsets.Remove(key);
					bone.localScale = Vector3.one;
				}
				return;
			}
			Logger.LogWarning("Bone not found: " + key);
		}

		public void ApplyPreset(string key, float val)
		{
			var cc = Global.code.uiCustomization.curCharacterCustomization;
			if (!cc) return;

			values[$"{cc.name}/{key}"] = val;
			foreach (var bone in cc.body.bones)
			{
				var bone_scale = bone.name + "_scale";
				var bone_pos = bone.name + "_pos";
				var scale = Vector3.one * 100f;
				var pos = Vector3.zero;

				if (Config.TryGetEntry("Bones", bone_pos, out ConfigEntry<Vector3> config_pos))
				{
					pos = (Vector3)config_pos.DefaultValue;
				}

				foreach (var slider in sliders)
				{
					if (presets.TryGetValue(slider.Key + bone_scale, out Vector3 value))
					{
						scale = Vector3.Lerp(scale, Vector3.Scale(scale, value / 100f), slider.Value.value);
					}

					if (presets.TryGetValue(slider.Key + bone_pos, out value))
					{
						pos += value * slider.Value.value;
					}
				}

				if (Config.TryGetEntry("Bones", bone_scale, out ConfigEntry<Vector3> config_scale))
				{
					config_scale.Value = scale;
				}

				if (config_pos != null)
				{
					config_pos.Value = pos;
				}
			}
		}

		public void OnSettingChanged(object source, SettingChangedEventArgs args)
		{
			if (!modEnabled.Value || isScanning) return;

			if (Global.code.uiFreePose && Global.code.uiFreePose.selectedCharacter)
			{
				ApplyConfig(Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>(), args);
			}
			else if (Global.code.uiCustomization && Global.code.uiCustomization.curCharacterCustomization)
			{
				ApplyConfig(Global.code.uiCustomization.curCharacterCustomization, args);
			}
			else if (Player.code.customization)
			{
				ApplyConfig(Player.code.customization, args);
			}
		}

		public void LoadPreset(CharacterCustomization gen, ES2Data data)
		{
			try
			{
				foreach (var d in data.loadedData)
				{
					var found = false;
					var key = $"{gen.name}/{d.Key}";
					foreach (var bone in gen.body.bones)
					{
						var isScale = d.Key.EndsWith("scale");
						if (d.Key.StartsWith(bone.name))
						{
							var offset = (Vector3)d.Value;
							if (Config.TryGetEntry("Bones", d.Key, out ConfigEntry<Vector3> entry))
							{
								var _default = (Vector3)entry.DefaultValue;
								if (Vector3.SqrMagnitude(offset - _default) >= threshold)
								{
									offsets[key] = new Offset
									{
										cc = gen,
										bone = bone,
										offset = (Vector3)d.Value,
										_default = _default,
										isScale = isScale
									};
								}
							}
							found = true;
							break;
						}
					}

					if (!found)
					{
						if (sliders.TryGetValue(d.Key, out Slider slider))
						{
							slider.value = (float)d.Value;
						}
						else
						{
							values[key] = (float)d.Value;
						}
					}
				}
			}
			catch (Exception e)
			{
				context.Logger.LogWarning("OnLoad: " + e.Message);
			}
		}

		public void Reset(CharacterCustomization cc)
		{
			foreach (var slider in sliders)
			{
				slider.Value.value = 0f;
			}

			foreach (var bone in cc.body.bones)
			{
				offsets.Remove($"{cc.name}/{bone.name}_scale");
				offsets.Remove($"{cc.name}/{bone.name}_pos");
				bone.localScale = Vector3.one;

				if (Config.TryGetEntry("Bones", $"{bone.name}_scale", out ConfigEntry<Vector3> scale))
				{
					scale.Value = Vector3.one * 100f;
				}

				if (Config.TryGetEntry("Bones", $"{bone.name}_pos", out ConfigEntry<Vector3> pos))
				{
					pos.Value = (Vector3)pos.DefaultValue;
				}
			}
		}

		[HarmonyPatch(typeof(Player), "Awake")]
		public static class Player_Awake_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(Player).GetMethod("Awake");
			}

			public static void Postfix()
			{
				if (!modEnabled.Value) return;
				context.ScanModel(Player.code.customization);
				context.Config.SettingChanged += context.OnSettingChanged;
			}
		}

		[HarmonyPatch(typeof(Mainframe), "SaveGame")]
		public static class Mainframe_SaveGame_Patch
		{
			public static void Postfix(Mainframe __instance)
			{
				if (!modEnabled.Value) return;
				try
				{
					Directory.CreateDirectory(__instance.GetFolderName() + "MegaMorph");
					foreach (var offset in context.offsets)
					{
						var key = offset.Key.Split('/');
						var id = $"{__instance.GetFolderName()}MegaMorph/{key[0]}.txt?tag={key[1]}";
						if (Vector3.SqrMagnitude(offset.Value.offset - offset.Value._default) >= threshold)
						{
							ES2.Save(offset.Value.offset, id);
						}
						else
						{
							ES2.Delete(id);
						}
					}

					foreach (var val in context.values)
					{
						var key = val.Key.Split('/');
						var id = $"{__instance.GetFolderName()}MegaMorph/{key[0]}.txt?tag={key[1]}";
						ES2.Save(val.Value, id);
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
				context.sliders.Clear();
				try
				{
					var data = ES2.LoadAll($"{__instance.GetFolderName()}MegaMorph/{gen.name}.txt");
					context.LoadPreset(gen, data);
				}
				catch (Exception e)
				{
					context.Logger.LogWarning("OnLoad: " + e.Message);
				}
			}
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
				if (!modEnabled.Value) return;

				context.viewport = __instance.panelBody.transform.GetChild(0).GetChild(0).GetChild(0);
				var title = Instantiate(context.viewport.GetChild(0), context.viewport);
				title.GetComponent<Text>().text = "Mega Morph";
				title.name = "Mega Morph";
				Destroy(title.GetComponent<LocalizationText>());

				try
				{
					var xml = new XmlDocument();
					xml.Load(context.xml_file);
					var nodes = xml.DocumentElement.ChildNodes;
					for (var i = 0; i < nodes.Count; ++i)
					{
						var preset = nodes[i].Name;

						var sub = nodes[i].ChildNodes;
						for (var j = 0; j < sub.Count; ++j)
						{
							var attr = sub[j].Attributes;
							context.presets[preset + sub[j].Name.Replace('-', ' ')] = new Vector3(
								float.Parse(attr[0].Value),
								float.Parse(attr[1].Value),
								float.Parse(attr[2].Value)
							);
						}

						if (!context.sliders.ContainsKey(preset))
						{
							context.sliders[preset] = CloneSlider(__instance, context.viewport, preset);
						}
					}
					UICustomization_Open_Patch.Postfix(__instance.curCharacterCustomization);
				}
				catch (Exception e)
				{
					context.Logger.LogError(e);
				}
			}
		}

		[HarmonyPatch(typeof(UICustomization), "Open")]
		public static class UICustomization_Open_Patch
		{
			public static void Postfix(CharacterCustomization customization, bool isOpenChangeName = true)
			{
				if (!modEnabled.Value || customization == null) return;

				foreach (var slider in context.sliders)
				{
					var key = $"{customization.name}/{slider.Key}";
					if (context.values.TryGetValue(key, out float val))
					{
						slider.Value.value = val;
					}
					else
					{
						try
						{
							var item = $"{Mainframe.code.GetFolderName()}MegaMorph/{customization.name}.txt?tag={slider.Key}";
							if (ES2.Exists(item))
							{
								val = ES2.Load<float>(item);
								context.values[key] = val;
								slider.Value.value = val;
							}
							else
							{
								slider.Value.value = 0f;
								context.Logger.LogWarning($"{item} does not exist!");
							}
						}
						catch (Exception e)
						{
							slider.Value.value = 0f;
							context.Logger.LogWarning(e);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Mainframe), "SaveCharacterPreset")]
		public static class Mainframe_SaveCharacterPreset_Patch
		{
			public static void Postfix(CharacterCustomization customization, string presetname, string creator, Texture2D profile)
			{
				try
				{
					foreach (var offset in context.offsets)
					{
						var key = offset.Key.Split('/')[1];
						var item = $"Character Presets/{presetname}/MegaMorph.txt?tag={key}";
						if (Vector3.SqrMagnitude(offset.Value.offset - offset.Value._default) >= threshold)
						{
							ES2.Save(offset.Value.offset, item);
						}
						else
						{
							ES2.Delete(item);
						}
					}

					foreach (var val in context.values)
					{
						if (val.Key.StartsWith(customization.name))
						{
							var key = val.Key.Split('/')[1];
							var item = $"Character Presets/{presetname}/MegaMorph.txt?tag={key}";
							ES2.Save(val.Value, item);
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
				var item = $"Character Presets/{presetname}/MegaMorph.txt";
				if (ES2.Exists(item))
				{
					context.Reset(gen);
					var data = ES2.LoadAll(item);
					context.LoadPreset(gen, data);
				}
			}
		}
	}
}
