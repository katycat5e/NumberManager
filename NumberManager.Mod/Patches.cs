﻿using System;
using System.Collections.Generic;
using System.Linq;
using DV.Customization.Paint;
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

    [HarmonyPatch(typeof(TrainCarPaint), nameof(TrainCarPaint.CurrentTheme), MethodType.Setter)]
    class SkinManager_ReplaceTexture_Patch
    {
        internal static void Prefix(TrainCarPaint __instance, out ReplaceTextureState? __state)
        {
            if (__instance.TargetArea != TrainCarPaint.Target.Exterior)
            {
                __state = null;
                return;
            }

            var trainCar = TrainCar.Resolve(__instance.gameObject);
            GetTextureState(trainCar, out __state);
        }

        internal static void GetTextureState(TrainCar trainCar, out ReplaceTextureState __state)
        {
            __state = new ReplaceTextureState();

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

        static void Postfix(TrainCarPaint __instance, ReplaceTextureState? __state)
        {
            if (__state is null) return;
            var trainCar = TrainCar.Resolve(__instance.gameObject);

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
            NumberManager.ApplyNumbering(trainCar, number);
        }

        public class ReplaceTextureState
        {
            public bool HadAppliedScheme = false;
            public bool WasOffsetNumber = false;
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
