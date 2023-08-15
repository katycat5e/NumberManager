using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NumberManager.Shared
{
    public interface IRemapProvider
    {
        bool TryGetUpdatedTextureName(string carId, string textureName, out string newName);
    }
}
