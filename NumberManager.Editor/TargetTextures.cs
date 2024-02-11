using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NumberManager.Editor
{
    public static class TargetTextures
    {
        public static string GetTargetTexture(TargetVehicle target)
        {
            return _mainTexName[(int)target];
        }

        public static TargetVehicle GetTargetVehicleForTexture(string textureName)
        {
            int idx = Array.IndexOf(_mainTexName, textureName);
            return idx > 0 ? (TargetVehicle)idx : TargetVehicle.NotSet;
        }

        private static readonly string[] _mainTexName =
        {
            null,
            "LocoDE2_Body_01d",
            "LocoS282A_Body_01d",
            "LocoS282B_Body_01d",
            "LocoS060_Body_01d",
            "LocoDE6_Body_01d",
            "LocoDE6_Body_01d",
            "LocoDH4_ExteriorBody_01d",
            "LocoDM3_Body_01d",
            "LocoMicroshunter_Body_01d",

            "CarFlatcarCBBulkheadStakes_Brown_d",
            "CarFlatcarCBBulkheadStakes_Brown_d",
            "CarFlatcarCBBulkheadStakes_Military_d",

            "CarAutorackRed_01d",
            "CarAutorackBlue_01d",
            "CarAutorackGreen_01d",
            "CarAutorackYellow_01d",

            "CarTankOrange_01",
            "CarTankWhite_01d",
            "CarTankYellow_01d",
            "CarTankBlue_01d",
            "CarTankChrome_01d",
            "CarTankBlack_01d",

            "CarBoxcar_Brown_01d",
            "CarBoxcar_Green_01d",
            "CarBoxcar_Pink_01d",
            "CarBoxcar_Red_01d",
            "CarBoxcarMilitary_01d",
            "CarRefrigerator_d",

            "CarHopperBrown_d",
            "CarHopperTeal_d",
            "CarHopperYellow_d",

            "CarGondolaRed_d",
            "CarGondolaGreen_d",
            "CarGondolaGrey_d",

            "CarPassengerRed_01d",
            "CarPassengerGreen_01d",
            "CarPassengerBlue_01d",

            "LocoHandcar_01d",
            "CarCabooseRed_Body_01d",
            "CarNuclearFlask_d",
        };
    }

    public enum TargetVehicle
    {
        NotSet,
        DE2,
        S282,
        Tender,
        S060,
        //LocoRailbus,
        DE6,
        DE6Slug,
        DH4,
        DM3,
        Microshunter,

        FlatbedEmpty,
        FlatbedStakes,
        FlatbedMilitary,

        AutorackRed,
        AutorackBlue,
        AutorackGreen,
        AutorackYellow,

        TankOrange,
        TankWhite,
        TankYellow,
        TankBlue,
        TankChrome,
        TankBlack,

        BoxcarBrown,
        BoxcarGreen,
        BoxcarPink,
        BoxcarRed,
        BoxcarMilitary,
        RefrigeratorWhite,

        HopperBrown,
        HopperTeal,
        HopperYellow,

        GondolaRed,
        GondolaGreen,
        GondolaGray,

        PassengerRed,
        PassengerGreen,
        PassengerBlue,

        HandCar,
        Caboose,
        NuclearFlask,
    }
}
