# if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor window for occlusion mesh hider
/// </summary>
[CustomEditor(typeof(CharacterClippingProtector))]
public class Editor_HideOcclusion : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CharacterClippingProtector HideOcclude = (CharacterClippingProtector)target;
        if (GUILayout.Button("Reset Meshes & Cache"))
        {
            HideOcclude.ResetMeshesToOriginal();
            HideOcclude.ResetCachedResults();
        }
        if (GUILayout.Button("Check"))
        {
            HideOcclude.StartEditorCoroutine();
        }
    }
}
#endif