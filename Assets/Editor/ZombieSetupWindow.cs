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
        AnimationClip attack1Clip = FindClip("zombie_attack1");
        AnimationClip attack2Clip = FindClip("zombie_attack2");

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
        if (attack1Clip == null)
        {
            Debug.LogError("ZombieSetup: Could not find 'zombie_attack1' clip.");
            return;
        }
        if (attack2Clip == null)
        {
            Debug.LogError("ZombieSetup: Could not find 'zombie_attack2' clip.");
            return;
        }

        idleClip.wrapMode = WrapMode.Loop;
        runClip.wrapMode = WrapMode.Loop;
        attack1Clip.wrapMode = WrapMode.Once;
        attack2Clip.wrapMode = WrapMode.Once;

        EditorUtility.SetDirty(idleClip);
        EditorUtility.SetDirty(runClip);
        EditorUtility.SetDirty(attack1Clip);
        EditorUtility.SetDirty(attack2Clip);

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Speed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 0f
        });

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Attack1",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false
        });

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Attack2",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = false
        });

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
        idleState.motion = idleClip;
        idleState.speed = 1f;
        sm.defaultState = idleState;

        AnimatorState runState = sm.AddState("Run", new Vector3(600f, 0f, 0f));
        runState.motion = runClip;
        runState.speed = 1f;

        AnimatorState attack1State = sm.AddState("Attack1", new Vector3(450f, -100f, 0f));
        attack1State.motion = attack1Clip;
        attack1State.speed = 1f;

        AnimatorState attack2State = sm.AddState("Attack2", new Vector3(450f, -200f, 0f));
        attack2State.motion = attack2Clip;
        attack2State.speed = 1f;

        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.15f;

        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.15f;

        AnimatorStateTransition idleToAttack1 = idleState.AddTransition(attack1State);
        idleToAttack1.AddCondition(AnimatorConditionMode.If, 0f, "Attack1");
        idleToAttack1.hasExitTime = false;
        idleToAttack1.duration = 0.15f;

        AnimatorStateTransition idleToAttack2 = idleState.AddTransition(attack2State);
        idleToAttack2.AddCondition(AnimatorConditionMode.If, 0f, "Attack2");
        idleToAttack2.hasExitTime = false;
        idleToAttack2.duration = 0.15f;

        AnimatorStateTransition runToAttack1 = runState.AddTransition(attack1State);
        runToAttack1.AddCondition(AnimatorConditionMode.If, 0f, "Attack1");
        runToAttack1.hasExitTime = false;
        runToAttack1.duration = 0.15f;

        AnimatorStateTransition runToAttack2 = runState.AddTransition(attack2State);
        runToAttack2.AddCondition(AnimatorConditionMode.If, 0f, "Attack2");
        runToAttack2.hasExitTime = false;
        runToAttack2.duration = 0.15f;

        AnimatorStateTransition attack1ToIdle = attack1State.AddTransition(idleState);
        attack1ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attack1");
        attack1ToIdle.hasExitTime = true;
        attack1ToIdle.exitTime = 0.9f;
        attack1ToIdle.duration = 0.15f;

        AnimatorStateTransition attack2ToIdle = attack2State.AddTransition(idleState);
        attack2ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attack2");
        attack2ToIdle.hasExitTime = true;
        attack2ToIdle.exitTime = 0.9f;
        attack2ToIdle.duration = 0.15f;

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
                string clipName = clipAnimations[i].name;
                bool isAttackClip = clipName.Contains("attack");

                if (!isAttackClip && !clipAnimations[i].loopTime)
                {
                    clipAnimations[i].loopTime = true;
                    changed = true;
                    Debug.Log($"ZombieSetup: Set loopTime on '{clipName}' in {path}");
                }
                else if (isAttackClip && clipAnimations[i].loopTime)
                {
                    clipAnimations[i].loopTime = false;
                    changed = true;
                    Debug.Log($"ZombieSetup: Disabled loopTime on attack clip '{clipName}' in {path}");
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
