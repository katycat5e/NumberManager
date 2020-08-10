using DV.JObjectExtstensions;
using Harmony12;
using Newtonsoft.Json.Linq;
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
    using SM_Main = SkinManagerMod.Main;

    public static partial class NumberManager
    {
        public const string NUM_CONFIG_FILE = "numbering.xml";

        private static UnityModManager.ModEntry modEntry;
        private static UnityModManager.ModEntry skinManagerEntry;
        private static XmlSerializer serializer;

        private static string skinsFolder;

        public static readonly Dictionary<SchemeKey, NumberConfig> NumberSchemes = new Dictionary<SchemeKey, NumberConfig>();
        public static readonly Dictionary<string, int> SavedCarNumbers = new Dictionary<string, int>();

        public static Shader NumShader { get; private set; } = null;
        public static int LastSteamerNumber { get; private set; } = -1;

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

            skinsFolder = Path.Combine(skinManagerEntry.Path, "Skins");

            // we only need one instance of the serializer
            serializer = new XmlSerializer(typeof(NumberConfig));

            // attempt to load the numbering shader
            string shaderBundlePath = Path.Combine(modEntry.Path, "numbering");
            modEntry.Logger.Log($"Attempting to load numbering shader from \"{shaderBundlePath}\"");

            var bytes = File.ReadAllBytes(shaderBundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if( bundle != null )
            {
                NumShader = bundle.LoadAsset<Shader>("Assets/NumSurface.shader");
                if( NumShader == null )
                {
                    modEntry.Logger.Error("Failed to load numbering shader from asset bundle");
                    return false;
                }
            }
            else
            {
                modEntry.Logger.Error("Failed to load numbering asset bundle");
                return false;
            }

            LoadSchemes();

            var harmony = HarmonyInstance.Create("cc.foxden.number_manager");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            return true;
        }

        private static void LoadSchemes()
        {
            if( !Directory.Exists(skinsFolder) ) return;

            // Check each skin under each car type for a numbering config file
            foreach( var prefab in SM_Main.prefabMap )
            {
                string carTypeDir = Path.Combine(skinsFolder, prefab.Value);

                if( Directory.Exists(carTypeDir) )
                {
                    foreach( string skinDir in Directory.GetDirectories(carTypeDir) )
                    {
                        string configFile = Path.Combine(skinDir, NUM_CONFIG_FILE);

                        if( File.Exists(configFile) )
                        {
                            LoadConfig(prefab.Key, Path.GetFileName(skinDir), configFile);
                        }
                    }
                }
            }
        }

        public static void LoadConfig( TrainCarType carType, string skinName, string configPath )
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
                return;
            }

            if( config != null )
            {
                string dir = Path.GetDirectoryName(configPath);
                if( config.Initialize(dir) )
                {
                    NumberSchemes.Add(new SchemeKey(carType, skinName), config);
                }
            }
        }

        #endregion

        #region Number Application

        private static bool TryGetAssignedSkin( TrainCar car, out SkinManagerMod.Skin skin )
        {
            skin = null;

            if( SM_Main.trainCarState.TryGetValue(car.logicCar.carGuid, out string skinName) )
            {
                if( SM_Main.skinGroups.TryGetValue(car.carType, out var group) )
                {
                    skin = group.GetSkin(skinName);
                    return (skin != null);
                }
            }

            return false;
        }

        public static NumberConfig GetScheme( TrainCarType carType, string skinName )
        {
            var key = new SchemeKey(carType, skinName);

            if( NumberSchemes.TryGetValue(key, out var config) ) return config;
            else return null;
        }

        public static void ApplyNumbering( TrainCar car, Dictionary<MeshRenderer, string> defaultTexDict )
        {
            if( !TryGetAssignedSkin(car, out var skin) ) return;
            var numScheme = GetScheme(car.carType, skin.name);

            if( (numScheme == null) || (NumShader == null) ) return; // nothing to apply

            int carNumber;

            // Tender should try to match engine if possible
            if( CarTypes.IsTender(car.carType) && numScheme.IsValidNumber(LastSteamerNumber) )
            {
                carNumber = LastSteamerNumber;
            }
            else
            {
                carNumber = numScheme.GetRandomNum();
            }

            modEntry.Logger.Log($"Applying number {carNumber} to {car.ID}");
            SetCarNumber(car.CarGUID, carNumber);

            if( !skin.ContainsTexture(numScheme.TargetTexture) )
            {
                modEntry.Logger.Warning($"Couldn't apply number to {car.ID}; skin does not contain target texture for num scheme");
                return;
            }

            var tgtTex = skin.GetTexture(numScheme.TargetTexture).textureData;
            var shaderProps = GetShaderProps(numScheme, carNumber, tgtTex.width, tgtTex.height);

            if( CarTypes.IsSteamLocomotive(car.carType) )
            {
                LastSteamerNumber = carNumber;
            }
            else
            {
                LastSteamerNumber = -1;
            }

            var renderers = car.gameObject.GetComponentsInChildren<MeshRenderer>();

            foreach( var renderer in renderers )
            {
                if( !renderer.material ) continue;

                string diffuseName = defaultTexDict[renderer];

                // check if this is the target for numbering
                if( string.Equals(numScheme.TargetTexture, diffuseName) )
                {
                    renderer.material.shader = NumShader;
                    renderer.material.SetTexture("_FontTex", numScheme.FontTexture);
                    shaderProps.ApplyTo(renderer.material);
                }
            }
        }

        #endregion

        #region Load/Save

        // Load/save number methods
        public static void SetCarNumber( string carGUID, int number )
        {
            SavedCarNumbers[carGUID] = number;
        }

        public static int GetSavedCarNumber( string carGUID )
        {
            if( SavedCarNumbers.TryGetValue(carGUID, out int n) ) return n;
            else return -1;
        }

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

        public static void LoadSaveData()
        {
            var numberData = SaveGameManager.data.GetJObject(SAVE_DATA_KEY);

            if( numberData != null )
            {
                var carNumArray = numberData.GetJObjectArray("carNumbers");

                if( carNumArray != null )
                {
                    foreach( var carNumEntry in carNumArray )
                    {
                        string guid = carNumEntry.GetString("guid");
                        int? number = carNumEntry.GetInt("number");

                        if( !string.IsNullOrEmpty(guid) && number.HasValue )
                        {
                            SetCarNumber(guid, number.Value);
                        }
                    }
                }
            }
        }

        private static JObject CreateSaveEntry( KeyValuePair<string, int> kvp )
        {
            var result = new JObject();
            result.SetString("guid", kvp.Key);
            result.SetInt("number", kvp.Value);
            return result;
        }

        public static void SaveData()
        {
            var numberData = new JObject();

            JObject[] carNumArray = SavedCarNumbers.Select(CreateSaveEntry).ToArray();
            numberData.SetJObjectArray("carNumbers", carNumArray);

            SaveGameManager.data.SetJObject(SAVE_DATA_KEY, numberData);
        }

        #endregion
    }
}
