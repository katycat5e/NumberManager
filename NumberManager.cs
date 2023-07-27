using CommandTerminal;
using DV.ThingTypes;
using HarmonyLib;
using SkinManagerMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;

namespace NumberManagerMod
{
    public static partial class NumberManager
    {
        public const string NUM_CONFIG_FILE = "numbering.xml";

        internal static UnityModManager.ModEntry modEntry;
        private static UnityModManager.ModEntry skinManagerEntry;
        private static XmlSerializer serializer;
        public static NMModSettings Settings { get; private set; }

        public static readonly Dictionary<string, NumberConfig> NumberSchemes = new Dictionary<string, NumberConfig>();
        public static readonly Dictionary<string, int> SavedCarNumbers = new Dictionary<string, int>();

        private static byte[] NumberingBundleData = null;
        private static AssetBundle NumberingBundle = null;
        public static Shader NumShader { get; private set; } = null;
        public static int? LastSteamerNumber { get; private set; } = null;

        private static readonly Dictionary<string, Shader> DefaultShaders = 
            new Dictionary<string, Shader>();

        private static string GetDefaultShaderKey(TrainCarLivery livery, string textureName) => $"{livery.id}_{textureName}";
        private static string GetSchemeKey(TrainCarLivery livery, string skinName) => $"{livery.id}_{skinName}";

        #region Initialization

        // Mod entry point
        public static bool Load( UnityModManager.ModEntry entry )
        {
            modEntry = entry;

            // We can't do anything without Skin Manager
            skinManagerEntry = UnityModManager.FindMod("SkinManagerMod");
            if( skinManagerEntry == null )
            {
                modEntry.Logger.Error("Couldn't find Skin Manager, aborting load");
                return false;
            }

            // Initialize settings
            Settings = UnityModManager.ModSettings.Load<NMModSettings>(modEntry);
            modEntry.OnGUI = DrawGUI;
            modEntry.OnSaveGUI = SaveGUI;

            // we only need one instance of the serializer
            serializer = new XmlSerializer(typeof(NumberConfig));

            // attempt to load the numbering shader
            string shaderBundlePath = Path.Combine(modEntry.Path, "numbering");
            modEntry.Logger.Log($"Attempting to load numbering shader from \"{shaderBundlePath}\"");

            NumberingBundleData = File.ReadAllBytes(shaderBundlePath);
            NumberingBundle = AssetBundle.LoadFromMemory(NumberingBundleData);

            if( NumberingBundle != null )
            {
                NumShader = NumberingBundle.LoadAsset<Shader>("Assets/NumberSurface.shader");
                if( NumShader == null )
                {
                    modEntry.Logger.Error("Failed to load numbering shader from asset bundle");
                    return false;
                }
                else
                {
                    modEntry.Logger.Log($"Loaded numbering shader {NumShader.name}");
                    //UnityEngine.Object.DontDestroyOnLoad(NumShader);
                }
            }
            else
            {
                modEntry.Logger.Error("Failed to load numbering asset bundle");
                return false;
            }

            LoadSchemes();
            SkinProvider.SkinsLoaded += OnSkinsLoaded;

            try
            {
                var command = new CommandInfo()
                {
                    name = "NM.ReloadConfig",
                    proc = ReloadConfig,
                    min_arg_count = 0,
                    max_arg_count = 0,
                    help = "Reload all number configs",
                };
                Terminal.Shell.AddCommand(command);
                Terminal.Autocomplete.Register(command);
            }
            catch
            {
                modEntry.Logger.Error("Failed to register terminal commands");
            }

            var harmony = new Harmony("cc.foxden.number_manager");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            return true;
        }

        private static void OnSkinsLoaded()
        {
            LoadSchemes();
            ReapplyNumbers();
        }

        private static void ReloadConfig(CommandArg[] args)
        {
            LoadSchemes();
            ReapplyNumbers();
        }

        private static void ReapplyNumbers()
        {
            var allCars = UnityEngine.Object.FindObjectsOfType<TrainCar>();
            foreach (var car in allCars)
            {
                if (SavedCarNumbers.TryGetValue(car.CarGUID, out int num))
                {
                    ApplyNumbering(car, num);
                }
            }
        }

        private static void LoadSchemes()
        {
            DefaultShaders.Clear();
            NumberSchemes.Clear();

            // Check each skin under each car type for a numbering config file
            foreach (var group in SkinProvider.AllSkinGroups)
            {
                foreach (var skin in group.Skins)
                {
                    LoadSchemeFromSkin(group.TrainCarType, skin);
                }
            }
        }

        private static void LoadSchemeFromSkin(TrainCarLivery carType, Skin skin)
        {
            if (string.IsNullOrEmpty(skin?.Path)) return;

            string configFile = Path.Combine(skin.Path, NUM_CONFIG_FILE);
            NumberConfig config = null;

            if (File.Exists(configFile))
            {
                config = LoadConfig(carType, skin, configFile);
            }

            string schemeKey = GetSchemeKey(carType, skin.Name);
            if (config != null)
            {
                NumberSchemes[schemeKey] = config;

                // get default shader for targeted material
                var carPrefabObj = carType.prefab;

                if (carPrefabObj != null)
                {
                    foreach (MeshRenderer renderer in carPrefabObj.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (renderer.material.HasProperty("_MainTex") && (renderer.material.GetTexture("_MainTex") is Texture2D mainTex))
                        {
                            string shaderKey = GetDefaultShaderKey(carType, mainTex.name);
                            if (string.Equals(mainTex.name, config.TargetTexture) && !DefaultShaders.ContainsKey(shaderKey))
                            {
                                DefaultShaders.Add(shaderKey, renderer.material.shader);
                            }
                        }
                    }
                }
            }
            else if (NumberSchemes.ContainsKey(schemeKey))
            {
                NumberSchemes.Remove(schemeKey);
            }
        }

        public static NumberConfig LoadConfig(TrainCarLivery carType, Skin skin, string configPath)
        {
            NumberConfig config = null;

            try
            {
                using(var stream = new FileStream(configPath, FileMode.Open))
                {
                    config = serializer.Deserialize(stream) as NumberConfig;
                }
            }
            catch (Exception ex)
            {
                modEntry.Logger.Warning($"Error loading numbering config in \"{configPath}\": {ex.Message}");
                return null;
            }

            if (config != null)
            {
                string dir = Path.GetDirectoryName(configPath);

                try
                {
                    config.Initialize(carType, dir);
                    config.Skin = skin;
                }
                catch (Exception ex)
                {
                    modEntry.Logger.Error($"Exception when loading numbering config for {skin.Name}:\n{ex.Message}");
                }
            }

            return config;
        }

        #endregion

        #region Number Application

        private static int GetCarIdNumber( string carId )
        {
            string idNum = carId.Substring(carId.Length - 3);
            return int.Parse(idNum);
        }

        public static void SetCarNumber(string carGUID, int number)
        {
            SavedCarNumbers[carGUID] = number;
        }

        public static int GetCurrentCarNumber(TrainCar car)
        {
            if (SavedCarNumbers.TryGetValue(car.CarGUID, out int n)) return n;
            else return GetNewCarNumber(car);
        }

        public static int GetNewCarNumber(TrainCar car)
        {
            // Previously un-numbered car
            // A new tender should try to match engine if possible
            if (CarTypes.IsTender(car.carLivery) && LastSteamerNumber.HasValue)
            {
                return LastSteamerNumber.Value;
            }
            else
            {
                var scheme = GetScheme(car);
                if (Settings.PreferCarId && (scheme?.ForceRandom != true))
                {
                    int offset = (Settings.AllowCarIdOffset && (scheme != null)) ? scheme.Offset : 0;
                    return GetCarIdNumber(car.ID) + offset;
                }
                else
                {
                    return (scheme != null) ? scheme.GetRandomNum() : 0;
                }
            }
        }

        public static NumberConfig GetScheme(TrainCar car)
        {
            var skin = SkinManager.GetCurrentCarSkin(car);
            if (skin == null) return null;
            return GetScheme(car.carLivery, skin.Name);
        }

        public static NumberConfig GetScheme(TrainCarLivery carType, string skinName)
        {
            var key = GetSchemeKey(carType, skinName);

            if (NumberSchemes.TryGetValue(key, out var config))
            {
                return config;
            }
            return null;
        }

        /// <summary>
        /// Apply a new number to a car
        /// </summary>
        public static void ApplyNumbering(TrainCar car, int number)
        {
            Dictionary<MeshRenderer, DefaultTexInfo> texDict = null;
            SkinManager_ReplaceTexture_Patch.Prefix(car, ref texDict);
            ApplyNumbering(car, number, texDict);
        }

        /// <summary>
        /// Set number with default texture data (for internal use)
        /// </summary>
        internal static void ApplyNumbering(TrainCar car, int number, Dictionary<MeshRenderer, DefaultTexInfo> defaultTexDict)
        {
            var numScheme = GetScheme(car);

            // make sure Unity didn't chuck out the asset bundle
            if( NumShader == null )
            {
                if( NumberingBundle == null ) NumberingBundle = AssetBundle.LoadFromMemory(NumberingBundleData);
                NumShader = NumberingBundle.LoadAsset<Shader>("Assets/NumberSurface.shader");
            }

            if( (numScheme == null) || (NumShader == null) )
            {
                // nothing to apply
                foreach( var renderer in car.gameObject.GetComponentsInChildren<MeshRenderer>() )
                {
                    if( !renderer.material.HasProperty("_MainTex") ) continue;

                    string texName = renderer.material.GetTexture("_MainTex")?.name;
                    if (string.IsNullOrEmpty(texName)) continue;

                    string shaderKey = GetDefaultShaderKey(car.carLivery, texName);
                    if (DefaultShaders.TryGetValue(shaderKey, out Shader defShader) )
                    {
                        renderer.material.shader = defShader;
                    }
                }
                return;
            }

            modEntry.Logger.Log($"Applying number {number} to {car.ID}");
            SetCarNumber(car.CarGUID, number);

            if (CarTypes.IsSteamLocomotive(car.carLivery))
            {
                LastSteamerNumber = number;
            }
            else
            {
                LastSteamerNumber = null;
            }

            // Check if the texture we're targeting is supplied by the skin
            var tgtTex = numScheme.Skin.GetTexture(numScheme.TargetTexture)?.TextureData;
            NumShaderProps shaderProps = null;

            if( tgtTex != null )
            {
                shaderProps = GetShaderProps(numScheme, number, tgtTex.width, tgtTex.height);
            }
            // otherwise we'll be lazy and figure out the width/height when we find the default texture

            var renderers = car.gameObject.GetComponentsInChildren<MeshRenderer>();

            foreach( var renderer in renderers )
            {
                if( !renderer.material ) continue;

                DefaultTexInfo defaultTex = defaultTexDict[renderer];

                // check if this is the target for numbering
                if( string.Equals(numScheme.TargetTexture, defaultTex.Name) )
                {
                    if( shaderProps == null )
                    {
                        shaderProps = GetShaderProps(numScheme, number, defaultTex.Width, defaultTex.Height);
                    }

                    renderer.material.shader = NumShader;
                    renderer.material.SetTexture("_FontTex", numScheme.FontTexture);

                    shaderProps.ApplyTo(renderer.material);
                }
            }
        }

        #endregion

        #region Load/Save

        public const string SAVE_DATA_KEY = "Mod_NumManager";

        // Save Configuration:

        //  SaveData
        //      Mod_NumManager
        //          carNumbers[]
        //              entry
        //                  guid
        //                  number
        //              entry
        //                  guid
        //                  number

        public struct CarSaveEntry
        {
            public string guid;
            public int? number;

            public CarSaveEntry(string guid, int? number)
            {
                this.guid = guid;
                this.number = number;
            }
        }

        public class NumberData
        {
            public CarSaveEntry[] carNumbers;
        }

        public static void LoadSaveData()
        {
            var numberData = SaveGameManager.Instance.data.GetObject<NumberData>(SAVE_DATA_KEY);

            if ((numberData != null) && (numberData.carNumbers != null))
            {
                modEntry.Logger.Log($"Loaded data, {numberData.carNumbers.Length} entries");
                foreach (var entry in numberData.carNumbers)
                {
                    if (!string.IsNullOrEmpty(entry.guid) && entry.number.HasValue)
                    {
                        SetCarNumber(entry.guid, entry.number.Value);
                    }
                }
            }
        }

        private static CarSaveEntry CreateSaveEntry(KeyValuePair<string, int> kvp)
        {
            return new CarSaveEntry(kvp.Key, kvp.Value);
        }

        public static void SaveData()
        {
            var numberData = new NumberData()
            {
                carNumbers = SavedCarNumbers.Select(CreateSaveEntry).ToArray()
            };

            SaveGameManager.Instance.data.SetObject(SAVE_DATA_KEY, numberData);
        }

        #endregion

        #region Settings

        static void DrawGUI( UnityModManager.ModEntry entry )
        {
            Settings.Draw(entry);
        }

        static void SaveGUI( UnityModManager.ModEntry entry )
        {
            Settings.Save(entry);
        }

        #endregion
    }
}
