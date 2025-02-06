using DV.Customization.Paint;
using DV.ThingTypes;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using NumberManager.Shared;

namespace NumberManager.Mod
{
    [HarmonyPatch(typeof(TrainCarPaint))]
    internal static class TrainCarPaintPatches
    {
        [HarmonyPatch(nameof(TrainCarPaint.CurrentTheme), MethodType.Setter)]
        [HarmonyPrefix]
        static void BeforeThemeChanged(TrainCarPaint __instance, out ReplaceTextureState? __state)
        {
            if (__instance.TargetArea != TrainCarPaint.Target.Exterior)
            {
                __state = null;
                return;
            }

            var trainCar = TrainCar.Resolve(__instance.gameObject);
            if (trainCar.logicCar is null)
            {
                __state = null;
                return;
            }

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

        [HarmonyPatch(nameof(TrainCarPaint.CurrentTheme), MethodType.Setter)]
        [HarmonyPostfix]
        static void AfterThemeChanged(TrainCarPaint __instance, ReplaceTextureState? __state)
        {
            if (__state is null) return;
            
            var trainCar = TrainCar.Resolve(__instance.gameObject);
            if (trainCar.logicCar is null) return;

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

    [HarmonyPatch(typeof(TrainCar))]
    internal static class TrainCarPatches
    {
        [HarmonyPatch(nameof(TrainCar.ReturnCarToPool))]
        [HarmonyPrefix]
        static void OnReturnToPool(TrainCar __instance)
        {
            NumberManager.RemoveCarNumber(__instance.CarGUID);
        }
    }

    [HarmonyPatch(typeof(SaveGameManager))]
    internal static class SaveGameManagerPatches
    {
        [HarmonyPatch(nameof(SaveGameManager.Save))]
        [HarmonyPrefix]
        static void OnSave()
        {
            NumberManager.SaveData();
        }
    }

    [HarmonyPatch(typeof(CarsSaveManager))]
    internal static class CarsSaveManagerPatches
    {
        [HarmonyPatch(nameof(CarsSaveManager.Load))]
        [HarmonyPrefix]
        static void BeforeLoad(JObject savedData)
        {
            if (savedData == null) return;

            NumberManager.LoadSaveData();
        }
    }
}
