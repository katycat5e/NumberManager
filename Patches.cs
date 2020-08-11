using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony12;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NumberManagerMod
{
    public class DefaultTexInfo
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;

        public DefaultTexInfo( string name, int width, int height )
        {
            Name = name;
            Width = width;
            Height = height;
        }
    }

    [HarmonyPatch(typeof(SkinManagerMod.Main), "ReplaceTexture")]
    class SkinManager_ReplaceTexture_Patch
    {
        private static DefaultTexInfo GetDefaultTexInfo( MeshRenderer renderer )
        {
            var mainTex = renderer.material.GetTexture("_MainTex");

            if( mainTex != null )
            {
                return new DefaultTexInfo(mainTex.name, mainTex.width, mainTex.height);
            }
            else return new DefaultTexInfo(null, 0, 0);
        }

        static void Prefix( TrainCar trainCar, ref Dictionary<MeshRenderer, DefaultTexInfo> __state )
        {
            // Get the default texture names, because the ReplaceTexture method erases them with the new textures
            var renderers = trainCar.gameObject.GetComponentsInChildren<MeshRenderer>();
            __state = renderers.ToDictionary(mr => mr, mr => GetDefaultTexInfo(mr));
        }

        static void Postfix( TrainCar trainCar, Dictionary<MeshRenderer, DefaultTexInfo> __state )
        {
            NumberManager.ApplyNumbering(trainCar, __state);
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    class SaveGameManager_Save_Patch
    {
        static void Prefix( SaveGameManager __instance )
        {

        }
    }

    [HarmonyPatch(typeof(CarsSaveManager), "Load")]
    class CarsSaveManager_Load_Patch
    {
        static void Prefix( JObject savedData )
        {
            if( savedData == null ) return;

            NumberManager.LoadSaveData();
        }
    }
}
