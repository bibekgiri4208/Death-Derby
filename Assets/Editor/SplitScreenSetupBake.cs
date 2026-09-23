using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor helper: bakes real Player 2 objects (car, camera, kill counter) into the
/// current scene and wires them to a scene-placed SplitScreenMode, so everything is
/// visible and editable in the editor instead of being created at runtime.
/// Run via: Death Derby > Split Screen > Bake Player 2 Objects in Scene
/// </summary>
public static class SplitScreenSetupBake
{
    [MenuItem("Death Derby/Split Screen/Bake Player 2 Objects in Scene")]
    public static void BakeSceneObjects()
    {
        SplitScreenMode mode = Object.FindAnyObjectByType<SplitScreenMode>();
        if (mode == null)
        {
            GameObject modeGo = new GameObject("Split Screen Mode");
            mode = modeGo.AddComponent<SplitScreenMode>();
            Undo.RegisterCreatedObjectUndo(modeGo, "Split Screen Bake: create mode");
        }

        EditMode_SetPlayer2Car(mode);
        EditMode_SetPlayer2Camera(mode);
        EditMode_SetKillCounters();
        EditMode_SetKillStreakPopups();
        EditMode_SetPlayer1Camera();
        EditMode_CreateDivider();

        EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
        Debug.Log("Split Screen bake complete. Save the scene to keep the changes.");
    }

    private static void EditMode_SetPlayer2Car(SplitScreenMode mode)
    {
        CarController player1 = Object.FindAnyObjectByType<CarController>();
        if (player1 == null)
        {
            EditorUtility.DisplayDialog("Split Screen Bake", "No CarController (Player 1) found in the scene.", "OK");
            return;
        }

        if (mode.player2Car == null)
        {
            Transform p1Root = player1.transform.root;
            CarController p1Controller = p1Root.GetComponent<CarController>();
            GameObject clone = Object.Instantiate(p1Root.gameObject);
            clone.name = p1Root.name + " (Player 2)";
            Undo.RegisterCreatedObjectUndo(clone, "Split Screen Bake: player 2 car");
            clone.transform.SetPositionAndRotation(
                player1.transform.position + player1.transform.TransformVector(mode.player2CloneOffset),
                player1.transform.rotation);

            CarController player2 = clone.GetComponent<CarController>();
            if (p1Controller != null && player2 == null)
                player2 = clone.GetComponentInChildren<CarController>();
            if (player2 != null)
                player2.playerIndex = 1;

            foreach (RainFollower follower in clone.GetComponentsInChildren<RainFollower>(true))
            {
                AudioSource rainAudio = follower.GetComponent<AudioSource>();
                if (rainAudio != null)
                    Undo.DestroyObjectImmediate(rainAudio);
            }

            if (player2 != null)
            {
                Undo.RecordObject(mode, "Split Screen Bake: assign player 2 car");
                mode.player2Car = player2.gameObject;
            }
        }
    }

    private static void EditMode_SetPlayer2Camera(SplitScreenMode mode)
    {
        CarFollowCamera player1Cam = FindPlayer1FollowCamera();
        if (player1Cam == null)
            return;

        Camera baseCamera = player1Cam.GetComponent<Camera>();
        Camera cam1 = baseCamera != null ? baseCamera : Camera.main;

        if (mode.player2Camera == null)
        {
            GameObject duplicate = Object.Instantiate(player1Cam.gameObject);
            duplicate.name = player1Cam.gameObject.name + " (Player 2)";
            Undo.RegisterCreatedObjectUndo(duplicate, "Split Screen Bake: player 2 camera");

            CarFollowCamera follow2 = duplicate.GetComponent<CarFollowCamera>();
            if (follow2 != null)
            {
                follow2.target = mode.player2Car != null ? mode.player2Car.transform : player1Cam.target;
                follow2.playerIndex = 1;
            }

            Camera cam2 = duplicate.GetComponent<Camera>();
            if (cam2 != null)
            {
                cam2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

                AudioListener listener = duplicate.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;

                Undo.RecordObject(mode, "Split Screen Bake: assign player 2 camera");
                mode.player2Camera = cam2;
            }
        }
        else
        {
            AudioListener listener = mode.player2Camera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        if (cam1 != null)
            cam1.rect = new Rect(0f, 0f, 0.5f, 1f);
    }

    private static CarFollowCamera FindPlayer1FollowCamera()
    {
        CarController player1 = Object.FindAnyObjectByType<CarController>();
        CarFollowCamera match = null;

        foreach (CarFollowCamera followCamera in Object.FindObjectsByType<CarFollowCamera>())
        {
            if (player1 != null && followCamera.target == player1.transform)
            {
                match = followCamera;
                break;
            }
        }

        if (match != null)
            return match;

        foreach (CarFollowCamera followCamera in Object.FindObjectsByType<CarFollowCamera>())
        {
            if (followCamera.playerIndex == 0)
            {
                match = followCamera;
                break;
            }
        }

        return match;
    }

    private static void EditMode_SetPlayer1Camera()
    {
        CarFollowCamera player1Cam = FindPlayer1FollowCamera();
        if (player1Cam == null)
            return;

        Camera cam1 = player1Cam.GetComponent<Camera>();
        if (cam1 == null)
            cam1 = Camera.main;

        if (cam1 != null)
            cam1.rect = new Rect(0f, 0f, 0.5f, 1f);
    }

    private static void EditMode_SetKillCounters()
    {
        KillCounter[] counters = Object.FindObjectsByType<KillCounter>();
        if (counters.Length == 0)
            return;

        KillCounter p1 = null;
        KillCounter p2 = null;
        foreach (KillCounter counter in counters)
        {
            if (counter.PlayerIndex == 1)
                p2 = counter;
            else if (p1 == null)
                p1 = counter;
        }

        if (p1 == null)
            return;

        AnchorCounterOnScreenHalf(p1.GetComponent<RectTransform>(), leftHalf: true);

        if (p2 == null)
        {
            Transform parent = p1.transform.parent;
            GameObject p2CounterGo = Object.Instantiate(p1.gameObject, parent);
            p2CounterGo.name = p1.gameObject.name + " (Player 2)";
            Undo.RegisterCreatedObjectUndo(p2CounterGo, "Split Screen Bake: player 2 kill counter");

            KillCounter p2Counter = p2CounterGo.GetComponent<KillCounter>();
            TextMeshProUGUI p2Text = p2CounterGo.GetComponent<TextMeshProUGUI>();
            if (p2Counter != null && p2Text != null)
            {
                SerializedObject serialized = new SerializedObject(p2Counter);
                serialized.FindProperty("killCountText").objectReferenceValue = p2Text;
                serialized.FindProperty("playerIndex").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            p2 = p2Counter;
        }

        if (p2 != null)
            AnchorCounterOnScreenHalf(p2.GetComponent<RectTransform>(), leftHalf: false);
    }

    private static void EditMode_SetKillStreakPopups()
    {
        KillStreakPopup[] popups = Object.FindObjectsByType<KillStreakPopup>();
        if (popups.Length == 0)
            return;

        KillStreakPopup p1 = null;
        KillStreakPopup p2 = null;
        foreach (KillStreakPopup popup in popups)
        {
            if (popup.PlayerIndex == 1)
                p2 = popup;
            else if (p1 == null)
                p1 = popup;
        }

        if (p1 == null)
            return;

        if (p2 == null)
        {
            Transform parent = p1.transform.parent;
            GameObject p2PopupGo = Object.Instantiate(p1.gameObject, parent);
            p2PopupGo.name = p1.gameObject.name + " (Player 2)";
            Undo.RegisterCreatedObjectUndo(p2PopupGo, "Split Screen Bake: player 2 kill popup");

            KillStreakPopup p2Popup = p2PopupGo.GetComponent<KillStreakPopup>();
            if (p2Popup != null)
            {
                SerializedObject serialized = new SerializedObject(p2Popup);
                serialized.FindProperty("playerIndex").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            p2 = p2Popup;
        }

        AnchorPopupOnScreenHalf(p1.GetComponent<RectTransform>(), leftHalf: true);
        if (p2 != null)
            AnchorPopupOnScreenHalf(p2.GetComponent<RectTransform>(), leftHalf: false);
    }

    private static void AnchorPopupOnScreenHalf(RectTransform rectTransform, bool leftHalf)
    {
        if (rectTransform == null)
            return;

        Undo.RecordObject(rectTransform, "Split Screen Bake: anchor kill popup");
        Vector2 anchor = new Vector2(leftHalf ? 0.25f : 0.75f, rectTransform.anchorMin.y);
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;

        Vector2 position = rectTransform.anchoredPosition;
        position.x = 0f;
        position.y = -180f;
        rectTransform.anchoredPosition = position;
    }

    private static void EditMode_CreateDivider()
    {
        bool existing = false;
        foreach (Image image in Object.FindObjectsByType<Image>())
        {
            if (image.gameObject.name == "Split Screen Divider")
            {
                existing = true;
                break;
            }
        }

        if (existing)
            return;

        Canvas canvas = null;
        foreach (Canvas candidate in Object.FindObjectsByType<Canvas>())
        {
            if (candidate.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas = candidate;
                break;
            }

            if (canvas == null)
                canvas = candidate;
        }

        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Split Screen Divider Canvas", typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Split Screen Bake: divider canvas");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject dividerGo = new GameObject("Split Screen Divider", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(dividerGo, "Split Screen Bake: divider");

        RectTransform rect = dividerGo.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-2f, 0f);
        rect.offsetMax = new Vector2(2f, 0f);

        Image dividerImage = dividerGo.GetComponent<Image>();
        dividerImage.color = Color.black;
        dividerImage.raycastTarget = false;

        dividerGo.transform.SetAsLastSibling();
    }

    private static void AnchorCounterOnScreenHalf(RectTransform rectTransform, bool leftHalf)
    {
        if (rectTransform == null)
            return;

        Undo.RecordObject(rectTransform, "Split Screen Bake: anchor kill counter");
        rectTransform.anchorMin = new Vector2(0.5f, rectTransform.anchorMin.y);
        rectTransform.anchorMax = new Vector2(0.5f, rectTransform.anchorMax.y);
        rectTransform.pivot = new Vector2(leftHalf ? 1f : 0f, rectTransform.pivot.y);

        Vector2 position = rectTransform.anchoredPosition;
        position.x = leftHalf ? -60f : 60f;
        rectTransform.anchoredPosition = position;
    }
}