using System.IO;
using UnityEditor;

namespace NumberManager.Editor
{
    public class CreateAssetBundles
    {
        [MenuItem("Assets/Build AssetBundles")]
        public static void BuildAllAssetBundles()
        {
            string bundleDir = "Assets/AssetBundles";
            if (!Directory.Exists(bundleDir))
            {
                Directory.CreateDirectory(bundleDir);
            }

            BuildPipeline.BuildAssetBundles(bundleDir, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
        }
    }
}
