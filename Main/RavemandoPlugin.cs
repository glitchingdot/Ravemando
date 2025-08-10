using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using IL.RoR2.ContentManagement;
using MonoMod.RuntimeDetour.HookGen;
using R2API;
using RoR2;

using RiskOfOptions;

using UnityEngine;
using UnityEngine.AddressableAssets;
using RiskOfOptions.Options;

namespace Ravemando
{
    [BepInDependency(LanguageAPI.PluginGUID)]

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    [BepInDependency("com.rune580.riskofoptions")]

    public class RavemandoPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "jackdotpng";
        public const string PluginName = "Ravemando";
        public const string PluginVersion = "2.0.3";

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

        private static List<ConfigEntry<Color>> customColorConfigs = new List<ConfigEntry<Color>> { };

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

            switch (colorSet.Value)
            {
                case ColorSet.Default:
                    //If this fails (colorSet not set), it will fail safe because it will default to to base colors without needing config input
                    break;
                case ColorSet.Custom:
                    cycleColors = GetCustomColors();
                    break;
            }

            for (; ; )
            {
                if (colorIndex >= cycleColors.Count)
                {
                    colorIndex = 0;
                }

                for (int i = 0; i < cycleRenderers.Count; i++)
                {
                    CharacterModel.RendererInfo renderer = cycleRenderers[i];

                    InstanceLogger.LogDebug($"Setting color for renderer {i} to {cycleColors[colorIndex]} with strength multiplier {strengthMultiplier.Value}");

                    Material mat = renderer.defaultMaterial;
                    mat.SetColor("_EmColor", cycleColors[colorIndex] * strengthMultiplier.Value);
                    renderer.defaultMaterial = mat;
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

        internal static ManualLogSource InstanceLogger
        {
            get
            {
                RavemandoPlugin instance = Instance;
                return instance != null ? instance.Logger : null;
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



            Instance = this;
            using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ravemando.jackdotpngravemando"))
            {
                assetBundle = AssetBundle.LoadFromStream(manifestResourceStream);
            }


            BodyCatalog.availability.CallWhenAvailable(new Action(BodyCatalogInit));
            //RavemandoPlugin.ReplaceShaders();
        }

        private static void ReplaceShaders()
        {
            LoadMaterialsWithReplacedShader("RoR2/Base/Shaders/HGStandard.shader", new string[]
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
                Material material = assetBundle.LoadAsset<Material>(text);
                material.shader = shader;
                materialsWithRoRShader.Add(material);
            }
        }

        private static void BodyCatalogInit()
        {
            MethodInfo method = typeof(SkinDef).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

            AddRavemando();
            AddHornet();
            AddTraptain();
            AddRadmiral();

            StartCycle();
        }

        private static int CheckBodyPrefabValidity(string bodyPrefabName, string skinName, out GameObject bodyPrefab, out GameObject modelTransform, out ModelSkinController skinController)
        {
            InstanceLogger.LogInfo("Getting Body Prefab");
            bodyPrefab = BodyCatalog.FindBodyPrefab(bodyPrefabName);
            skinController = null;
            modelTransform = null;

            if (!bodyPrefab)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin because \"{bodyPrefabName}\" doesn't exist");
                return -1;
            }

            InstanceLogger.LogInfo("Getting Model Locator");
            ModelLocator component = bodyPrefab.GetComponent<ModelLocator>();
            if (!component)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyPrefabName}\" because it doesn't have \"ModelLocator\" component");
                return -2;
            }


            InstanceLogger.LogInfo("Getting Model Transform");
            modelTransform = component.modelTransform.gameObject;
            skinController = modelTransform ? modelTransform.GetComponent<ModelSkinController>() : null;
            if (!skinController)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyPrefabName}\" because it doesn't have \"ModelSkinController\" component");
                return -3;
            }

            return 0;
        }

#pragma warning disable CS0612 // Type or member is obsolete

        private static SkinDefInfo CreateNewSkinDefInfo(string bodyPrefabName, string skinName, string skinNameToken, Sprite icon, int baseSkinIndex, out GameObject bodyPrefab, out GameObject modelTransform, out ModelSkinController skinController)
        {
            LanguageAPI.Add(skinNameToken, skinName);

            InstanceLogger.LogInfo("Checking prefab validity!");
            if (CheckBodyPrefabValidity(bodyPrefabName, skinName, out bodyPrefab, out modelTransform, out skinController) != 0)
            {
                InstanceLogger.LogError("Failed to add Ravemando skin due to invalid prefab!");
                return default;
            }

            SkinDefInfo skinDefInfo = default;

            SkinDef[] baseSkins = new SkinDef[1];
            baseSkins[0] = skinController.skins[baseSkinIndex];
            skinDefInfo.BaseSkins = baseSkins;

            skinDefInfo.MinionSkinReplacements = [];
            skinDefInfo.ProjectileGhostReplacements = [];
            skinDefInfo.MeshReplacements = [];

            skinDefInfo.Name = skinNameToken;
            skinDefInfo.NameToken = skinNameToken;

            skinDefInfo.Icon = icon;
            skinDefInfo.RootObject = modelTransform;

            return skinDefInfo;
        }

        private static void AddSkinToSkinController(ModelSkinController skinController, SkinDefInfo skinDefInfo)
        {
            SkinDef newSkin = Skins.CreateNewSkinDef(skinDefInfo);

            var skinsArray = skinController.skins;
            Array.Resize(ref skinsArray, skinsArray.Length + 1);
            skinsArray[skinsArray.Length - 1] = newSkin;
            skinController.skins = skinsArray;

            InstanceLogger.LogInfo($"Added {skinDefInfo.Name} Skin!");
        }

        private static void AddRavemando()
        {
            string bodyPrefabName = "CommandoBody";
            string skinName = "Ravemando";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_RAVEMANDO_NAME";

            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Commando/01 - Ravemando/Icon.png");

            int baseSkinIndex = 0;

            GameObject bodyPrefab = null;
            GameObject modelTransform = null;
            ModelSkinController skinController = null;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            //var mat = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/01 - Ravemando/Material.mat");

            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Commando.matCommandoDualies_mat).WaitForCompletion();
            Material instancedMat = new Material(loadedMat);

            InstanceLogger.LogInfo("Loaded material: " + instancedMat.name);

            int rendererIndex = 6;

            newRendererInfos[0] = new CharacterModel.RendererInfo
            {
                defaultMaterial = instancedMat,
                defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                ignoreOverlays = false,
                renderer = renderers[rendererIndex],
            };

            AddToCycle(newRendererInfos[0]);
            skinDefInfo.RendererInfos = newRendererInfos;

            AddSkinToSkinController(skinController, skinDefInfo);
        }

        private static void AddHornet()
        {
            string bodyPrefabName = "CommandoBody";
            string skinName = "H0rn3t";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_H0RN3T_NAME";

            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Commando/02 - H0rn3t/Icon.png");

            int baseSkinIndex = 0;

            GameObject bodyPrefab = null;
            GameObject modelTransform = null;
            ModelSkinController skinController = null;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            //var mat = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/01 - Ravemando/Material.mat");

            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Commando.matCommandoDualiesAlt_mat).WaitForCompletion();
            Material instancedMat = new Material(loadedMat);

            InstanceLogger.LogInfo("Loaded material: " + instancedMat.name);

            int rendererIndex = 6;

            newRendererInfos[0] = new CharacterModel.RendererInfo
            {
                defaultMaterial = instancedMat,
                defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                ignoreOverlays = false,
                renderer = renderers[rendererIndex],
            };

            AddToCycle(newRendererInfos[0]);
            skinDefInfo.RendererInfos = newRendererInfos;

            AddSkinToSkinController(skinController, skinDefInfo);
        }

        private static void AddTraptain()
        {
            string bodyPrefabName = "CaptainBody";
            string skinName = "Traptain";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_TRAPTAIN_NAME";

            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");

            int baseSkinIndex = 0;

            GameObject bodyPrefab = null;
            GameObject modelTransform = null;
            ModelSkinController skinController = null;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Captain.matCaptain_mat).WaitForCompletion();
            Material instancedMat = new Material(loadedMat);

            InstanceLogger.LogInfo("Loaded material: " + instancedMat.name);

            int rendererIndex = 5;

            newRendererInfos[0] = new CharacterModel.RendererInfo
            {
                defaultMaterial = instancedMat,
                defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                ignoreOverlays = false,
                renderer = renderers[rendererIndex],
            };

            AddToCycle(newRendererInfos[0]);
            skinDefInfo.RendererInfos = newRendererInfos;

            AddSkinToSkinController(skinController, skinDefInfo);
        }

        private static void AddRadmiral()
        {
            string bodyPrefabName = "CaptainBody";
            string skinName = "Radmiral";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_RADMIRAL_NAME";

            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");

            int baseSkinIndex = 1;

            GameObject bodyPrefab = null;
            GameObject modelTransform = null;
            ModelSkinController skinController = null;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Captain.matCaptain_mat).WaitForCompletion();
            Material instancedMat = new Material(loadedMat);

            InstanceLogger.LogInfo("Loaded material: " + instancedMat.name);

            int rendererIndex = 5;

            newRendererInfos[0] = new CharacterModel.RendererInfo
            {
                defaultMaterial = instancedMat,
                defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                ignoreOverlays = false,
                renderer = renderers[rendererIndex],
            };

            AddToCycle(newRendererInfos[0]);
            skinDefInfo.RendererInfos = newRendererInfos;

            AddSkinToSkinController(skinController, skinDefInfo);
        }

#pragma warning restore CS0612 // Type or member is obsolete


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
                throw new FieldException(message, innerException);
            }
        }

        private static AssetBundle assetBundle;

        private static readonly List<Material> materialsWithRoRShader = new List<Material>();

        private class FieldException : Exception
        {
            public FieldException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}
