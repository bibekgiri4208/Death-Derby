using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SplitScreenMode : MonoBehaviour
{
    public static bool Active { get; private set; }

    private const string AutoEnableSceneName = "Coop";
    private const string DefaultPlayerPrefsKey = "SplitScreenMode";

    [Header("Enable")]
    [Tooltip("Enables split-screen mode when this scene starts.")]
    public bool enableOnStart = true;
    [Tooltip("If true, the SplitScreenMode PlayerPrefs key overrides enableOnStart. " +
             "Set it before loading a level with SplitScreenMode.SetPreferred(true).")]
    public bool usePlayerPrefsOverride = false;
    [Tooltip("PlayerPrefs key used when usePlayerPrefsOverride is enabled.")]
    public string playerPrefsKey = DefaultPlayerPrefsKey;

    [Header("Player 2 Car (optional)")]
    [Tooltip("A second car already placed in the scene. Leave empty to duplicate Player 1's car.")]
    public GameObject player2Car;
    [Tooltip("Prefab to spawn for Player 2 (takes priority over duplicating Player 1's car).")]
    public GameObject player2CarPrefab;
    [Tooltip("Where player2CarPrefab is spawned. Leave empty to spawn beside Player 1.")]
    public Transform player2SpawnPoint;
    [Tooltip("Offset from Player 1 (in Player 1's local space) when duplicating Player 1's car.")]
    public Vector3 player2CloneOffset = new Vector3(3f, 0f, 0f);

    [Header("Player 2 Camera (optional)")]
    [Tooltip("Second camera for the right-hand split. Leave empty to duplicate Player 1's camera.")]
    public Camera player2Camera;

    [Header("Screen Divider")]
    [Tooltip("Draws a vertical line between the two split-screen halves.")]
    public bool showDivider = true;
    [Tooltip("Color of the divider line.")]
    public Color dividerColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("Width (in screen pixels) of the divider line.")]
    public float dividerWidth = 4f;

    private static readonly List<Transform> players = new List<Transform>();
    private GameObject dividerObject;

    public static Gamepad GetPad(int playerIndex)
    {
        var pads = Gamepad.all;
        if (playerIndex >= 0 && playerIndex < pads.Count)
            return pads[playerIndex];
        return null;
    }

    public static Transform GetNearestPlayer(Vector3 from)
    {
        if (Active)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;

            for (int i = players.Count - 1; i >= 0; i--)
            {
                Transform candidate = players[i];
                if (candidate == null)
                {
                    players.RemoveAt(i);
                    continue;
                }

                float sqr = (candidate.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    public static void SetPreferred(bool enabled)
    {
        PlayerPrefs.SetInt(DefaultPlayerPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnable()
    {
        if (SceneManager.GetActiveScene().name != AutoEnableSceneName)
            return;

        if (FindAnyObjectByType<SplitScreenMode>() != null)
            return;

        new GameObject("Split Screen Mode").AddComponent<SplitScreenMode>();
    }

    private void Awake()
    {
        bool enabled = enableOnStart;

        if (usePlayerPrefsOverride && PlayerPrefs.HasKey(playerPrefsKey))
            enabled = PlayerPrefs.GetInt(playerPrefsKey) == 1;

        if (!enabled || Active)
            return;

        Setup();
    }

    private void Setup()
    {
        Active = true;

        CarController player1 = FindAnyObjectByType<CarController>();
        if (player1 == null)
        {
            Debug.LogError("SplitScreenMode: no CarController found to assign as Player 1.", this);
            Active = false;
            return;
        }

        if (Gamepad.all.Count < 2)
            Debug.LogWarning("SplitScreenMode: only " + Gamepad.all.Count +
                             " of 2 required gamepads connected. Player 2 will be idle until a second gamepad is plugged in.", this);

        player1.playerIndex = 0;

        GameObject p2Go = ResolvePlayer2Car(player1, out bool createdP2);
        CarController player2 = p2Go != null ? p2Go.GetComponent<CarController>() : null;

        if (player2 != null)
        {
            player2.playerIndex = 1;
            if (!p2Go.activeSelf)
                p2Go.SetActive(true);
        }
        else if (p2Go != null)
        {
            Debug.LogWarning("SplitScreenMode: Player 2 object has no CarController; it will not receive input.", this);
        }

        if (createdP2)
            RemoveDuplicatedEnvironmentAudio(p2Go);

        players.Clear();
        players.Add(player1.transform);
        if (player2 != null)
            players.Add(player2.transform);

        SetupCameras(player1, player2);

        SetupKillCounters();
        SetupKillStreakPopups();

        if (showDivider)
            CreateScreenDivider();
    }

    private GameObject ResolvePlayer2Car(CarController player1, out bool createdObject)
    {
        createdObject = false;

        if (player2Car != null)
            return player2Car;

        if (player2CarPrefab != null)
        {
            Vector3 pos = player2SpawnPoint != null
                ? player2SpawnPoint.position
                : player1.transform.position + player1.transform.TransformVector(player2CloneOffset);
            Quaternion rot = player2SpawnPoint != null ? player2SpawnPoint.rotation : player1.transform.rotation;
            createdObject = true;
            return Instantiate(player2CarPrefab, pos, rot);
        }

        Vector3 clonePos = player1.transform.position + player1.transform.TransformVector(player2CloneOffset);
        GameObject clone = Instantiate(player1.gameObject, clonePos, player1.transform.rotation);
        clone.name = player1.gameObject.name + " (Player 2)";
        createdObject = true;
        return clone;
    }

    private void RemoveDuplicatedEnvironmentAudio(GameObject p2CarRoot)
    {
        if (p2CarRoot == null)
            return;

        foreach (RainFollower follower in p2CarRoot.GetComponentsInChildren<RainFollower>(true))
        {
            AudioSource src = follower.GetComponent<AudioSource>();
            if (src != null)
            {
                src.Stop();
                Destroy(src);
            }
        }
    }

    private void SetupCameras(CarController player1, CarController player2)
    {
        CarFollowCamera[] followCams = FindObjectsByType<CarFollowCamera>();
        CarFollowCamera p1Cam = null;

        foreach (CarFollowCamera cam in followCams)
        {
            if (cam.target == player1.transform)
            {
                p1Cam = cam;
                break;
            }
        }

        if (p1Cam == null && followCams.Length > 0)
            p1Cam = followCams[0];

        Camera cam1 = p1Cam != null ? p1Cam.GetComponent<Camera>() : Camera.main;

        if (cam1 != null)
            cam1.rect = new Rect(0f, 0f, 0.5f, 1f);

        Camera cam2 = player2Camera;

        if (cam2 == null && p1Cam != null)
        {
            GameObject duplicate = Instantiate(p1Cam.gameObject);
            duplicate.name = p1Cam.gameObject.name + " (Player 2)";

            CarFollowCamera follow2 = duplicate.GetComponent<CarFollowCamera>();
            if (follow2 != null)
            {
                follow2.target = player2 != null ? player2.transform : player1.transform;
                follow2.playerIndex = 1;
            }

            cam2 = duplicate.GetComponent<Camera>();
        }

        if (cam2 != null)
        {
            cam2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

            AudioListener listener = cam2.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        if (cam1 == null)
            Debug.LogError("SplitScreenMode: no camera found to split.", this);
    }

    private void SetupKillCounters()
    {
        KillCounter[] counters = FindObjectsByType<KillCounter>();
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

        if (p2 == null && p1.KillCountText != null)
        {
            Transform parent = p1.transform.parent;

            GameObject p2CounterGo = Instantiate(p1.gameObject, parent);
            p2CounterGo.name = p1.gameObject.name + " (Player 2)";
            p2CounterGo.transform.SetAsLastSibling();

            KillCounter clone = p2CounterGo.GetComponent<KillCounter>();
            TextMeshProUGUI cloneText = p2CounterGo.GetComponent<TextMeshProUGUI>();
            if (clone != null && cloneText != null)
                clone.Configure(cloneText, 1);

            p2 = clone;
        }

        AnchorCounterOnScreenHalf(p1.GetComponent<RectTransform>(), leftHalf: true);
        if (p2 != null)
            AnchorCounterOnScreenHalf(p2.GetComponent<RectTransform>(), leftHalf: false);
    }

    private static void AnchorCounterOnScreenHalf(RectTransform rt, bool leftHalf)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0.5f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(0.5f, rt.anchorMax.y);
        rt.pivot = new Vector2(leftHalf ? 1f : 0f, rt.pivot.y);

        Vector2 position = rt.anchoredPosition;
        position.x = leftHalf ? -60f : 60f;
        rt.anchoredPosition = position;
    }

    private void SetupKillStreakPopups()
    {
        KillStreakPopup[] popups = FindObjectsByType<KillStreakPopup>();
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
            GameObject p2PopupGo = Instantiate(p1.gameObject, p1.transform.parent);
            p2PopupGo.name = p1.gameObject.name + " (Player 2)";
            p2PopupGo.transform.SetAsLastSibling();

            KillStreakPopup clone = p2PopupGo.GetComponent<KillStreakPopup>();
            if (clone != null)
                clone.Configure(1);

            p2 = clone;
        }

        AnchorPopupOnScreenHalf(p1.GetComponent<RectTransform>(), leftHalf: true);
        if (p2 != null)
            AnchorPopupOnScreenHalf(p2.GetComponent<RectTransform>(), leftHalf: false);
    }

    private static void AnchorPopupOnScreenHalf(RectTransform rt, bool leftHalf)
    {
        if (rt == null)
            return;

        Vector2 anchor = new Vector2(leftHalf ? 0.25f : 0.75f, rt.anchorMin.y);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;

        Vector2 position = rt.anchoredPosition;
        position.x = 0f;
        position.y = -180f;
        rt.anchoredPosition = position;
    }

    private void CreateScreenDivider()
    {
        if (dividerObject == null)
        {
            foreach (UnityEngine.UI.Image existing in FindObjectsByType<UnityEngine.UI.Image>())
            {
                if (existing.gameObject.name == "Split Screen Divider")
                {
                    dividerObject = existing.gameObject;
                    break;
                }
            }
        }

        if (dividerObject != null)
        {
            dividerObject.GetComponent<UnityEngine.UI.Image>().color = dividerColor;
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject(
                "Split Screen Divider Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject dividerGo = new GameObject(
            "Split Screen Divider",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image));

        RectTransform rect = dividerGo.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-dividerWidth * 0.5f, 0f);
        rect.offsetMax = new Vector2(dividerWidth * 0.5f, 0f);

        UnityEngine.UI.Image image = dividerGo.GetComponent<UnityEngine.UI.Image>();
        image.color = dividerColor;
        image.raycastTarget = false;

        dividerGo.transform.SetAsLastSibling();
        dividerObject = dividerGo;
    }

    public void SetDividerVisible(bool visible)
    {
        if (visible && dividerObject == null)
            CreateScreenDivider();

        if (dividerObject != null)
            dividerObject.SetActive(visible);
    }

    public static void SetSplitScreenDividerVisible(bool visible)
    {
        if (!Active)
            return;

        SplitScreenMode split = FindAnyObjectByType<SplitScreenMode>();
        if (split != null)
            split.SetDividerVisible(visible);
    }

    private void OnDestroy()
    {
        if (!Active)
            return;

        Active = false;
        players.Clear();

        if (dividerObject != null)
            Destroy(dividerObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
        players.Clear();
    }
}