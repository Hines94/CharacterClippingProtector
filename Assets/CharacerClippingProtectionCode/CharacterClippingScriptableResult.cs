using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// Represents a cached result for the character clipping protector. Can be loaded at runtime rather than re-running the results.
/// </summary>
public class CharacterClippingScriptableResult : ScriptableObject
{
    public ClippingProtectorSetup Setup;
    public ClippingProtectorResult Result;

    static CharacterClippingScriptableResult[] AllRes = null;

    static void RefreshResour() { AllRes = Resources.LoadAll<CharacterClippingScriptableResult>(""); }
    public static void InitCachedResults()
    {
        if(AllRes != null && AllRes.Length > 0) { return; }
        RefreshResour();
    }
    
    public static ClippingProtectorResult TryLoadCachedResult(ClippingProtectorSetup Setup)
    {
        CharacterClippingScriptableResult Exist = TryGetExisting(Setup);
        if(Exist != null) { return Exist.Result; }
        return new ClippingProtectorResult();
    }

    public static CharacterClippingScriptableResult TryGetExisting(ClippingProtectorSetup Setup)
    {
        InitCachedResults();
        foreach (CharacterClippingScriptableResult Res in AllRes)
        {
            if (Setup.SetupEqual(Res.Setup)) { return Res; }
        }
        return null;
    }

#if UNITY_EDITOR
    public static CharacterClippingScriptableResult SaveLastResultToCache(CharacterClippingProtector ClipProtector)
    {
        if (ClipProtector.GetOriginalMeshes().Count > 0)
        {
            if (!ClipProtector.CacheResults)
            {
                EditorUtility.DisplayDialog("Caching Not Set", "Please enable caching on the script to save to editor", "I Will");
            }
            else
            {
                Dictionary<ClippingProtectorSetup, ClippingProtectorResult> Cached = ClipProtector.GetCachedResults();
                if (Cached.Count == 0) { EditorUtility.DisplayDialog("No Simulations", "No similations have been run", "I Will Run A Simulation"); return null; }
                CharacterClippingScriptableResult Saved = CharacterClippingScriptableResult.SaveOutToCache(ClipProtector.LastSetup, Cached[ClipProtector.LastSetup]);
                //Refresh Cache
                RefreshResour();
                return Saved;
            }
        }
        return null;
    }

    public static CharacterClippingScriptableResult SaveOutToCache(ClippingProtectorSetup Setup, ClippingProtectorResult Result)
    {
        CharacterClippingScriptableResult Existing = TryGetExisting(Setup);
        if (Existing != null) {
            Debug.Log("Existing save found for Character Clipping Simulation");
            return Existing; 
        }
        CharacterClippingScriptableResult CCR = CharacterClippingScriptableResult.CreateInstance<CharacterClippingScriptableResult>();
        string ScriptableName = "CacheResult_" + System.DateTime.Now.ToString("dd_MM_yyyy_hh_mm_ss_ms");
        CheckDirExists(ScriptableName);
        //Make sure results are saved as proper meshes
        foreach(Mesh m in Result.FinalMeshes){
            AssetDatabase.CreateAsset(m, "Assets/Resources/CharacterClippingCache/" + ScriptableName + "/" + m.name + "_" + ScriptableName + ".asset");
        }
        //Save out the asset
        UnityEditor.AssetDatabase.CreateAsset(CCR, "Assets/Resources/CharacterClippingCache/" + ScriptableName + "/" + ScriptableName + ".asset");
        CCR.Result = Result;
        CCR.Setup = Setup;
        UnityEditor.EditorUtility.SetDirty(CCR);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Saved out Character Clipping Simulation");
        return CCR;
    }

    static void CheckDirExists(string Name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/CharacterClippingCache"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "CharacterClippingCache");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/CharacterClippingCache/" + Name))
        {
            AssetDatabase.CreateFolder("Assets/Resources/CharacterClippingCache", Name);
        }

    }
#endif
}

[System.Serializable]
public struct ClippingProtectorSetup
{
    public List<Mesh> InputMeshes;
    public float OutCheckDistance;
    public float InCheckDistance;
    public float MarginDistance;
    public bool LowerChain;

    public bool SetupEqual(object obj)
    {
        if (!(obj is ClippingProtectorSetup)) { return false; }
        ClippingProtectorSetup mys = (ClippingProtectorSetup)obj;
        // Compare setup variables
        if (OutCheckDistance != mys.OutCheckDistance) { return false; }
        if (InCheckDistance != mys.InCheckDistance) { return false; }
        if (MarginDistance != mys.MarginDistance) { return false; }
        if (LowerChain != mys.LowerChain) { return false; }
        //Compare mesh lists 
        if (InputMeshes.Count != mys.InputMeshes.Count) { return false; }
        var firstNotSecond = InputMeshes.Except(mys.InputMeshes).ToList();
        var secondNotFirst = mys.InputMeshes.Except(InputMeshes).ToList();
        if (firstNotSecond.Any() && secondNotFirst.Any()) { return false; }
        //Success
        return true;
    }
}
[System.Serializable]
public struct ClippingProtectorResult
{
    public List<Mesh> FinalMeshes;
    public bool IsValidResult() { return FinalMeshes != null && FinalMeshes.Count > 0; }
}
