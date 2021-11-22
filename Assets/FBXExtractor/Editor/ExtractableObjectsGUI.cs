using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FBXAssetExtractor
{
    /// <summary>
    /// Class to represent lists of objects that we can extract to separate unity assets.
    /// </summary>
    /// <typeparam name="T">Type inheirited from UnityEngine.Object</typeparam>
    public class ExtractableObjectsGUI<T> where T : UnityEngine.Object
    {
        /// <summary>
        /// Objects to extract
        /// </summary>
        private List<ExtractableObject<T>> extractableObjects;

        /// <summary>
        /// Title used for the list header, and for the folder picker.
        /// </summary>
        private string listTitle;

        /// <summary>
        /// Where are we going to put these folders when we extract?
        /// </summary>
        private string targetFolder;

        /// <summary>
        /// Is the whole list of extractable objects selected?
        /// </summary>
        private bool isAllListSelected = true;

        /// <summary>
        /// Is at least one of the list of extractable objects selected?
        /// </summary>
        private bool isSomeOrAllListSelected = true;

        /// <summary>
        /// Are none of the extractable objects selected?
        /// </summary>
        private bool isNoneListSelected = false;

        /// <summary>
        /// Is the "extract all" button toggled.
        /// </summary>
        private bool extractAllToggle = true;

        /// <summary>
        /// Is the drop down button pressed?
        /// </summary>
        private bool listDroppedDown = false;

        /// <summary>
        /// Scroll position of the dropped down list.
        /// </summary>
        private Vector2 listScrollPosition;

        
        private enum SelectAllToggle
        {
            ToggleAllOn,
            ToggleAllOff,
            DontToggle
        }

        public ExtractableObjectsGUI(string title, string folder)
        {
            extractableObjects = new List<ExtractableObject<T>>();
            listTitle = title;
            targetFolder = folder;
        }

        /// <summary>
        /// Tries to add the object to the list of objects.
        /// </summary>
        /// <param name="objectToAdd">Object to add to the list.</param>
        /// <returns>True if the object was added. False otherwise.</returns>
        public bool TryAddExtractableObject(UnityEngine.Object objectToAdd)
        {
            var extractableObj = objectToAdd as T;
            if(extractableObj != null)
            {
                extractableObjects.Add(new ExtractableObject<T>(extractableObj, true));
                return true;
            }
            return false;
        }

        public void DrawGUI()
        {
            if(extractableObjects.Count == 0)
            {
                //There's nothing to draw, so don't
                return;
            }
            SelectAllToggle selectAllToggle = DrawListHeader();

            //Set these values, so that we can perform boolean algebra on them.
            //isNone and isAll are both true at the start,
            //the first item we find that is NOT selected will set isAllListSelected to false.
            //The inverse is true for isNoneListSelected
            isAllListSelected = true;
            isNoneListSelected = true;
            isSomeOrAllListSelected = false;
            listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition, GUILayout.ExpandHeight(false));
            for (int i = 0; i < extractableObjects.Count; ++i)
            {
                switch (selectAllToggle)
                {
                    case SelectAllToggle.ToggleAllOn:
                        extractableObjects[i].doExtractObject = true;
                        break;
                    case SelectAllToggle.ToggleAllOff:
                        extractableObjects[i].doExtractObject = false;
                        break;
                    default:
                        //The select all was not toggled on this draw, so do nothing to the value.
                        break;
                }
                
                if (listDroppedDown)
                {
                    extractableObjects[i].DrawGUI();
                }

                isAllListSelected &= extractableObjects[i].doExtractObject;
                isSomeOrAllListSelected |= extractableObjects[i].doExtractObject;
                isNoneListSelected &= !extractableObjects[i].doExtractObject;
            }

            if (isAllListSelected || isSomeOrAllListSelected)
            {
                extractAllToggle = true;
            }
            else if (isNoneListSelected)
            {
                extractAllToggle = false;
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the list header, and gets whether the user has selected the entire list.
        /// </summary>
        /// <returns>The toggle state of the select all toggle button.</returns>
        private SelectAllToggle DrawListHeader()
        {
            SelectAllToggle selectAllToggle = SelectAllToggle.DontToggle;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = isSomeOrAllListSelected && !isAllListSelected;
            extractAllToggle = EditorGUILayout.Toggle(extractAllToggle, GUILayout.Width(10.0f));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                selectAllToggle = extractAllToggle ? SelectAllToggle.ToggleAllOn : SelectAllToggle.ToggleAllOff;
            }
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical(GUILayout.Width(100.0f));
            listDroppedDown = EditorGUILayout.Foldout(listDroppedDown, listTitle, EditorStyles.foldoutHeader);
            EditorGUILayout.EndVertical();
            if (GUILayout.Button("Select Folder..."))
            {
                //index of last forward slash
                int lastSlashIndex = targetFolder.LastIndexOf('/');
                string folder = targetFolder.Substring(0, lastSlashIndex);
                string defaultFolder = targetFolder.Substring(lastSlashIndex + 1);
                string selectedFolder = EditorUtility.OpenFolderPanel(listTitle + " Folder", folder, defaultFolder);
                if (selectedFolder != null && selectedFolder.Length > 0)
                {
                    //Get the relative path of the selected folder
                    targetFolder = "Assets" + selectedFolder.Substring(Application.dataPath.Length);
                }
            }
            GUILayout.Label(targetFolder);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return selectAllToggle;
        }

        public Tuple<List<T>, string> GetAllObjectsToExtractAndFolder()
        {
            return new Tuple<List<T>, string>(extractableObjects.
                Where(x => x.doExtractObject).
                Select(y => y.objectToExtract).
                ToList(),
                targetFolder);
        }
    }

    public class ExtractableObject<T> where T : UnityEngine.Object
    {
        public T objectToExtract;
        public bool doExtractObject;

        private const float objectNameWidth = 400.0f;
        private const float listItemInset = 20.0f;

        public void DrawGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(listItemInset, true);
            doExtractObject = EditorGUILayout.Toggle(doExtractObject, GUILayout.Width(10.0f));
            GUI.enabled = doExtractObject;
            GUILayout.Label(objectToExtract.name, GUILayout.Width(objectNameWidth));
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        public ExtractableObject(T objectToExtract, bool doExtractObject)
        {
            this.objectToExtract = objectToExtract as T;
            this.doExtractObject = doExtractObject;
        }
    }
}