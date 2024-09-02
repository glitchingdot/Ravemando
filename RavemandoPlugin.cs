using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ravemando
{
	// Token: 0x02000002 RID: 2
	[BepInPlugin("com.jackdotpng.Ravemando", "Ravemando", "2.0.2")]
	public class RavemandoPlugin : BaseUnityPlugin
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		// (set) Token: 0x06000002 RID: 2 RVA: 0x00002057 File Offset: 0x00000257
		internal static RavemandoPlugin Instance { get; private set; }

        private static ConfigEntry<float> cycleTime;
        private static ConfigEntry<float> strengthMultiplier;

        private static List<CharacterModel.RendererInfo> cycleRenderers = new List<CharacterModel.RendererInfo>();

        private static List<Color> defaultColors = new List<Color>
            {
                new Color32(187, 91, 91, 255),
                new Color32(187, 127, 91, 255),
                new Color32(187, 172, 91, 255),
                new Color32(120, 187, 91, 255),
                new Color32(91, 187, 158, 255),
                new Color32(91, 121, 187, 255),
                new Color32(187, 91, 185, 255),
                new Color32(139, 91, 187, 255),
            };

		private static List<ConfigEntry<Color>> customColorConfigs = new List<ConfigEntry<Color>>{};

		private enum ColorSet
		{
			Default,
			Custom
		};

        private static ConfigEntry<ColorSet> colorSet;

        private static List<Color> GetCustomColors()
        {
			List<Color> colors = new List<Color>();
			if (customColorConfigs.Count <= 0)
			{
				InstanceLogger.LogError("Custom colors not found!");
			}

			for (int i = 0; i < customColorConfigs.Count; i++)
			{
				colors.Add(customColorConfigs[i].Value);
			}
			return colors;
        }

        private static IEnumerator CycleColor()
        {
            int colorIndex = 0;

			List<Color> cycleColors = defaultColors;

			switch(colorSet.Value)
			{
                case ColorSet.Default:
					//If this fails (colorSet not set), it will fail safe because it will default to to base colors without needing config input
					break;
				case ColorSet.Custom:
					cycleColors = GetCustomColors();
                    break;
            }

            for (;;)
            {
                if (colorIndex >= cycleColors.Count)
                {
                    colorIndex = 0;
                }

                for (int i = 0; i < cycleRenderers.Count; i++)
                {
                    CharacterModel.RendererInfo rendererInfo = cycleRenderers[i];
                    Material mat = rendererInfo.defaultMaterial;
                    mat.SetColor("_EmColor", cycleColors[colorIndex] * strengthMultiplier.Value);
                    rendererInfo.defaultMaterial = mat;
                }

                colorIndex++;

                yield return new WaitForSeconds(cycleTime.Value);

            }
        }

        private static void StartCycle()
        {
            InstanceLogger.LogInfo("Started color cycle!");
            InstanceLogger.LogInfo($"{cycleRenderers.Count} Renderers are being managed");
			InstanceLogger.LogInfo($"Config Options:");
            InstanceLogger.LogInfo($"Cycle Time: {cycleTime.Value} seconds");
            InstanceLogger.LogInfo($"Strength Multiplier: {strengthMultiplier.Value} times");
            Instance.StartCoroutine(CycleColor());
        }

		private static void AddToCycle(CharacterModel.RendererInfo renderer)
		{
			cycleRenderers.Add(renderer);
		}

        // Token: 0x17000002 RID: 2
        // (get) Token: 0x06000003 RID: 3 RVA: 0x0000205F File Offset: 0x0000025F
        internal static ManualLogSource InstanceLogger
		{
			get
			{
				RavemandoPlugin instance = RavemandoPlugin.Instance;
				return (instance != null) ? instance.Logger : null;
			}
		}

		private void Awake()
		{
			int customColorCount = 8;

			cycleTime = Config.Bind("General",
									"CycleTime",
									0.5f,
                                    "Controls the time (in seconds) between changing colors");

			strengthMultiplier = Config.Bind("General",
											 "StrengthMultiplier",
											 1.0f,
											 "Controls how intense the lighting displays");

            colorSet = Config.Bind("ColorSet",
								   "ColorSet",
								   ColorSet.Default,
								   "Controls which color set will be used");

            for (int i = 0; i < customColorCount; i++)
            {
                Color color = defaultColors[i];
                ConfigEntry<Color> configColor = Config.Bind("ColorSet.CustomColors",
															 $"Color{i}",
															 color,
															 $"Custom color #${i}");
				customColorConfigs.Add(configColor);
            }
        }

		// Token: 0x06000004 RID: 4 RVA: 0x00002074 File Offset: 0x00000274
		private void Start()
		{
			RavemandoPlugin.Instance = this;
			using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ravemando.jackdotpngravemando"))
			{
				RavemandoPlugin.assetBundle = AssetBundle.LoadFromStream(manifestResourceStream);
			}
			BodyCatalog.availability.CallWhenAvailable(new Action(RavemandoPlugin.BodyCatalogInit));
			HookEndpointManager.Add(typeof(Language).GetMethod("LoadStrings"), new Action<Action<Language>, Language>(RavemandoPlugin.LanguageLoadStrings));
			RavemandoPlugin.ReplaceShaders();
		}

		// Token: 0x06000005 RID: 5 RVA: 0x00002108 File Offset: 0x00000308
		private static void ReplaceShaders()
		{
			RavemandoPlugin.LoadMaterialsWithReplacedShader("RoR2/Base/Shaders/HGStandard.shader", new string[]
			{
				"Assets/Commando/01 - Ravemando/Material.mat",
				"Assets/Commando/02 - H0rn3t/Material.mat",
				"Assets/Bandit/Hat.mat"
			});
		}

		// Token: 0x06000006 RID: 6 RVA: 0x00002134 File Offset: 0x00000334
		private static void LoadMaterialsWithReplacedShader(string shaderPath, params string[] materialPaths)
		{
			Shader shader = Addressables.LoadAssetAsync<Shader>(shaderPath).WaitForCompletion();
			foreach (string text in materialPaths)
			{
				Material material = RavemandoPlugin.assetBundle.LoadAsset<Material>(text);
				material.shader = shader;
				RavemandoPlugin.materialsWithRoRShader.Add(material);
			}
		}

		// Token: 0x06000007 RID: 7 RVA: 0x00002190 File Offset: 0x00000390
		private static void LanguageLoadStrings(Action<Language> orig, Language self)
		{
			orig(self);
			self.SetStringByToken("JACKDOTPNG_SKIN_COMMANDO_-_RAVEMANDO_NAME", "Ravemando");
			self.SetStringByToken("JACKDOTPNG_SKIN_COMMANDO_-_H0RN3T_NAME", "H0rn3t");
			self.SetStringByToken("JACKDOTPNG_SKIN_HUNTRESS_-_HUNTRESS_NAME", "PLACEHOLDER");
			self.SetStringByToken("JACKDOTPNG_SKIN_HUNTRESS_-_ARCTIC_NAME", "PLACEHOLDER");
			self.SetStringByToken("JACKDOTPNG_SKIN_BANDIT_-_RGBANDIT_NAME", "RGBandit");
			self.SetStringByToken("JACKDOTPNG_SKIN_ENGINEER_-_ENGINEER_NAME", "PLACEHOLDER");
			self.SetStringByToken("JACKDOTPNG_SKIN_ENGINEER_-_32_EOD_TECH_NAME", "PLACEHOLDER");
			self.SetStringByToken("JACKDOTPNG_SKIN_LOADER_-_L0@D3R_NAME", "L0@d3r");
			self.SetStringByToken("JACKDOTPNG_SKIN_LOADER_-_CLA$$IC_NAME", "Cla$$ic");
			self.SetStringByToken("JACKDOTPNG_SKIN_CAPTAIN_-_TRAPTAIN_NAME", "Traptain");
			self.SetStringByToken("JACKDOTPNG_SKIN_CAPTAIN_-_RADMIRAL_NAME", "Radmiral");
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002261 File Offset: 0x00000461
		private static void Nothing(Action<SkinDef> orig, SkinDef self)
		{
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00002264 File Offset: 0x00000464
		private static void BodyCatalogInit()
		{
			MethodInfo method = typeof(SkinDef).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
			HookEndpointManager.Add(method, new Action<Action<SkinDef>, SkinDef>(RavemandoPlugin.Nothing));

			RavemandoPlugin.AddCommandoBodyCommandoRavemandoSkin(); // Done
			RavemandoPlugin.AddCommandoBodyCommandoH0rn3tSkin(); // Done
			RavemandoPlugin.AddHuntressBodyHuntressHuntressSkin(); // To-Do
            RavemandoPlugin.AddHuntressBodyHuntressArcticSkin(); // To-Do
            RavemandoPlugin.AddBandit2BodyBanditRGBanditSkin(); // To-Do
            RavemandoPlugin.AddEngiBodyEngineerEngineerSkin(); // To-Do (Figure out minion skins)
            RavemandoPlugin.AddEngiBodyEngineer32EODTechSkin(); // To-Do (Figure out minion skins)
            RavemandoPlugin.AddLoaderBodyLoaderL0D3rSkin(); // Done
			RavemandoPlugin.AddLoaderBodyLoaderClaIcSkin(); // Done
			RavemandoPlugin.AddCaptainBodyCaptainTraptainSkin(); // Done 
			RavemandoPlugin.AddCaptainBodyCaptainRadmiralSkin(); // Done

			// To-Do: 5
			// In-Progress: 1
			// Done: 5

            HookEndpointManager.Remove(method, new Action<Action<SkinDef>, SkinDef>(RavemandoPlugin.Nothing));
            StartCycle();
        }

        // Token: 0x0600000A RID: 10 RVA: 0x000022F4 File Offset: 0x000004F4
        private static void AddCommandoBodyCommandoRavemandoSkin()
		{
			string text = "CommandoBody";
			string text2 = "Commando - Ravemando";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Commando/01 - Ravemando/Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_COMMANDO_-_RAVEMANDO_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[0]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "Ravemando");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
								Material mat = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/01 - Ravemando/Material.mat");
                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[6];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                AddToCycle(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00002674 File Offset: 0x00000874
		private static void AddCommandoBodyCommandoH0rn3tSkin()
		{
			string text = "CommandoBody";
			string text2 = "Commando - H0rn3t";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Commando/02 - H0rn3t/Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_COMMANDO_-_H0RN3T_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[1]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "H0rn3t");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								SkinDef skinDef = skin;
								CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
								int num = 0;
								CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
								rendererInfo.defaultMaterial = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/02 - H0rn3t/Material.mat");
								rendererInfo.defaultShadowCastingMode = 0;
								rendererInfo.ignoreOverlays = false;
								rendererInfo.renderer = renderers[6];
								array[num] = rendererInfo;
								skinDef.rendererInfos = array;
                                AddToCycle(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x0600000C RID: 12 RVA: 0x000029F4 File Offset: 0x00000BF4
		private static void AddHuntressBodyHuntressHuntressSkin()
		{
			string text = "HuntressBody";
			string text2 = "Huntress - Huntress";
			int baseSkinIndex = 0;
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_HUNTRESS_-_HUNTRESS_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[baseSkinIndex]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "PLACEHOLDER");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
                                SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
                                int rendererIndex = 10;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
                                //Create a copy of the default skin
                                Material baseMat = skinController.skins[baseSkinIndex].rendererInfos[rendererIndex].defaultMaterial;
                                Material mat = new Material(baseMat);
                                // Swap the diffuse texture to black out the visor
                                Texture2D newDiffuse = RavemandoPlugin.assetBundle.LoadAsset<Texture2D>("Assets/Huntress/Huntress - Diffuse - Main.png");
                                mat.SetTexture("_MainTex", newDiffuse);

								Texture2D newEmi = RavemandoPlugin.assetBundle.LoadAsset<Texture2D>("Assets/Huntress/Huntress - Emission.png");
								mat.SetTexture("_EmiTex", newEmi);

                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[rendererIndex];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                AddToCycle(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002D70 File Offset: 0x00000F70
		private static void AddHuntressBodyHuntressArcticSkin()
		{
			string text = "HuntressBody";
			string text2 = "Huntress - Arctic";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_HUNTRESS_-_ARCTIC_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[1]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "Funktress");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								skin.rendererInfos = Array.Empty<CharacterModel.RendererInfo>();
							});
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x0600000E RID: 14 RVA: 0x000030EC File Offset: 0x000012EC
		private static void AddBandit2BodyBanditRGBanditSkin()
		{
			string text = "Bandit2Body";
			string text2 = "Bandit - RGBandit";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_BANDIT_-_RGBANDIT_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[0]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "RGBandit");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								SkinDef skinDef = skin;
								CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
								int num = 0;
								CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
								rendererInfo.defaultMaterial = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Bandit/Hat.mat");
								rendererInfo.defaultShadowCastingMode = 0;
								rendererInfo.ignoreOverlays = false;
								rendererInfo.renderer = renderers[6];
								array[num] = rendererInfo;
								skin.rendererInfos = array;
                                AddToCycle(rendererInfo);
							});
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x0600000F RID: 15 RVA: 0x0000346C File Offset: 0x0000166C
		private static void AddEngiBodyEngineerEngineerSkin()
		{
			string text = "EngiBody";
			string text2 = "Engineer - Engineer";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_ENGINEER_-_ENGINEER_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[0]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "PLACEHOLDER");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								skin.rendererInfos = Array.Empty<CharacterModel.RendererInfo>();
							});
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000010 RID: 16 RVA: 0x000037E8 File Offset: 0x000019E8
		private static void AddEngiBodyEngineer32EODTechSkin()
		{
			string text = "EngiBody";
			string text2 = "Engineer - 32 EOD Tech";
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_ENGINEER_-_32_EOD_TECH_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[2]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "PLACEHOLDER");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								skin.rendererInfos = Array.Empty<CharacterModel.RendererInfo>();
							});
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000011 RID: 17 RVA: 0x00003B64 File Offset: 0x00001D64
		private static void AddLoaderBodyLoaderL0D3rSkin()
		{
			string text = "LoaderBody";
			string text2 = "Loader - L0@d3r";
			int baseSkinIndex = 0;
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_LOADER_-_L0@D3R_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[baseSkinIndex]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "L0@d3r");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
								SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
                                int rendererIndex = 2;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
                                //Create a copy of the default Loader skin
                                Material baseMat = skinController.skins[baseSkinIndex].rendererInfos[rendererIndex].defaultMaterial;
                                Material mat = new Material(baseMat);
                                // Swap the diffuse texture to black out the visor
                                Texture2D newDiffuse = RavemandoPlugin.assetBundle.LoadAsset<Texture2D>("Assets/Loader/Loader - Diffuse - Main.png");
								mat.SetTexture("_MainTex", newDiffuse);
                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[rendererIndex];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                AddToCycle(rendererInfo);
							});
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000012 RID: 18 RVA: 0x00003EE0 File Offset: 0x000020E0
		private static void AddLoaderBodyLoaderClaIcSkin()
		{
			string text = "LoaderBody";
			string text2 = "Loader - Cla$$ic";
			int baseSkinIndex = 1;
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_LOADER_-_CLA$$IC_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[baseSkinIndex]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "Cla$$ic");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
                                SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
                                int rendererIndex = 2;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
                                //Create a copy of the default Loader skin
                                Material baseMat = skinController.skins[baseSkinIndex].rendererInfos[rendererIndex].defaultMaterial;
                                Material mat = new Material(baseMat);
                                // Swap the diffuse texture to black out the visor
                                Texture2D newDiffuse = RavemandoPlugin.assetBundle.LoadAsset<Texture2D>("Assets/Loader/Loader - Diffuse - Main.png");
                                mat.SetTexture("_MainTex", newDiffuse);
                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[rendererIndex];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                AddToCycle(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000013 RID: 19 RVA: 0x0000425C File Offset: 0x0000245C
		private static void AddCaptainBodyCaptainTraptainSkin()
		{
			string text = "CaptainBody";
			string text2 = "Captain - Traptain";
			int baseSkinIndex = 0;
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_CAPTAIN_-_TRAPTAIN_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[baseSkinIndex]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "Traptain");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
                                SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
								int rendererIndex = 5;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
								//Create a copy of the default Captain skin
								Material baseMat = skinController.skins[baseSkinIndex].rendererInfos[rendererIndex].defaultMaterial;
                                Material mat = new Material(baseMat);
                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[rendererIndex];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                cycleRenderers.Add(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000014 RID: 20 RVA: 0x000045D8 File Offset: 0x000027D8
		private static void AddCaptainBodyCaptainRadmiralSkin()
		{
			string text = "CaptainBody";
			string text2 = "Captain - Radmiral";
			int baseSkinIndex = 1;
			try
			{
				GameObject gameObject = BodyCatalog.FindBodyPrefab(text);
				bool flag = !gameObject;
				if (flag)
				{
					RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
					{
						"Failed to add \"",
						text2,
						"\" skin because \"",
						text,
						"\" doesn't exist"
					}));
				}
				else
				{
					ModelLocator component = gameObject.GetComponent<ModelLocator>();
					bool flag2 = !component;
					if (flag2)
					{
						RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
						{
							"Failed to add \"",
							text2,
							"\" skin to \"",
							text,
							"\" because it doesn't have \"ModelLocator\" component"
						}));
					}
					else
					{
						GameObject gameObject2 = component.modelTransform.gameObject;
						ModelSkinController skinController = gameObject2 ? gameObject2.GetComponent<ModelSkinController>() : null;
						bool flag3 = !skinController;
						if (flag3)
						{
							RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
							{
								"Failed to add \"",
								text2,
								"\" skin to \"",
								text,
								"\" because it doesn't have \"ModelSkinController\" component"
							}));
						}
						else
						{
							Renderer[] renderers = gameObject2.GetComponentsInChildren<Renderer>(true);
							SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
							RavemandoPlugin.TryCatchThrow("Icon", delegate
							{
								skin.icon = RavemandoPlugin.assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
							});
							skin.name = text2;
							skin.nameToken = "JACKDOTPNG_SKIN_CAPTAIN_-_RADMIRAL_NAME";
							skin.rootObject = gameObject2;
							RavemandoPlugin.TryCatchThrow("Base Skins", delegate
							{
								skin.baseSkins = new SkinDef[]
								{
									skinController.skins[baseSkinIndex]
								};
							});
							RavemandoPlugin.TryCatchThrow("Unlockable Name", delegate
							{
								skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault((UnlockableDef def) => def.cachedName == "Radmiral");
							});
							RavemandoPlugin.TryCatchThrow("Game Object Activations", delegate
							{
								skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
							});
							RavemandoPlugin.TryCatchThrow("Renderer Infos", delegate
							{
                                SkinDef skinDef = skin;
                                CharacterModel.RendererInfo[] array = new CharacterModel.RendererInfo[1];
                                int num = 0;
                                int rendererIndex = 5;
                                CharacterModel.RendererInfo rendererInfo = default(CharacterModel.RendererInfo);
                                //Create a copy of the default Captain skin
                                Material baseMat = skinController.skins[baseSkinIndex].rendererInfos[rendererIndex].defaultMaterial;
                                Material mat = new Material(baseMat);
                                rendererInfo.defaultMaterial = mat;
                                rendererInfo.defaultShadowCastingMode = 0;
                                rendererInfo.ignoreOverlays = false;
                                rendererInfo.renderer = renderers[rendererIndex];
                                array[num] = rendererInfo;
                                skin.rendererInfos = array;
                                cycleRenderers.Add(rendererInfo);
                            });
							RavemandoPlugin.TryCatchThrow("Mesh Replacements", delegate
							{
								skin.meshReplacements = Array.Empty<SkinDef.MeshReplacement>();
							});
							RavemandoPlugin.TryCatchThrow("Minion Skin Replacements", delegate
							{
								skin.minionSkinReplacements = new SkinDef.MinionSkinReplacement[0];
							});
							RavemandoPlugin.TryCatchThrow("Projectile Ghost Replacements", delegate
							{
								skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
							});
							Array.Resize<SkinDef>(ref skinController.skins, skinController.skins.Length + 1);
							skinController.skins[skinController.skins.Length - 1] = skin;
							BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(gameObject)] = skinController.skins;
						}
					}
				}
			}
			catch (RavemandoPlugin.FieldException ex)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogWarning("Field causing issue: " + ex.Message);
				RavemandoPlugin.InstanceLogger.LogError(ex.InnerException);
			}
			catch (Exception ex2)
			{
				RavemandoPlugin.InstanceLogger.LogWarning(string.Concat(new string[]
				{
					"Failed to add \"",
					text2,
					"\" skin to \"",
					text,
					"\""
				}));
				RavemandoPlugin.InstanceLogger.LogError(ex2);
			}
		}

		// Token: 0x06000015 RID: 21 RVA: 0x00004954 File Offset: 0x00002B54
		private static void TryCatchThrow(string message, Action action)
		{
			try
			{
				if (action != null)
				{
					action();
				}
			}
			catch (Exception innerException)
			{
				throw new RavemandoPlugin.FieldException(message, innerException);
			}
		}

		// Token: 0x04000002 RID: 2
		private static AssetBundle assetBundle;

		// Token: 0x04000003 RID: 3
		private static readonly List<Material> materialsWithRoRShader = new List<Material>();

		// Token: 0x02000003 RID: 3
		private class FieldException : Exception
		{
			// Token: 0x06000018 RID: 24 RVA: 0x000049A1 File Offset: 0x00002BA1
			public FieldException(string message, Exception innerException) : base(message, innerException)
			{
			}
		}
	}
}
