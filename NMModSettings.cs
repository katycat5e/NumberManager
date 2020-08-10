using System;
using System.Collections.Generic;
using UnityModManagerNet;

namespace NumberManagerMod
{
    public class NMModSettings : UnityModManager.ModSettings, IDrawable
    {
        public bool PreferCarId = true;

        public void OnChange()
        {
            throw new NotImplementedException();
        }
    }
}
