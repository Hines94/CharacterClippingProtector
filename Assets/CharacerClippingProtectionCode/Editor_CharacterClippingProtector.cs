# if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor window for occlusion mesh hider
/// </summary>
[CustomEditor(typeof(CharacterClippingProtector))]
public class Editor_CharacterClippingProtector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CharacterClippingProtector ClipProtector = (CharacterClippingProtector)target;
        if (GUILayout.Button("Reset Meshes & Cache"))
        {
            ClipProtector.ResetMeshesToOriginal();
            ClipProtector.ResetCachedResults();
        }
        if (GUILayout.Button("Save Last Result In Resources"))
        {
            CharacterClippingScriptableResult Res =CharacterClippingScriptableResult.SaveLastResultToCache(ClipProtector);
            //Notify user of cache size & new item size
            NotifyUserCacheSizeNewSize(Res);
        }
        if (GUILayout.Button("Run Clipping Protector"))
        {
            ClipProtector.StartEditorCoroutine();
        }
    }

    void NotifyUserCacheSizeNewSize(CharacterClippingScriptableResult Res)
    {
        //New Item size
        StartCheckTime = System.DateTime.Now;
        float ItemSize = CalculateFolderSize("Assets/Resources/CharacterClippingCache/" + Res.name);
        StartCheckTime = System.DateTime.Now;
        float OverallSize = CalculateFolderSize("Assets/Resources/CharacterClippingCache");
        EditorUtility.DisplayDialog("Item Saved", "Simulation has been saved as: " + Res.name + " simulation size: " + GetSizeAsStr(ItemSize) + "MB overall cache size: " + GetSizeAsStr(OverallSize) + "MB", "Ok Thanks"); ;
    }

    string GetSizeAsStr(float Size)
    {
        if(Size < 0) { return "Extremely Large"; }//folder size has timed out
        return (Size / 1000000).ToString("0.00");
    }

    System.DateTime StartCheckTime;
    protected float CalculateFolderSize(string folder)
    {
        if((DateTime.Now - StartCheckTime).TotalSeconds > 1) { return -100000000000; }
        float folderSize = 0.0f;
        try
        {
            //Checks if the path is valid or not
            if (!Directory.Exists(folder))
                return folderSize;
            else
            {
                try
                {
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        if (File.Exists(file))
                        {
                            FileInfo finfo = new FileInfo(file);
                            folderSize += finfo.Length;
                        }
                    }

                    foreach (string dir in Directory.GetDirectories(folder))
                        folderSize += CalculateFolderSize(dir);
                }
                catch (NotSupportedException e)
                {
                    Console.WriteLine("Unable to calculate folder size: {0}", e.Message);
                }
            }
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine("Unable to calculate folder size: {0}", e.Message);
        }
        return folderSize;
    }

}
#endif