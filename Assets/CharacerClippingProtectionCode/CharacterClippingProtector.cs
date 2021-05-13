using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Burst;
using System;
public enum CharClipDebugLevel
{
    None,
    ShowHits,
    ShowAll,
}
public enum AsyncHarshness
{
    None,
    Soft,
    Harsh
}

public class CharacterClippingProtector : MonoBehaviour
{
    [Tooltip("HAS TO BE SINGLE LAYER!! Unique Layer to use for checking meshes.")]
    public LayerMask UniqueLayerMask;
    [Tooltip("Will work through from fist to last")]
    public List<SkinnedMeshRenderer> Meshes = new List<SkinnedMeshRenderer>();
    [Tooltip("Do we want meshes with lower indexes NOT to affect higher index meshes?")]
    public bool PreventLowerChainItems = true;
    [Tooltip("Distance to check from each vertex for overlapping meshes")]
    [Range(0.05f,0.5f)]
    public float OutwardCheckDistance = 0.1f;
    [Tooltip("Distance to check from each vertex inward for overlapping meshes")]
    [Range(0.00f, 0.5f)]
    public float InwardCheckDistance = 0.1f;
    [Tooltip("The ammount of vertices from a non occluded vert to cull")]
    [Range(0.0f, 0.5f)]
    public float MarginDistance = 0.1f;
    [Tooltip("Cache results to prevent multiple runs of the same meshes?")]
    public bool CacheResults = true;
    [Tooltip("Run processes over multiple frames?")]
    public AsyncHarshness RunAsynch = AsyncHarshness.None;
    public CharClipDebugLevel DebugLevel = CharClipDebugLevel.None;
    public float DebugLineTime = 3f;
    [Tooltip("Automatically rerun the simulation when changing a slider value?")]
    public bool AutoRerunOnChange = true;

    Color[] MeshCols = new Color[] { Color.red, Color.blue, Color.green, Color.white, Color.grey, Color.cyan };
    Dictionary<SkinnedMeshRenderer,Mesh> OriginalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
    public Dictionary<SkinnedMeshRenderer, Mesh> GetOriginalMeshes() { return OriginalMeshes; }
    List<GameObject> Colliders = new List<GameObject>();
    static Dictionary<ClippingProtectorSetup, ClippingProtectorResult> CachedResults = new Dictionary<ClippingProtectorSetup, ClippingProtectorResult>();
    public Dictionary<ClippingProtectorSetup, ClippingProtectorResult> GetCachedResults() { return CachedResults; }
    public ClippingProtectorSetup LastSetup;
    public void ResetCachedResults() { CachedResults = new Dictionary<ClippingProtectorSetup, ClippingProtectorResult>(); }

    /// <summary>
    /// Main method. Runs a raycast simulation and hides parts of mesh that are obscured. 
    /// NOTE has to be IEnumerator otherwise created collision meshes do not provide any raycast result on same frame.
    /// </summary>
    public IEnumerator RunClippingSimulation(Action OnSimulationComplete = null)
    {
        //TODO best to run in T or A pose!
        //TODO make sure that we are fully reset the meshes before re running!!

        //Make sure we are working with the propper meshes and not already partially hidden
        ResetMeshesToOriginal();

        //Check our cache to see if we have already run
        if (CacheResults)
        {
            List<Mesh> CheckValues = new List<Mesh>();
            foreach(SkinnedMeshRenderer smr in Meshes)
            {
                if(smr == null || smr.sharedMesh == null) { continue; }
                CheckValues.Add(smr.sharedMesh);
            }
            ClippingProtectorSetup Check = new ClippingProtectorSetup
            {
                OutCheckDistance = OutwardCheckDistance,
                InputMeshes = CheckValues,
                MarginDistance = MarginDistance,
                InCheckDistance = InwardCheckDistance,
                LowerChain = PreventLowerChainItems
            };
            if (CheckCached(Check)) { yield break; }
        }
        if(DebugLevel != CharClipDebugLevel.None) { Debug.Log("Could not find cached simulation. Running clipping check."); }

        //Create collider meshes that we can test against
        for (int i = 0; i < Colliders.Count; i++) { DestroyImmediate(Colliders[i]); }
        Colliders = new List<GameObject>();
        CreateColliders(Colliders);
        yield return null; //REQUIRED!

        //Foreach mesh in meshes check occlude
        int Index = 0;
        List<Mesh> FinalMeshes = new List<Mesh>();
        foreach (SkinnedMeshRenderer smr in Meshes)
        {
            if (smr == null || smr.sharedMesh == null) { continue; }

            //Setup so we dont trigger ourselves or any mesh lower down the chain
            if (PreventLowerChainItems)
            {
                Colliders[Index].gameObject.SetActive(false);
                yield return null;
            }
            //if(Index > 0) { Colliders[Index - 1].gameObject.SetActive(true); }
            OriginalMeshes.Add(smr, smr.sharedMesh);

            //Raycast for results
            bool[] Results = RaycastToFindOverlap(Colliders, Index);

            //If we have nothing to hide then no point to change the mesh etc!
            if (!Results.Contains(true)) { FinalMeshes.Add(smr.sharedMesh); continue; }

            //Identify the triangles that we no longer want and remove them
            CalculateNewTrisJob newJob = new CalculateNewTrisJob {
                NewTris = new NativeList<int>(Allocator.TempJob),
                OriginalTris = new NativeArray<int>(smr.sharedMesh.triangles, Allocator.TempJob),
                Results = new NativeArray<bool>(Results,Allocator.TempJob)
            };
            newJob.Schedule().Complete();
            if (RunAsynch == AsyncHarshness.Harsh) { yield return null; }

            //Setup instance mesh ready for hiding elements
            Mesh newm = Instantiate(smr.sharedMesh);
            newm.triangles = newJob.NewTris.ToArray();
            newm.name = "Clone_" + smr.sharedMesh.name;
            smr.sharedMesh = newm;
            FinalMeshes.Add(newm);

            //Finalise
            newJob.Cleanup();
            Index++;

            if (RunAsynch == AsyncHarshness.Soft) { yield return null; }
        }
        //Clear up old junk colliders
        for (int i = 0; i < Colliders.Count; i++) { DestroyImmediate(Colliders[i]); }

        //Add result to cache for quick rerun of any future meshes
        if(CacheResults)
        {
            LastSetup = new ClippingProtectorSetup
            {
                OutCheckDistance = OutwardCheckDistance,
                InputMeshes = OriginalMeshes.Values.ToList(),
                MarginDistance = MarginDistance,
                InCheckDistance = InwardCheckDistance,
                LowerChain = PreventLowerChainItems
            };

            CachedResults.Add(LastSetup,
            new ClippingProtectorResult
            {
                FinalMeshes = FinalMeshes
            });
        }

        //Notify of completion
        if(OnSimulationComplete != null) { OnSimulationComplete.Invoke(); }
    }

    /// <summary>
    /// Raycasts to find the overlap of meshes from the outside in
    /// </summary>
    private bool[] RaycastToFindOverlap(List<GameObject> Colliders, int Index)
    {
        //Have to get mesh from colliders in case of weird scaling issues
        Mesh M = Colliders[Index].GetComponent<MeshCollider>().sharedMesh;
        //Setup
        List<GameObject> OurCollideGOs = new List<GameObject>() { Colliders[Index].gameObject };
        if (PreventLowerChainItems) { for (int i = 0; i < Index; i++) { OurCollideGOs.Add(Colliders[i]); } }
        Color MeshCol = MeshCols[Index % MeshCols.Count()];
        //Setup ready for raycast
        var results = new NativeArray<RaycastHit>(M.vertexCount, Allocator.TempJob);
        var commands = new NativeArray<RaycastCommand>(M.vertexCount, Allocator.TempJob);
        for (int i = 0; i < M.vertexCount; i++)
        {
            Vector3 Position = Colliders[Index].transform.TransformPoint(M.vertices[i]);
            Vector3 Normal = Colliders[Index].transform.TransformDirection(M.normals[i]);
            commands[i] = new RaycastCommand(Position + (Normal.normalized * OutwardCheckDistance), Normal.normalized * -1, OutwardCheckDistance + InwardCheckDistance, UniqueLayerMask.value);
        }
        // Wait for the batch processing job to complete
        JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));
        handle.Complete();

        //Run checks as a job for much faster results
        bool[] Results = new bool[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].collider == null) { Results[i] = true;  }
            else if (OurCollideGOs.Contains(results[i].collider.gameObject)) { Results[i] = true; Debug.Log("Triggered " + results[i].collider.gameObject); }
            else { Results[i] = false; }
        }

        //Debug
        if (DebugLevel != CharClipDebugLevel.None)
        {
            for (int i = 0; i < Results.Length; i++)
            {
                //Hit something that isnt us?
                if (!Results[i])
                {
                    if (DebugLevel == CharClipDebugLevel.ShowAll) Debug.DrawLine(commands[i].from, commands[i].from + commands[i].direction * commands[i].distance, Color.white, DebugLineTime);
                    if (DebugLevel == CharClipDebugLevel.ShowHits) Debug.DrawLine(commands[i].from, commands[i].from + commands[i].direction * commands[i].distance, MeshCol, DebugLineTime);
                }
                else
                {
                    if (DebugLevel == CharClipDebugLevel.ShowAll) Debug.DrawLine(commands[i].from, commands[i].from + commands[i].direction * commands[i].distance, MeshCol, DebugLineTime);
                }
            }
        }

        //Run final pass to check nearness to edge
        CheckVertPassedTests CheckTest = new CheckVertPassedTests
        {
            PassedIndividual = new NativeArray<bool>(Results, Allocator.TempJob),
            Margin = MarginDistance,
            Origins = commands,
            FinalPassResult = new NativeArray<bool>(Results, Allocator.TempJob)
        };
        CheckTest.Schedule(Results.Length, 1).Complete();

        //Have we passed the test?
        Results = CheckTest.FinalPassResult.ToArray();

        //Cleanup
        CheckTest.Cleanup();
        results.Dispose();
        commands.Dispose();
        return Results;
    }

    /// <summary>
    /// Create mesh colliders that we can accurately check against
    /// </summary>
    /// <param name="Colliders"></param>
    private void CreateColliders(List<GameObject> Colliders)
    {
        int Found = 0;
        foreach (SkinnedMeshRenderer smr in Meshes)
        {
            if (smr == null || smr.sharedMesh == null) { continue; }
            Mesh collider = new Mesh();
            smr.BakeMesh(collider);
            GameObject NewGO = CreateColliderMeshObject(smr, collider);
            Colliders.Add(NewGO);
        }
    }

    /// <summary>
    /// Reset our meshes back to originals
    /// </summary>
    public void ResetMeshesToOriginal()
    {
        if (OriginalMeshes.Count == 0) return;
        foreach(SkinnedMeshRenderer key in OriginalMeshes.Keys)
        {
            if(key == null) { continue; }
            key.sharedMesh = OriginalMeshes[key];
        }
        OriginalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
    }

    /// <summary>
    /// Create a temporary object that we can use to raycast against for accurate results
    /// </summary>
    private GameObject CreateColliderMeshObject(SkinnedMeshRenderer smr, Mesh NewMesh)
    {
        GameObject NewGO = new GameObject();
        MeshCollider MC = (MeshCollider)NewGO.AddComponent(typeof(MeshCollider));
        NewGO.name = "TEMP_Collider_" + smr.gameObject.name;
        MC.sharedMesh = NewMesh;
        MC.gameObject.layer = GetLayerFromMask(UniqueLayerMask);
        NewGO.transform.position = smr.transform.position;
        NewGO.transform.rotation = smr.transform.rotation;
        return NewGO;
    }

    /// <summary>
    /// Check our cached previous simulations and see if we can skip the simulation process
    /// </summary>
    private bool CheckCached(ClippingProtectorSetup Check)
    {
        //Check local cached
        foreach(ClippingProtectorSetup cs in CachedResults.Keys)
        {
            if(cs.SetupEqual(Check))
            {
                List<Mesh> NewMeshes = CachedResults[cs].FinalMeshes;
                SetFromCachedMeshes(NewMeshes);
                return true;
            }
        }
        //Check Saved cache scriptbales
        ClippingProtectorResult Res = CharacterClippingScriptableResult.TryLoadCachedResult(Check);
        if(Res.FinalMeshes != null && Res.FinalMeshes.Count > 0)
        {
            SetFromCachedMeshes(Res.FinalMeshes);
            return true;
        }
        return false;
    }

    private void SetFromCachedMeshes(List<Mesh> NewMeshes)
    {
        OriginalMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
        if (DebugLevel != CharClipDebugLevel.None) Debug.Log("Existing simulation found!");
        int Ind = 0;
        foreach (SkinnedMeshRenderer smr in Meshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            OriginalMeshes.Add(smr, smr.sharedMesh);
            smr.sharedMesh = NewMeshes[Ind];
            Ind++;
        }
    }



    //Benefit from burst to speed this up significantly
    [BurstCompile(CompileSynchronously = true)]
    private struct CheckVertPassedTests : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> Origins;
        [NativeDisableParallelForRestriction]
        public NativeArray<bool> FinalPassResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<bool> PassedIndividual;
        [ReadOnly]
        public float Margin;
        [ReadOnly]
        public float OutwardCheckDistance;

        public void Cleanup()
        {
            //Dont dispose commands as coming from somewhere else
            FinalPassResult.Dispose();
            PassedIndividual.Dispose();
        }

        public void Execute(int index)
        {
            //Not hit anything then we can consider ourselves passed
            if (PassedIndividual[index]) { FinalPassResult[index] = true;return; }
            //Else we still may pass if we are close enough to a pass
            else
            {
                for (int i = 0; i < Origins.Length; i++)
                {
                    //If other has passed and we are close enough then we pass anyway
                    if (index == i) { continue; }
                    if (!PassedIndividual[i]) { continue; }
                    float Dist = (GetRealOrigin(Origins[i]) - GetRealOrigin(Origins[index])).magnitude;
                    if(Dist < Margin) { FinalPassResult[index] = true;return; }
                }
            }
            //All Fail
            FinalPassResult[index] = false;
        }


        public Vector3 GetRealOrigin(RaycastCommand Command)
        {
            return Command.from + (Command.direction * OutwardCheckDistance);
        }
    }

    //Benefit from burst to speed this up significantly
    [BurstCompile(CompileSynchronously = true)]
    private struct CalculateNewTrisJob : IJob
    {
        public NativeArray<int> OriginalTris;
        public NativeArray<bool> Results;
        public NativeList<int> NewTris;

        public void Cleanup()
        {
            OriginalTris.Dispose();
            Results.Dispose();
            NewTris.Dispose();
        }

        public void Execute()
        {
            for (int t = 0; t < OriginalTris.Length; t += 3)
            {
                //If all three tris are inside the "Zone"
                if (!Results[OriginalTris[t]] &&
                   !Results[OriginalTris[t + 1]] &&
                   !Results[OriginalTris[t + 2]])
                {
                    //Is not allowed
                }
                else
                {
                    //Is allowed
                    NewTris.Add(OriginalTris[t]);
                    NewTris.Add(OriginalTris[t + 1]);
                    NewTris.Add(OriginalTris[t + 2]);
                }
            }
        }
    }

    /// <summary>
    /// Returns a value between [0;31].
    /// Important: This will only work properly if the LayerMask is one created in the inspector and not a LayerMask
    /// with multiple values.
    /// </summary>
    public static int GetLayerFromMask(LayerMask _mask)
    {
        var bitmask = _mask.value;
        int result = bitmask > 0 ? 0 : 31;
        while (bitmask > 1)
        {
            bitmask = bitmask >> 1;
            result++;
        }
        return result;
    }

#if UNITY_EDITOR
    public Unity.EditorCoroutines.Editor.EditorCoroutine EditorCoroutine = null;
    private void OnValidate()
    {
        if (!AutoRerunOnChange || OriginalMeshes.Count == 0) { return; }
        StartEditorCoroutine();
    }

    public void StartEditorCoroutine()
    {
        StopAllCoroutines();
        if (EditorCoroutine != null) { Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StopCoroutine(EditorCoroutine); }
        if (Application.isPlaying) { StartCoroutine(RunClippingSimulation()); }
        else { EditorCoroutine = Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutine(RunClippingSimulation(), this); }
    }
#endif
}
