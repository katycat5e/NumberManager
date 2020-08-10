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
    [HarmonyPatch(typeof(SkinManagerMod.Main), "ReplaceTexture")]
    class SkinManager_ReplaceTexture_Patch
    {
        static void Prefix( TrainCar trainCar, ref Dictionary<MeshRenderer, string> __state )
        {
            // Get the default texture names, because the ReplaceTexture method erases them with the new textures
            var renderers = trainCar.gameObject.GetComponentsInChildren<MeshRenderer>();
            __state = renderers.ToDictionary(mr => mr, mr => mr.material.GetTexture("_MainTex")?.name);
        }

        static void Postfix( TrainCar trainCar, Dictionary<MeshRenderer, string> __state )
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
