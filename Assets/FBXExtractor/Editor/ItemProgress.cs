using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ItemProgress
{
    int currentProgress;
    int maximumProgress;
    string windowTitle;

    public ItemProgress(int totalItems, string title)
    {
        currentProgress = 0;
        maximumProgress = totalItems;
        windowTitle = title;
    }

    public void IncrementCountAndDisplay(string itemName)
    {
        ++currentProgress;
        EditorUtility.DisplayProgressBar(windowTitle, itemName, (float)currentProgress / maximumProgress);
    }

    ~ItemProgress()
    {
        EditorUtility.ClearProgressBar();
    }
}
