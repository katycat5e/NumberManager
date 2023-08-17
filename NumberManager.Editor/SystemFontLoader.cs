using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace NumberManager.Editor
{
    public class SystemFontLoader : EditorWindow
    {
        private const string FONTS_FOLDER = "_FONTS";

        private static SystemFontLoader _instance;

        public static bool PromptForFont(out Font selection)
        {
            _instance = CreateInstance<SystemFontLoader>();
            _instance.Refresh();
            _instance.ShowModalUtility();

            selection = _instance._selectedFontAsset;
            return selection;
        }

        private bool Supported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private readonly List<SystemFontInfo> _availableFonts = new List<SystemFontInfo>();
        private Font _selectedFontAsset;

        private readonly struct SystemFontInfo
        {
            public readonly string Name;
            public readonly string Path;
            public readonly Font LocalAsset;

            public SystemFontInfo(string name, string path, Font fontAsset)
            {
                Name = name;
                Path = path;
                LocalAsset = fontAsset;
            }
        }

        public void Refresh()
        {
            if (!Supported) return;

            const string FONTS_SUBKEY = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts";

            using var hklmFonts = Registry.LocalMachine.OpenSubKey(FONTS_SUBKEY);
            using var hkUserFonts = Registry.CurrentUser.OpenSubKey(FONTS_SUBKEY);

            _availableFonts.Clear();
            _availableFonts.AddRange(
                GetFontValues(hklmFonts)
                .Concat(GetFontValues(hkUserFonts))
                .OrderBy(f => f.Name));
        }

        private static IEnumerable<SystemFontInfo> GetFontValues(RegistryKey key)
        {
            foreach (string valueName in key.GetValueNames())
            {
                string fontPath = key.GetValue(valueName) as string;
                if (string.IsNullOrEmpty(fontPath) || (Path.GetExtension(fontPath).ToLower() != ".ttf"))
                {
                    continue;
                }
                if (!Path.IsPathRooted(fontPath))
                {
                    fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fontPath);
                }

                string fontName = valueName.Replace(" (TrueType)", string.Empty);

                string assetPath = Path.Combine("Assets", FONTS_FOLDER, $"{fontName}.ttf");
                var fontAsset = AssetDatabase.LoadAssetAtPath<Font>(assetPath);

                yield return new SystemFontInfo(fontName, fontPath, fontAsset);
            }
        }

        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            if (!Supported)
            {
                EditorGUILayout.LabelField("Not supported on this platform");
            }
            else
            {
                EditorGUILayout.LabelField("Select System Font:");
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                foreach (var font in _availableFonts)
                {
                    if (GUILayout.Button(font.Name))
                    {
                        ImportSelectedFont(font);
                        Close();
                        return;
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Cancel"))
            {
                _selectedFontAsset = null;
                Close();
                return;
            }

            EditorGUILayout.EndVertical();
        }

        private void ImportSelectedFont(SystemFontInfo selection)
        {
            if (selection.LocalAsset)
            {
                _selectedFontAsset = selection.LocalAsset;
                return;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_FONTS"));

            if (!File.Exists(selection.Path))
            {
                Debug.LogWarning($"System font {selection.Name} could not be found on disk - probably moved or deleted");
                _selectedFontAsset = null;
                return;
            }

            string fontAssetPath = Path.Combine("_FONTS", $"{selection.Name}.ttf");
            string targetPath = Path.Combine(Application.dataPath, fontAssetPath);

            File.Copy(selection.Path, targetPath, true);

            fontAssetPath = Path.Combine("Assets", fontAssetPath);
            AssetDatabase.ImportAsset(fontAssetPath);
            _selectedFontAsset = AssetDatabase.LoadAssetAtPath<Font>(fontAssetPath);
        }
    }
}
