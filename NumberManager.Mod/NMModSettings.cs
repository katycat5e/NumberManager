using System;
using System.Collections.Generic;
using UnityModManagerNet;

namespace NumberManager.Mod
{
    public class NMModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Use Car ID for number")]
        public bool PreferCarId = true;

        [Draw("Allow skins to supply a Car ID offset")]
        public bool AllowCarIdOffset = true;

        [Draw("Enable the default loco numbers")]
        public bool EnableDefaultNumbers = true;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            
        }
    }
}
