using NumberManager.Shared;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NumberManager.Editor
{
    public class WorkspaceWizard : EditorWindow
    {
        private static WorkspaceWizard _instance;

        private const string WORKSPACE_ROOT = "_NUMBERS";

        private static string LastTextureFolder
        {
            get => EditorPrefs.GetString("NM_LastTextureFolder");
            set => EditorPrefs.SetString("NM_LastTextureFolder", value);
        }


        [MenuItem("Number Manager/Create New Workspace")]
        public static void LaunchWizard()
        {
            _instance = GetWindow<WorkspaceWizard>();
            _instance.Refresh();
            _instance.Show();
        }

        private string _texturePath;
        private string _metalRoughPath;
        private string _workspaceName;
        // template

        public void Refresh()
        {
            titleContent = new GUIContent("NM - New Number Workspace");
            _texturePath = null;
            _metalRoughPath = null;
            _workspaceName = null;
        }

        private Vector2 _scrollPosition = Vector2.zero;

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Target Image File
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path to the target diffuse texture (PNG)");
            if (GUILayout.Button("Open..."))
            {
                _texturePath = PromptForTexture(_texturePath);
            }
            EditorGUILayout.LabelField(_texturePath);

            // Target Metallic/Rough File
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Path to metal/rough map (PNG, optional)");
            if (GUILayout.Button("Open..."))
            {
                _metalRoughPath = PromptForTexture(_metalRoughPath);
            }
            EditorGUILayout.LabelField(_metalRoughPath);

            // Workspace Name
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            _workspaceName = EditorGUILayout.TextField("Workspace Name", _workspaceName);
            EditorGUILayout.LabelField("Name of the workspace folder and scene");

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = IsValid;
            if (GUILayout.Button("Create Car"))
            {
                CreateWorkspace();
                Close();
                return;
            }
            GUI.enabled = true;

            if (GUILayout.Button("Cancel"))
            {
                Close();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static string PromptForTexture(string current)
        {
            string startFolder;
            if (string.IsNullOrWhiteSpace(current))
            {
                startFolder = Path.GetDirectoryName(current);
            }
            else
            {
                startFolder = LastTextureFolder;
                if (string.IsNullOrWhiteSpace(startFolder)) startFolder = Application.dataPath;
            }

            string targetTexPath = EditorUtility.OpenFilePanelWithFilters("Select Texture File", startFolder, new string[] { "PNG Images", "png" });
            if (string.IsNullOrEmpty(targetTexPath) || !File.Exists(targetTexPath))
            {
                return null;
            }

            return targetTexPath;
        }

        private bool IsValid =>
            !string.IsNullOrWhiteSpace(_texturePath) &&
            !string.IsNullOrWhiteSpace(_workspaceName);


        private Shader _numberShader;
        private Shader NumberShader
        {
            get
            {
                if (!_numberShader)
                {
                    _numberShader = Shader.Find("Custom/NumberSurface");
                }
                return _numberShader;
            }
        }

        private void CreateWorkspace()
        {
            string workspaceRelPath = Path.Combine(WORKSPACE_ROOT, _workspaceName);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, workspaceRelPath));

            // Texture
            string textureFileName = Path.GetFileName(_texturePath);
            File.Copy(_texturePath, Path.Combine(Application.dataPath, workspaceRelPath, textureFileName), true);

            string textureAssetPath = Path.Combine("Assets", workspaceRelPath, textureFileName);
            AssetDatabase.ImportAsset(textureAssetPath);
            var baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);

            Texture2D metalRough = null;
            if (!string.IsNullOrWhiteSpace(_metalRoughPath))
            {
                string metalRoughFileName = Path.GetFileName(_metalRoughPath);
                File.Copy(_metalRoughPath, Path.Combine(Application.dataPath, workspaceRelPath, metalRoughFileName), true);

                string metalRoughAssetPath = Path.Combine("Assets", workspaceRelPath, metalRoughFileName);
                AssetDatabase.ImportAsset(metalRoughAssetPath);
                metalRough = AssetDatabase.LoadAssetAtPath<Texture2D>(metalRoughAssetPath);
            }

            // Material
            var numMaterial = new Material(NumberShader);
            numMaterial.SetTexture(NumShaderProps.ID_MAIN_TEXTURE, baseTexture);
            if (metalRough)
            {
                numMaterial.SetTexture(NumShaderProps.ID_METAL_GLOSS_MAP, metalRough);
            }

            string materialAssetPath = Path.Combine("Assets", workspaceRelPath, 
                $"{_workspaceName.Replace(" ", string.Empty)}Preview.mat");
            AssetDatabase.CreateAsset(numMaterial, materialAssetPath);

            // Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string scenePath = Path.Combine("Assets", workspaceRelPath, $"{_workspaceName}.unity");

            // Preview Plane
            var previewPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            previewPlane.name = _workspaceName;
            previewPlane.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            DestroyImmediate(previewPlane.GetComponent<Collider>());

            var planeRenderer = previewPlane.GetComponent<Renderer>();
            planeRenderer.sharedMaterial = numMaterial;

            // Config Editor Script
            var editor = previewPlane.AddComponent<NumberConfigEditor>();
            editor.TargetRenderer = planeRenderer;
            editor.DisplayNumber = 1234;
            editor.SetTargetTextureName(Path.GetFileNameWithoutExtension(textureFileName));
            
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeObject = previewPlane;
        }
    }
}
