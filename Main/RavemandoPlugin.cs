using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

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

                Color newColor = cycleColors[colorIndex] * strengthMultiplier.Value;
                materialPropertyBlock.SetColor("_EmColor", newColor);

                for (int i = 0; i < cycleRenderers.Count; i++)
                {
                    CharacterModel.RendererInfo renderer = cycleRenderers[i];

                    InstanceLogger.LogDebug($"Setting color for renderer {i} to {cycleColors[colorIndex]} with strength multiplier {strengthMultiplier.Value}");

                    renderer.renderer.SetPropertyBlock(materialPropertyBlock);
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
            AddRavemando();
            AddHornet();
            AddLoader();
            AddClassic();
            AddTraptain();
            AddRadmiral();
            AddRailgunner();

            StartCycle();
        }

        private static int CheckBodyPrefabValidity(string bodyPrefabName, string skinName, out GameObject bodyPrefab, out GameObject modelTransform, out ModelSkinController skinController)
        {
            InstanceLogger.LogDebug("Getting Body Prefab");
            bodyPrefab = BodyCatalog.FindBodyPrefab(bodyPrefabName);
            skinController = null;
            modelTransform = null;

            if (!bodyPrefab)
            {
                InstanceLogger.LogError($"Failed to add \"{skinName}\" skin because \"{bodyPrefabName}\" doesn't exist");
                return -1;
            }

            InstanceLogger.LogDebug("Getting Model Locator");
            ModelLocator component = bodyPrefab.GetComponent<ModelLocator>();
            if (!component)
            {
                InstanceLogger.LogError($"Failed to add \"{skinName}\" skin to \"{bodyPrefabName}\" because it doesn't have \"ModelLocator\" component");
                return -2;
            }


            InstanceLogger.LogDebug("Getting Model Transform");
            modelTransform = component.modelTransform.gameObject;
            skinController = modelTransform ? modelTransform.GetComponent<ModelSkinController>() : null;
            if (!skinController)
            {
                InstanceLogger.LogError($"Failed to add \"{skinName}\" skin to \"{bodyPrefabName}\" because it doesn't have \"ModelSkinController\" component");
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

        private static void AddSimpleSkin(string bodyPrefabName, string skinName, string skinNameToken, Sprite icon, int baseSkinIndex, int rendererIndex, Material replacementMaterial)
        {
            GameObject bodyPrefab = null;
            GameObject modelTransform = null;
            ModelSkinController skinController = null;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material instancedMat = new Material(replacementMaterial);

            InstanceLogger.LogInfo("Loaded material: " + instancedMat.name);

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

        private static void AddRavemando()
        {
            string bodyPrefabName = "CommandoBody";
            string skinName = "Ravemando";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_RAVEMANDO_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Commando/01 - Ravemando/Icon.png");
            int baseSkinIndex = 0;

            //Material mat = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/01 - Ravemando/Material.mat");
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Commando.matCommandoDualies_mat).WaitForCompletion();
            int rendererIndex = 6;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);
        }

        private static void AddHornet()
        {
            string bodyPrefabName = "CommandoBody";
            string skinName = "H0rn3t";
            string skinNameToken = "JACKDOTPNG_SKIN_COMMANDO_-_H0RN3T_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Commando/02 - H0rn3t/Icon.png");
            int baseSkinIndex = 1;
            //var mat = RavemandoPlugin.assetBundle.LoadAsset<Material>("Assets/Commando/01 - Ravemando/Material.mat");
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Commando.matCommandoDualiesAlt_mat).WaitForCompletion();
            int rendererIndex = 6;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);
        }

        private static void AddLoader()
        {
            string bodyPrefabName = "LoaderBody";
            string skinName = "L0@d3r";
            string skinNameToken = "JACKDOTPNG_SKIN_LOADER_-_LOADER_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
            int baseSkinIndex = 0;
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Loader.matLoaderPilotDiffuse_mat).WaitForCompletion();
            int rendererIndex = 2;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);
        }

        private static void AddClassic()
        {
            string bodyPrefabName = "LoaderBody";
            string skinName = "ClA$$ic";
            string skinNameToken = "JACKDOTPNG_SKIN_LOADER_-_CLASSIC_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
            int baseSkinIndex = 1;
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Loader.matLoaderPilotDiffuse_mat).WaitForCompletion();
            int rendererIndex = 2;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);
        }

        private static void AddTraptain()
        {
            string bodyPrefabName = "CaptainBody";
            string skinName = "Traptain";
            string skinNameToken = "JACKDOTPNG_SKIN_CAPTAIN_-_TRAPTAIN_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
            int baseSkinIndex = 0;
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Captain.matCaptainRobotBits_mat).WaitForCompletion();
            int rendererIndex = 5;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);
        }

        private static void AddRadmiral()
        {
            string bodyPrefabName = "CaptainBody";
            string skinName = "Radmiral";
            string skinNameToken = "JACKDOTPNG_SKIN_CAPTAIN_-_RADMIRAL_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
            int baseSkinIndex = 1;
            Material loadedMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Loader.matLoaderPilotDiffuseAlt_mat).WaitForCompletion();
            int rendererIndex = 5;

            AddSimpleSkin(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, rendererIndex, loadedMat);

        }

        private static void AddRailgunner()
        {
            string bodyPrefabName = "RailgunnerBody";
            string skinName = "Railgunner";
            string skinNameToken = "JACKDOTPNG_SKIN_RAILGUNNER_-_RAILGUNNER_NAME";
            Sprite icon = assetBundle.LoadAsset<Sprite>("Assets/Placeholder Icon.png");
            int baseSkinIndex = 0;
            int rendererIndex = 4;

            GameObject bodyPrefab;
            GameObject modelTransform;
            ModelSkinController skinController;

            SkinDefInfo skinDefInfo = CreateNewSkinDefInfo(bodyPrefabName, skinName, skinNameToken, icon, baseSkinIndex, out bodyPrefab, out modelTransform, out skinController);

            // Railgunner uses many different renderers, so we're gonna have to prepare for that
            CharacterModel.RendererInfo[] newRendererInfos = new CharacterModel.RendererInfo[1];
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>(true);

            Material baseMat = Addressables.LoadAssetAsync<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_DLC1_Railgunner.matRailGunnerBase_mat).WaitForCompletion();
            Material instancedMat = new Material(baseMat);

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
