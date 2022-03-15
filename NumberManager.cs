using DV.JObjectExtstensions;
using HarmonyLib;
using Newtonsoft.Json.Linq;
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
    using SchemeKey = Tuple<TrainCarType, string>;

    public static partial class NumberManager
    {
        public const string NUM_CONFIG_FILE = "numbering.xml";

        internal static UnityModManager.ModEntry modEntry;
        private static UnityModManager.ModEntry skinManagerEntry;
        private static XmlSerializer serializer;
        public static NMModSettings Settings { get; private set; }

        private static string skinsFolder;

        public static readonly Dictionary<SchemeKey, NumberConfig> NumberSchemes = new Dictionary<SchemeKey, NumberConfig>();
        public static readonly Dictionary<string, int> SavedCarNumbers = new Dictionary<string, int>();

        private static byte[] NumberingBundleData = null;
        private static AssetBundle NumberingBundle = null;
        public static Shader NumShader { get; private set; } = null;
        public static int? LastSteamerNumber { get; private set; } = null;

        private static readonly Dictionary<TrainCarType, Dictionary<string, Shader>> DefaultShaders = 
            new Dictionary<TrainCarType, Dictionary<string, Shader>>();

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

            skinsFolder = Path.Combine(skinManagerEntry.Path, "Skins");

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
                }
            }
            else
            {
                modEntry.Logger.Error("Failed to load numbering asset bundle");
                return false;
            }

            LoadSchemes();

            var harmony = new Harmony("cc.foxden.number_manager");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            return true;
        }

        private static void LoadSchemes()
        {
            if( !Directory.Exists(skinsFolder) ) return;

            // Check each skin under each car type for a numbering config file
            foreach ((TrainCarType carType, _) in SkinManager.EnabledCarTypes )
            {
                var loadedSkins = SkinManager.GetSkinsForType(carType);

                var shaderDict = new Dictionary<string, Shader>();
                DefaultShaders.Add(carType, shaderDict);

                foreach (var skin in loadedSkins)
                {
                    if (string.IsNullOrEmpty(skin?.Path)) continue;

                    string configFile = Path.Combine(skin.Path, NUM_CONFIG_FILE);
                    NumberConfig config = null;

                    if (File.Exists(configFile))
                    {
                        config = LoadConfig(carType, skin, configFile);
                    }

                    if (config != null)
                    {
                        // get default shader for targeted material
                        var carPrefabObj = CarTypes.GetCarPrefab(carType);

                        if (carPrefabObj != null)
                        {
                            foreach (MeshRenderer renderer in carPrefabObj.GetComponentsInChildren<MeshRenderer>())
                            {
                                if (renderer.material.HasProperty("_MainTex") && (renderer.material.GetTexture("_MainTex") is Texture2D mainTex))
                                {
                                    if (string.Equals(mainTex.name, config.TargetTexture) && !shaderDict.ContainsKey(mainTex.name))
                                    {
                                        shaderDict.Add(mainTex.name, renderer.material.shader);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static NumberConfig LoadConfig( TrainCarType carType, Skin skin, string configPath )
        {
            NumberConfig config = null;

            try
            {
                using( var stream = new FileStream(configPath, FileMode.Open) )
                {
                    config = serializer.Deserialize(stream) as NumberConfig;
                }
            }
            catch( Exception ex )
            {
                modEntry.Logger.Warning($"Error loading numbering config in \"{configPath}\": {ex.Message}");
                return null;
            }

            if( config != null )
            {
                string dir = Path.GetDirectoryName(configPath);

                try
                {
                    config.Initialize(dir);
                    config.Skin = skin;
                    NumberSchemes.Add(new SchemeKey(carType, skin.Name), config);
                }
                catch( Exception ex )
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
            if (CarTypes.IsTender(car.carType) && LastSteamerNumber.HasValue)
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
            return GetScheme(car.carType, skin.Name);
        }

        public static NumberConfig GetScheme( TrainCarType carType, string skinName )
        {
            var key = new SchemeKey(carType, skinName);

            if( NumberSchemes.TryGetValue(key, out var config) ) return config;
            else return null;
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
                    if( !string.IsNullOrEmpty(texName) &&
                        DefaultShaders.TryGetValue(car.carType, out var shaderDict) &&
                        shaderDict.TryGetValue(texName, out Shader defShader) )
                    {
                        renderer.material.shader = defShader;
                    }
                }
                return;
            }

            modEntry.Logger.Log($"Applying number {number} to {car.ID}");
            SetCarNumber(car.CarGUID, number);

            if (CarTypes.IsSteamLocomotive(car.carType))
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
            var numberData = SaveGameManager.data.GetObject<NumberData>(SAVE_DATA_KEY);

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

            SaveGameManager.data.SetObject(SAVE_DATA_KEY, numberData);
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
