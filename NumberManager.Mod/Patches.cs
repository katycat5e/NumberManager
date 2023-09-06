using System;
using System.Collections.Generic;
using System.Linq;
using DV.ThingTypes;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using NumberManager.Shared;
using SkinManagerMod;
using UnityEngine;

namespace NumberManager.Mod
{
    public struct DefaultTexInfo
    {
        public readonly string? Name;
        public readonly int Width;
        public readonly int Height;

        public DefaultTexInfo( string? name, int width, int height )
        {
            Name = name;
            Width = width;
            Height = height;
        }
    }

    [HarmonyPatch(typeof(SkinManager), nameof(SkinManager.ApplySkin), typeof(TrainCar), typeof(Skin))]
    class SkinManager_ReplaceTexture_Patch
    {
        private static DefaultTexInfo GetDefaultTexInfo( MeshRenderer renderer )
        {
            //var mainTex = renderer.material.GetTexture("_MainTex");

            if( renderer.material.HasProperty("_MainTex") && (renderer.material.GetTexture("_MainTex") is Texture mainTex) )
            {
                return new DefaultTexInfo(mainTex.name, mainTex.width, mainTex.height);
            }
            else return new DefaultTexInfo(null, 0, 0);
        }

        internal static void Prefix( TrainCar trainCar, out ReplaceTextureState __state )
        {
            // Get the default texture names, because the ReplaceTexture method erases them with the new textures
            var renderers = trainCar.gameObject.GetComponentsInChildren<MeshRenderer>();
            __state = new ReplaceTextureState(renderers.ToDictionary(mr => mr, mr => GetDefaultTexInfo(mr)));

            var currentScheme = NumberManager.GetScheme(trainCar);
            if (currentScheme != null)
            {
                __state.HadAppliedScheme = true;

                int currentNumber = NumberManager.GetCurrentCarNumber(trainCar);
                int carId = NumberManager.GetCarIdNumber(trainCar.ID);
                __state.WasOffsetNumber = (currentScheme.Offset + carId) == currentNumber;
            }
            else
            {
                __state.WasOffsetNumber = NumberManager.Settings.AllowCarIdOffset;
            }
        }

        static void Postfix( TrainCar trainCar, ReplaceTextureState __state )
        {
            int number;
            if (CarTypes.IsTender(trainCar.carLivery) && NumberManager.LastSteamerNumber.HasValue)
            {
                number = NumberManager.LastSteamerNumber.Value;
            }
            else if (__state.WasOffsetNumber)
            {
                number = NumberManager.GetCarIdNumber(trainCar.ID);
                if ((NumberManager.GetScheme(trainCar) is NumberConfig currentScheme) && NumberManager.Settings.AllowCarIdOffset)
                {
                    number += currentScheme.Offset;
                }
            }
            else
            {
                number = NumberManager.GetCurrentCarNumber(trainCar);
            }
            NumberManager.ApplyNumbering(trainCar, number, __state.DefaultTextureInfo);
        }

        public class ReplaceTextureState
        {
            public Dictionary<MeshRenderer, DefaultTexInfo> DefaultTextureInfo;
            public bool HadAppliedScheme = false;
            public bool WasOffsetNumber = false;

            public ReplaceTextureState(Dictionary<MeshRenderer, DefaultTexInfo> defaultTextureInfo)
            {
                DefaultTextureInfo = defaultTextureInfo;
            }
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    class SaveGameManager_Save_Patch
    {
        static void Prefix( SaveGameManager __instance )
        {
            NumberManager.SaveData();
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
