using NumberManager.Shared;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;

namespace NumberManager.Editor
{
    [ExecuteInEditMode]
    public class NumberConfigEditor : MonoBehaviour
    {
        [Header("Debug Display")]
        public Renderer TargetRenderer;
        public int DisplayNumber;

        [Header("Config Generation")]
        public string TargetTextureName;
        public TargetVehicle TargetVehicle;

        public FontBlendMode BlendMode;
        [Range(0, 1)]
        public float ColorizeWhiteLevel;

        [Header("Random Numbers")]
        [Tooltip("Override car number setting and only use random numbers")]
        public bool ForceRandom = false;
        public int MinNumber = 1;
        public int MaxNumber = 9999;

        [Header("Car ID Numbers")]
        [Tooltip("Offset added to car ID when using ID number generation")]
        public int Offset = 0;

        [Header("Font Atlas Generation")]
        public FontProvider[] Fonts;
        [Header("Number Attach Points")]
        public NumAttachPoint[] AttachPoints;

        [RenderMethodButtons]
        [MethodButton("NumberManager.Editor.NumberConfigEditor:RegenerateFontConfig", "Regen Font Config")]
        [MethodButton("NumberManager.Editor.NumberConfigEditor:ExportConfig", "Export Config")]
        public bool renderButtons;

        private Texture2D _fontAtlas;
        private NumberConfig _numberConfig;

        public void SetTargetTextureName(string textureName)
        {
            TargetTextureName = textureName;
            _prevTargetTex = textureName;
        }

        private string _prevTargetTex;
        private void OnValidate()
        {
            if (TargetTextureName != _prevTargetTex)
            {
                TargetVehicle = TargetTextures.GetTargetVehicleForTexture(TargetTextureName);
            }
            else if (TargetVehicle != TargetVehicle.NotSet)
            {
                TargetTextureName = TargetTextures.GetTargetTexture(TargetVehicle);
            }

            _prevTargetTex = TargetTextureName;
        }

        public void Update()
        {
            _numberConfig ??= new NumberConfig();

            RegenerateFontConfig();

            RefreshShaderProps();
        }

        public static void RegenerateFontConfig(NumberConfigEditor editor)
        {
            editor.RegenerateFontConfig();
        }

        private void RegenerateFontConfig()
        {
            _numberConfig.Offset = Offset;
            _numberConfig.MinNumber = MinNumber;
            _numberConfig.MaxNumber = MaxNumber;
            _numberConfig.BlendMode = BlendMode;
            _numberConfig.ColorizeWhiteLevel = (BlendMode == FontBlendMode.Colorize) ? ColorizeWhiteLevel : 0;
            _numberConfig.ForceRandom = ForceRandom;
            _numberConfig.TargetTexture = TargetTextureName;

            if (Fonts == null || Fonts.Length == 0) return;

            _numberConfig.Fonts = new NumberFont[Fonts.Length];

            var atlasSizes = Fonts.Select(f => f.GetRotatedAtlasSize()).ToList();
            int atlasHeight = atlasSizes.Max(f => f.y);
            int atlasWidth = atlasSizes.Sum(f => f.x);

            _fontAtlas = new Texture2D(0, 0, TextureFormat.ARGB32, false);
            var atlases = new Texture2D[Fonts.Length];

            for (int i = 0; i < Fonts.Length; i++)
            {
                atlases[i] = Fonts[i].RenderFontToAtlas();
                _numberConfig.Fonts[i] = Fonts[i].NumberFont;
            }

            var offsets = _fontAtlas.PackTextures(atlases, 0);

            for (int i = 0; i < Fonts.Length; i++)
            {
                var offset = OffsetFromPackedUV(offsets[i], _fontAtlas);
                Fonts[i].CreateFontSettings(offset);
                _numberConfig.Fonts[i] = Fonts[i].NumberFont;
            }

            _numberConfig.FontTexture = _fontAtlas;
            _numberConfig.AttachPoints = AttachPoints;
            _numberConfig.TextureWidth = _fontAtlas.width;
            _numberConfig.TextureHeight = _fontAtlas.height;
        }

        // get bottom left corner of packed sub atlas
        private static Vector2Int OffsetFromPackedUV(Rect packedUV, Texture packedTex)
        {
            int x = Mathf.RoundToInt(packedUV.x * packedTex.width);
            int y = Mathf.RoundToInt((1 - packedUV.y) * packedTex.height);
            return new Vector2Int(x, y);
        }

        private void RefreshShaderProps()
        {
            if (TargetRenderer && (_numberConfig.Fonts != null) && (_numberConfig.Fonts.Length > 0))
            {
                var tgtTex = TargetRenderer.sharedMaterial.mainTexture;
                if (tgtTex)
                {
                    var props = ShaderPropBuilder.GetShaderProps(_numberConfig, DisplayNumber, tgtTex.width, tgtTex.height, Debug.LogWarning);
                    if (props != null)
                    {
                        TargetRenderer.sharedMaterial.SetTexture(NumShaderProps.ID_FONT_TEXTURE, _numberConfig.FontTexture);
                        props.ApplyTo(TargetRenderer.sharedMaterial);
                    }
                }
            }
        }

        public static void ExportConfig(NumberConfigEditor editor)
        {
            editor.ExportConfig();
        }

        private static string LastExportPath
        {
            get => EditorPrefs.GetString("NM_LastExportPath");
            set => EditorPrefs.SetString("NM_LastExportPath", value);
        }

        private void ExportConfig()
        {
            string startingPath;
            string folderName;
            string lastExport = LastExportPath;

            if (!string.IsNullOrEmpty(lastExport) && Directory.Exists(lastExport))
            {
                startingPath = Path.GetDirectoryName(lastExport);
                folderName = Path.GetFileName(lastExport.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            else
            {
                startingPath = Application.dataPath;
                folderName = "Numbering";
            }
            string exportPath = EditorUtility.SaveFolderPanel("Select Config Destination Folder", startingPath, folderName);
            if (string.IsNullOrWhiteSpace(exportPath)) return;
            LastExportPath = exportPath;

            // Export Font Atlas
            string texturePath = Path.Combine(exportPath, "num.png");
            byte[] _bytes = _fontAtlas.EncodeToPNG();
            File.WriteAllBytes(texturePath, _bytes);

            // Export XML Config file
            _numberConfig.StringPack();

            var s = new XmlSerializer(typeof(NumberConfig));
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);

            var settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Encoding = null,
            };
            var sb = new StringBuilder();

            using var stream = new UTF8StringWriter(sb);
            using var writer = XmlWriter.Create(stream, settings);
            s.Serialize(stream, _numberConfig, namespaces);

            string result = sb.ToString();
            result = Regex.Replace(result, "\\s+<\\w+ xsi:nil=\"true\" \\/>", string.Empty);

            string xmlConfigPath = Path.Combine(exportPath, "numbering.xml");
            File.WriteAllText(xmlConfigPath, result);

            EditorUtility.RevealInFinder(exportPath);
        }

        private class UTF8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public UTF8StringWriter(StringBuilder sb) : base(sb) { }
        }
    }
}
