using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Text;

namespace FBXAssetExtractor
{
    /// <summary>
    /// Editor tool for extracting meshes, animations, materials, and prefabs from .FBX files.
    /// Based on this Reddit post https://www.reddit.com/r/Unity3D/comments/59pywj/extracting_meshes_from_fbx_files/
    /// </summary>
    public class FBXAssetExtractor : EditorWindow
    {
        const string EXTRACT_FBX_MENU_ITEM = "Assets/Extract FBX";

        private static readonly string fbxExtension = ".fbx";
        private static readonly string prefabExtension = ".prefab";
        private static readonly Dictionary<Type, string> typeToExtension = new Dictionary<Type, string>()
        {
            { typeof(Mesh),         ".mesh" },
            { typeof(AnimationClip),".anim"},
            { typeof(Material),     ".mat"},
            { typeof(Avatar),       ".asset" },
            { typeof(GameObject),   ".prefab"},
        };

        private string meshFolder, animationFolder, prefabFolder, materialFolder, avatarFolder = null;

        private ExtractableObjectsGUI<Mesh> extractableMeshGUI;
        private ExtractableObjectsGUI<AnimationClip> extractableAnimationGUI;
        private ExtractableObjectsGUI<Material> extractableMaterialGUI;
        private ExtractableObjectsGUI<Avatar> extractableAvatarGUI;
        private ExtractableObjectsGUI<GameObject> extractableGameObjectGUI;

        /// <summary>
        /// Checks to see if we should run the process on the selected item.
        /// </summary>
        /// <returns>true if the selected item is an .fbx file. false otherwise.</returns>
        [MenuItem(EXTRACT_FBX_MENU_ITEM, validate = true)]
        private static bool ExtractMeshesMenuItemValidate()
        {
            if (AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(fbxExtension))
            {
                return true;
            }
            return false;
        }

        [MenuItem(EXTRACT_FBX_MENU_ITEM)]
        private static void ExtractMeshesMenuItem()
        {

            var window = GetWindow<FBXAssetExtractor>();

            window.Initialize(Selection.activeObject);
        }

        private void OnGUI()
        {
            extractableMeshGUI.DrawGUI();
            extractableAnimationGUI.DrawGUI();
            extractableMaterialGUI.DrawGUI();
            extractableAvatarGUI.DrawGUI();
            extractableGameObjectGUI.DrawGUI();

            if (GUILayout.Button("Extract!", GUILayout.Width(100.0f)))
            {
                Close();
                ExtractAssets(
                    extractableMeshGUI.GetAllObjectsToExtractAndFolder(),
                    extractableAnimationGUI.GetAllObjectsToExtractAndFolder(),
                    extractableMaterialGUI.GetAllObjectsToExtractAndFolder(),
                    extractableAvatarGUI.GetAllObjectsToExtractAndFolder(),
                    extractableGameObjectGUI.GetAllObjectsToExtractAndFolder()
                    );
            }
        }


        /// <summary>
        /// Initialize all of the window fields to the default values, and set up the UI for extraction of the current object.
        /// </summary>
        /// <param name="parentFbxObject"></param>
        private void Initialize(UnityEngine.Object parentFbxObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(parentFbxObject);
            int lastSlash = assetPath.LastIndexOf('/') + 1;
            string assetFolder = assetPath.Remove(lastSlash);
            
            meshFolder = assetFolder + "Meshes";
            animationFolder = assetFolder + "Animations";
            prefabFolder = assetFolder + "Prefabs";
            materialFolder = assetFolder + "Materials";
            avatarFolder = assetFolder + "Avatars";

            extractableMeshGUI = new ExtractableObjectsGUI<Mesh>("Meshes", meshFolder);
            extractableAnimationGUI = new ExtractableObjectsGUI<AnimationClip>("Animations", animationFolder);
            extractableMaterialGUI = new ExtractableObjectsGUI<Material>("Materials", materialFolder);
            extractableAvatarGUI = new ExtractableObjectsGUI<Avatar>("Avatars", avatarFolder);
            extractableGameObjectGUI = new ExtractableObjectsGUI<GameObject>("Prefabs", prefabFolder);

            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            
            //Iterate through all of the sub objects of the asset, and add them to the respective extractable GUI object.
            for (int objectIndex = 0; objectIndex < objects.Length; ++objectIndex)
            {
                UnityEngine.Object currentObject = objects[objectIndex];
                if(currentObject is Avatar)
                {
                    Debug.Log("Found avatar: " + currentObject.name);
                }
                if ( extractableMeshGUI.TryAddExtractableObject(currentObject) ||
                extractableAnimationGUI.TryAddExtractableObject(currentObject) ||
                extractableAvatarGUI.TryAddExtractableObject(currentObject)||
                 extractableMaterialGUI.TryAddExtractableObject(currentObject))
                {
                    //We have found a regular asset to extract. And we added them to the respective GUI objects
                }
                else if (currentObject is GameObject && (currentObject as GameObject).transform.parent == null)
                {
                    //We have found a root game object to prefab.
                    extractableGameObjectGUI.TryAddExtractableObject(currentObject);
                }
            }
        }

        private static void ExtractAssets(
            Tuple<List<Mesh>, string> meshesAndFoldername,
            Tuple<List<AnimationClip>, string> animationsAndFoldername,
            Tuple<List<Material>, string> materialsAndFoldername,
            Tuple<List<Avatar>, string> avatarsAndFoldername,
            Tuple<List<GameObject>, string> prefabsAndFoldername)
        {
            int totalItems = meshesAndFoldername.Item1.Count + animationsAndFoldername.Item1.Count + materialsAndFoldername.Item1.Count + avatarsAndFoldername.Item1.Count + prefabsAndFoldername.Item1.Count;
            ItemProgress itemProgress = new ItemProgress(totalItems, "Extracting FBX");
            //Keep track of the saved meshes and materials by name, so that we can replace them in the prefabs if necessary.
            Dictionary<string, Mesh> savedMeshes = SaveItemsToFolder(meshesAndFoldername.Item1, meshesAndFoldername.Item2, itemProgress);
            Dictionary<string, Material> savedMaterials = SaveItemsToFolder(materialsAndFoldername.Item1, materialsAndFoldername.Item2, itemProgress);
            //Animations do not need to be tracked in a dictionary since they will not be attached to the gameobject components.
            SaveItemsToFolder(animationsAndFoldername.Item1, animationsAndFoldername.Item2, itemProgress);
            SaveItemsToFolder(avatarsAndFoldername.Item1, avatarsAndFoldername.Item2, itemProgress);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            //Create the folder for the prefabs if it does not exist.
            if (prefabsAndFoldername.Item1.Count > 0)
            {
                CreateAllNestedFolders(prefabsAndFoldername.Item2);
            }
            Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            //Create the prefab asset files here since they are handled differently as assets, and we need to link the assets to the components.
            for (int gameObjectIndex = 0; gameObjectIndex < prefabsAndFoldername.Item1.Count; ++gameObjectIndex)
            {
                GameObject gameObject = GameObject.Instantiate(prefabsAndFoldername.Item1[gameObjectIndex]);
                MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
                for (int meshFilterIndex = 0; meshFilterIndex < meshFilters.Length; ++meshFilterIndex)
                {
                    MeshFilter meshFilter = meshFilters[meshFilterIndex];
                    Mesh mesh;
                    if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name != null && savedMeshes.TryGetValue(meshFilter.sharedMesh.name, out mesh))
                    {
                        meshFilter.sharedMesh = mesh;
                    }
                    else if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name != null)
                    {
                        Debug.LogWarning("Could not find mesh to replace: " + meshFilter.sharedMesh.name);
                        meshFilter.sharedMesh = null;
                    }
                }
                Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();

                for (int rendererIndex = 0; rendererIndex < renderers.Length; ++rendererIndex)
                {
                    SkinnedMeshRenderer skinnedMeshRenderer = renderers[rendererIndex] as SkinnedMeshRenderer;
                    Mesh mesh;
                    if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.name != null)
                    {
                        if (savedMeshes.TryGetValue(skinnedMeshRenderer.sharedMesh.name, out mesh))
                        {
                            skinnedMeshRenderer.sharedMesh = mesh;
                        }
                        else
                        {
                            Debug.LogWarning("Could not find skinned mesh to replace: " + skinnedMeshRenderer.sharedMesh.name +
                                "On gameObject: " + skinnedMeshRenderer.gameObject.name);
                            skinnedMeshRenderer.sharedMesh = null;
                        }

                    }

                    Material[] replacementMaterials = new Material[renderers[rendererIndex].sharedMaterials.Length];
                    for (int rendererMatIndex = 0; rendererMatIndex < renderers[rendererIndex].sharedMaterials.Length; ++rendererMatIndex)
                    {
                        Material currentSharedMaterial = renderers[rendererIndex].sharedMaterials[rendererMatIndex];
                        if (savedMaterials.ContainsKey(currentSharedMaterial.name))
                        {
                            replacementMaterials[rendererMatIndex] = savedMaterials[currentSharedMaterial.name];
                        }
                        else if (currentSharedMaterial != defaultMaterial)
                        {
                            Debug.LogWarning("Could not find material to replace: " + renderers[rendererIndex].sharedMaterials[rendererMatIndex].name +
                                "On gameObject: " + renderers[rendererIndex].gameObject.name + ", replacing with default diffuse. ");
                            replacementMaterials[rendererMatIndex] = defaultMaterial;
                        }
                    }
                    renderers[rendererIndex].sharedMaterials = replacementMaterials;
                }

                int substringIndex = gameObject.name.IndexOf("(Clone)");
                string goName;
                if (substringIndex > 0)
                {
                    goName = gameObject.name.Remove(substringIndex);
                }
                else
                {
                    goName = gameObject.name;
                }
                string filename = MakeValidFileName(goName) + prefabExtension;
                PrefabUtility.SaveAsPrefabAsset(gameObject, prefabsAndFoldername.Item2 + "/" + filename);
                GameObject.DestroyImmediate(gameObject);
            }

            //Cleanup
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Save the list of items to the folder.
        /// </summary>
        /// <typeparam name="ItemType">Type of all items to save. Must be a child class of UnityEngine.Object</typeparam>
        /// <param name="ItemsToSave">List of items to save.</param>
        /// <param name="folderName">Parent folder of all assets to save.</param>
        /// <param name="itemProgress">Item progress object to track progress for the user.</param>
        /// <returns>A dictionary of the names of the objects. Used to assign to gameObject components.</returns>
        private static Dictionary<string, ItemType> SaveItemsToFolder<ItemType>(List<ItemType> ItemsToSave, string folderName, ItemProgress itemProgress) where ItemType : UnityEngine.Object
        {
            if (!AssetDatabase.IsValidFolder(folderName) && ItemsToSave.Count > 0)
            {
                CreateAllNestedFolders(folderName);
            }
            string extension;
            if (!typeToExtension.TryGetValue(typeof(ItemType), out extension))
            {
                extension = ".asset";
            }
            Dictionary<string, ItemType> objectDict = new Dictionary<string, ItemType>(ItemsToSave.Count);
            for (int meshIndex = 0; meshIndex < ItemsToSave.Count; ++meshIndex)
            {
                ItemType instantiatedObject = UnityEngine.Object.Instantiate(ItemsToSave[meshIndex]);
                instantiatedObject.name = instantiatedObject.name.Remove(instantiatedObject.name.IndexOf("(Clone)"));
                string filename = MakeValidFileName(instantiatedObject.name) + extension;
                objectDict.Add(instantiatedObject.name, instantiatedObject);
                itemProgress.IncrementCountAndDisplay(instantiatedObject.name);
                AssetDatabase.CreateAsset(instantiatedObject, folderName + "/" + filename);
            }
            return objectDict;
        }

        /// <summary>
        /// Creates the specified folder and all necessary parent folders if they do not exists.
        /// </summary>
        /// <param name="fullFolderName">Full folder name relative to the asset path.</param>
        private static void CreateAllNestedFolders(string fullFolderName)
        {
            string[] folderNames = fullFolderName.Split('/');
            StringBuilder runningFolderName = new StringBuilder(fullFolderName.Length);
            runningFolderName.Append(folderNames[0]);
            runningFolderName.Append('/');
            for (int folderNameIndex = 1; folderNameIndex < folderNames.Length; ++folderNameIndex)
            {
                string parentFolder = runningFolderName.ToString(0, runningFolderName.Length - 1);
                string currentFolderName = folderNames[folderNameIndex];
                runningFolderName.Append(currentFolderName);

                if (!AssetDatabase.IsValidFolder(runningFolderName.ToString()))
                {
                    Debug.Log("Creating folder: " + currentFolderName + " in " + parentFolder);
                    AssetDatabase.CreateFolder(parentFolder, currentFolderName);
                }
                runningFolderName.Append('/');

            }
        }

        /// <summary>
        /// Strips characters from a string that are disallowed from filenames
        /// </summary>
        /// <param name="filename">filename excluding the extension</param>
        /// <returns></returns>
        private static string MakeValidFileName(string filename)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(filename, invalidRegStr, "_");
        }
    }
}
