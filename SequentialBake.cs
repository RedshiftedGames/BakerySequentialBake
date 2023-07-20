#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SequentialBake : ScriptableWizard {
    [Header("Denoiser")]
    public bool SettingsDenoise = false;
    [Tooltip("(5, 6, 7 on GPU. 100 for software.)")]
    public int SettingsDenoiserType = 6;
    [Header("Quality")]
    public int SettingsTexelsPerUnit = 20;
    [Header("Performance")]
    public int SettingsTileSize = 1024;

    private static int sectorCount;
    private static IEnumerator progressFunc;

    private static ftRenderLightmap bakery;
    private static ftLightmapsStorage storage;

    private static List<string> sectors;
    private static bool _SettingsDenoise;
    private static int _SettingsDenoiserType;
    private static int _SettingsTileSize;
    private static int _SettingsTexelsPerUnit;

    static IEnumerator BatchBakeFunc() {
        BakerySector[] bakerySectors = Component.FindObjectsOfType<BakerySector>();
        sectors.Clear();
        for (int i = 0; i < bakerySectors.Length; i++)
            sectors.Add(bakerySectors[i].gameObject.name);

        sectorCount = bakerySectors.Length;

        // Save
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();

        // Bake each zone in series
        for (int i = 0; i < sectorCount; i++) {
            GameObject sectorGameObject = GameObject.Find(sectors[i]);
            EditorSceneManager.SetActiveScene(sectorGameObject.scene);

            bakery = ftRenderLightmap.instance;
            storage = ftRenderLightmap.FindRenderSettingsStorage();

            storage.renderSettingsSector = sectorGameObject.gameObject.GetComponent<BakerySector>();

            Debug.Log("BatchBakery: Loading " + sectorGameObject.gameObject.GetComponent<BakerySector>().gameObject.name);

            storage.renderSettingsBounces = 1;
            storage.renderSettingsGISamples = 16;

            // Hard options
            storage.renderSettingsGIBackFaceWeight = 0;
            storage.renderSettingsTileSize = _SettingsTileSize;
            storage.renderSettingsPriority = 3;
            storage.renderSettingsTexelsPerUnit = _SettingsTexelsPerUnit;
            storage.renderSettingsForceRefresh = true;
            storage.renderSettingsForceRebuildGeometry = true;
            storage.renderSettingsPerformRendering = true;
            storage.renderSettingsUserRenderMode = 0;
            storage.renderSettingsDistanceShadowmask = false;
            storage.renderSettingsSettingsMode = 2;
            storage.renderSettingsFixSeams = true;
            storage.renderSettingsDenoise = _SettingsDenoise;
            storage.renderSettingsDenoise2x = _SettingsDenoise;
            storage.renderSettingsEncode = true;
            storage.renderSettingsEncodeMode = 0;
            storage.renderSettingsOverwriteWarning = false;
            storage.renderSettingsAutoAtlas = true;
            storage.renderSettingsUnwrapUVs = true;
            storage.renderSettingsForceDisableUnwrapUVs = false;
            storage.renderSettingsMaxAutoResolution = 4096;
            storage.renderSettingsMinAutoResolution = 16;
            storage.renderSettingsUnloadScenes = true;
            storage.renderSettingsAdjustSamples = true;
            storage.renderSettingsGILODMode = 2;
            storage.renderSettingsGILODModeEnabled = false;
            storage.renderSettingsCheckOverlaps = true;
            storage.renderSettingsSkipOutOfBoundsUVs = true;
            storage.renderSettingsHackEmissiveBoost = 1;
            storage.renderSettingsHackIndirectBoost = 1;
            storage.renderSettingsTempPath = "";
            storage.renderSettingsOutPath = "BakeryLightmaps";
            storage.renderSettingsUseScenePath = false;
            storage.renderSettingsHackAOIntensity = 0;
            storage.renderSettingsHackAOSamples = 0;
            storage.renderSettingsHackAORadius = 0;
            storage.renderSettingsShowAOSettings = false;
            storage.renderSettingsShowTasks = false;
            storage.renderSettingsShowTasks2 = false;
            storage.renderSettingsShowPaths = false;
            storage.renderSettingsShowNet = false;
            storage.renderSettingsOcclusionProbes = false;
            storage.renderSettingsTexelsPerMap = false;
            storage.renderSettingsTexelsColor = 1;
            storage.renderSettingsTexelsMask = 1;
            storage.renderSettingsTexelsDir = 1;
            storage.renderSettingsShowDirWarning = false;
            storage.renderSettingsRenderDirMode = 1;
            storage.renderSettingsShowCheckerSettings = false;
            storage.renderSettingsSamplesWarning = false;
            storage.renderSettingsSuppressPopups = true;
            storage.renderSettingsPrefabWarning = false;
            storage.renderSettingsSplitByScene = false;
            storage.renderSettingsSplitByTag = false;
            storage.renderSettingsUVPaddingMax = false;
            storage.renderSettingsPostPacking = true;
            storage.renderSettingsHoleFilling = true;
            storage.renderSettingsBeepOnFinish = false;
            storage.renderSettingsExportTerrainAsHeightmap = false;
            storage.renderSettingsRTXMode = true;
            storage.renderSettingsLightProbeMode = 0;
            storage.renderSettingsClientMode = false;
            storage.renderSettingsUnwrapper = 1;
            storage.renderSettingsDenoiserType = _SettingsDenoiserType; // Optix 5/6/7 are 5/6/7, OID is 100.
            storage.renderSettingsExportTerrainTrees = true;
            storage.renderSettingsShowPerf = false;
            storage.renderSettingsSampleDiv = 1;
            storage.renderSettingsAtlasPacker = ftGlobalStorage.AtlasPacker.xatlas;
            storage.renderSettingsBatchPoints = true;
            storage.renderSettingsCompressVolumes = false;


            // Save settings and scene
            EditorUtility.SetDirty(storage);
            EditorSceneManager.SaveOpenScenes();

            ftLightmaps.RefreshFull();
            bakery.LoadRenderSettings();

            // Perform the render
            bakery.RenderButton(false);
            // Wait for it to complete
            while (ftRenderLightmap.bakeInProgress) {
                yield return null;
            }

            EditorSceneManager.SaveOpenScenes();
        }

        Debug.LogWarning("SequentialBake: All Sectors Batch Bake Complete");
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
    }

    static void BatchBakeUpdate() {
        if (progressFunc.MoveNext()) return;
        EditorApplication.update -= BatchBakeUpdate;
    }

    void OnWizardCreate() {
        GameObject[] lightmaps = FindGameObjectsWithName("!ftraceLightmaps");
        Debug.Log("Removing " + lightmaps.Length + " !ftraceLightmaps.");
        for (int i = 0; i < lightmaps.Length; i++) {
            DestroyImmediate(lightmaps[i]);
        }
        _SettingsDenoiserType = SettingsDenoiserType;
        _SettingsDenoise = SettingsDenoise;
        _SettingsTileSize = SettingsTileSize;
        _SettingsTexelsPerUnit = SettingsTexelsPerUnit;

        sectors = new List<string>();
        sectors.Clear();
        progressFunc = BatchBakeFunc();
        EditorApplication.update += BatchBakeUpdate;
    }

    [MenuItem("Bakery/Utilities/Sequential Sector Bake")]
    public static void sequentialBake() {
        ScriptableWizard.DisplayWizard("Sequential Sector Bake", typeof(SequentialBake), "Sequential Sector Bake");
    }

    GameObject[] FindGameObjectsWithName(string name) {
        int a = GameObject.FindObjectsOfType<GameObject>().Length;
        GameObject[] arr = new GameObject[a];
        int FluentNumber = 0;
        for (int i = 0; i < a; i++) {
            if (GameObject.FindObjectsOfType<GameObject>()[i].name == name) {
                arr[FluentNumber] = GameObject.FindObjectsOfType<GameObject>()[i];
                FluentNumber++;
            }
        }
        Array.Resize(ref arr, FluentNumber);
        return arr;
    }
}

#endif