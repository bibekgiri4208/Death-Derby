using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

public class ZombieSetupWindow : EditorWindow
{
    [MenuItem("Tools/Setup Zombie Animations")]
    static void Setup()
    {
        SetFBXAnimationsToLoop();

        string controllerDir = "Assets/Prefab";
        string controllerPath = controllerDir + "/ZombieAnimatorController.controller";

        if (!AssetDatabase.IsValidFolder(controllerDir))
            Directory.CreateDirectory(controllerDir);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimationClip idleClip = FindClip("Root|Idle");
        AnimationClip runClip = FindClip("Root|Run");

        if (idleClip == null)
        {
            Debug.LogError("ZombieSetup: Could not find 'Root|Idle' clip.");
            return;
        }
        if (runClip == null)
        {
            Debug.LogError("ZombieSetup: Could not find 'Root|Run' clip.");
            return;
        }

        idleClip.wrapMode = WrapMode.Loop;
        runClip.wrapMode = WrapMode.Loop;

        EditorUtility.SetDirty(idleClip);
        EditorUtility.SetDirty(runClip);

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Speed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 0f
        });

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
        idleState.motion = idleClip;
        idleState.speed = 1f;
        sm.defaultState = idleState;

        AnimatorState runState = sm.AddState("Run", new Vector3(600f, 0f, 0f));
        runState.motion = runClip;
        runState.speed = 1f;

        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.15f;

        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.15f;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Capsule.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("ZombieSetup: Capsule.prefab not found. Assign the controller manually.");
            return;
        }

        GameObject root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        Animator animator = FindAnimatorRecursive(root);
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            Debug.Log("ZombieSetup: Assigned controller to Animator on " + animator.gameObject.name);
        }

        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
        Object.DestroyImmediate(root);

        Debug.Log("ZombieSetup: Done!");
    }

    static void SetFBXAnimationsToLoop()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/3D Models/Animated Zombies/Animations" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            bool changed = false;
            ModelImporterClipAnimation[] clipAnimations = importer.defaultClipAnimations;

            for (int i = 0; i < clipAnimations.Length; i++)
            {
                if (!clipAnimations[i].loopTime)
                {
                    clipAnimations[i].loopTime = true;
                    changed = true;
                    Debug.Log($"ZombieSetup: Set loopTime on '{clipAnimations[i].name}' in {path}");
                }
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
            }
        }
    }

    static AnimationClip FindClip(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/3D Models/Animated Zombies" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object sub in subAssets)
            {
                if (sub is AnimationClip clip && clip.name == clipName)
                    return clip;
            }
        }
        return null;
    }

    static Animator FindAnimatorRecursive(GameObject obj)
    {
        Animator a = obj.GetComponent<Animator>();
        if (a != null) return a;

        foreach (Transform child in obj.transform)
        {
            a = FindAnimatorRecursive(child.gameObject);
            if (a != null) return a;
        }
        return null;
    }
}
