using DV.JObjectExtstensions;
using HarmonyLib;
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
        public static int LastSteamerNumber { get; private set; } = -1;

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
            foreach( var prefab in SM_Main.prefabMap )
            {
                string carTypeDir = Path.Combine(skinsFolder, prefab.Value);

                if( Directory.Exists(carTypeDir) )
                {
                    var shaderDict = new Dictionary<string, Shader>();
                    DefaultShaders.Add(prefab.Key, shaderDict);

                    foreach( string skinDir in Directory.GetDirectories(carTypeDir) )
                    {
                        string configFile = Path.Combine(skinDir, NUM_CONFIG_FILE);
                        NumberConfig config = null;

                        if( File.Exists(configFile) )
                        {
                            config = LoadConfig(prefab.Key, Path.GetFileName(skinDir), configFile);
                        }

                        if( config != null )
                        {
                            // get default shader for targeted material
                            var carPrefabObj = CarTypes.GetCarPrefab(prefab.Key);

                            if( carPrefabObj != null )
                            {
                                foreach( MeshRenderer renderer in carPrefabObj.GetComponentsInChildren<MeshRenderer>() )
                                {
                                    if( renderer.material.HasProperty("_MainTex") && (renderer.material.GetTexture("_MainTex") is Texture2D mainTex) )
                                    {
                                        if( string.Equals(mainTex.name, config.TargetTexture) && !shaderDict.ContainsKey(mainTex.name) )
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
        }

        public static NumberConfig LoadConfig( TrainCarType carType, string skinName, string configPath )
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
                    NumberSchemes.Add(new SchemeKey(carType, skinName), config);
                }
                catch( Exception ex )
                {
                    modEntry.Logger.Error($"Exception when loading numbering config for {skinName}:\n{ex.Message}");
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

        public static void ApplyNumbering( TrainCar car )
        {
            Dictionary<MeshRenderer, DefaultTexInfo> texDict = null;
            SkinManager_ReplaceTexture_Patch.Prefix(car, ref texDict);
            ApplyNumbering(car, texDict);
        }

        public static void ApplyNumbering( TrainCar car, Dictionary<MeshRenderer, DefaultTexInfo> defaultTexDict, string prevSkin = null )
        {
            if( !TryGetAssignedSkin(car, out var skin) ) return;
            var numScheme = GetScheme(car.carType, skin.name);

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

            // Check if car already had a number assigned
            int carNumber = GetSavedCarNumber(car.logicCar.carGuid);

            if( carNumber < 0 )
            {
                // Previously un-numbered car
                // A new tender should try to match engine if possible
                if( CarTypes.IsTender(car.carType) )
                {
                    carNumber = LastSteamerNumber;
                }
                else
                {
                    if( Settings.PreferCarId && !numScheme.ForceRandom )
                    {
                        int offset = Settings.AllowCarIdOffset ? numScheme.Offset : 0;
                        carNumber = GetCarIdNumber(car.ID) + offset;
                    }
                    else carNumber = numScheme.GetRandomNum();
                }
            }

            modEntry.Logger.Log($"Applying number {carNumber} to {car.ID}");
            SetCarNumber(car.CarGUID, carNumber);

            // Check if the texture we're targeting is supplied by the skin
            var tgtTex = skin.GetTexture(numScheme.TargetTexture)?.TextureData;
            NumShaderProps shaderProps = null;

            if( tgtTex != null )
            {
                shaderProps = GetShaderProps(numScheme, carNumber, tgtTex.width, tgtTex.height);
            }
            // otherwise we'll be lazy and figure out the width/height when we find the default texture

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

                DefaultTexInfo defaultTex = defaultTexDict[renderer];

                // check if this is the target for numbering
                if( string.Equals(numScheme.TargetTexture, defaultTex.Name) )
                {
                    if( shaderProps == null )
                    {
                        shaderProps = GetShaderProps(numScheme, carNumber, defaultTex.Width, defaultTex.Height);
                    }

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
