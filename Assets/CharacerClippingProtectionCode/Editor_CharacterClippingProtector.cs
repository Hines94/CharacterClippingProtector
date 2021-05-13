# if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
            CharacterClippingScriptableResult.SaveLastResultToCache(ClipProtector);
        }
        if (GUILayout.Button("Run Clipping Protector"))
        {
            ClipProtector.StartEditorCoroutine();
        }
    }
}
#endif