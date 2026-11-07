using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks runtime menu construction without loading a gameplay scene.</summary>
public static class RuntimeMenuValidation
{
    [MenuItem("Death Derby/Tests/Validate Runtime Match Menus")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the menu checks outside Play mode.");

        ValidatePrefab("Assets/Prefab/Utilities/Pause Menu.prefab", typeof(PauseMenu));
        ValidatePrefab("Assets/Prefab/Utilities/Result Board.prefab", typeof(ResultBoard));

        Scene previous = SceneManager.GetActiveScene();
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(test);
            var canvas = new GameObject("Runtime menu test", typeof(RectTransform), typeof(Canvas));
            var pauseHost = new GameObject("Pause controller", typeof(RectTransform));
            pauseHost.transform.SetParent(canvas.transform, false);
            var pause = pauseHost.AddComponent<PauseMenu>();
            var pauseView = DerbyMenuView.CreatePause(pause);
            Require(pauseView.Buttons.Length == 4 && pauseView.OptionButtons.Length == 3, "Pause/options buttons missing.");
            Require(pauseView.MusicStateLabel != null && pauseView.FpsStateLabel != null, "Runtime option labels missing.");
            Require(!pauseView.Options.activeSelf, "Options must start hidden.");
            ValidateNavigation(pauseView.Buttons, true);
            ValidateNavigation(pauseView.OptionButtons, true);

            foreach (bool multiplayer in new[] { false, true })
            {
                var resultHost = new GameObject("Result controller", typeof(RectTransform));
                resultHost.transform.SetParent(canvas.transform, false);
                var result = DerbyMenuView.CreateResults(resultHost.transform, multiplayer);
                Require(result.Buttons.Length == 2, "Result actions missing.");
                Require(result.Buttons.All(button => button.onClick.GetPersistentEventCount() == 0), "Results still require serialized button actions.");
                ValidateNavigation(result.Buttons, false);
                if (multiplayer)
                {
                    result.SetWinner(1);
                    Require(result.Header.text == "WINNER" && result.Winner.text == "PLAYER 2", "Winner display failed.");
                    result.SetWinner(-1);
                    Require(result.Header.text == "DRAW" && result.Winner.text == "TIED ROUND", "Draw display failed.");
                }
                else
                {
                    result.SetSoloResult(false, 17);
                    Require(result.Header.text.Contains("DIED") && result.Score.text.Contains("17"), "Death/score display failed.");
                    result.SetSoloResult(true, 23);
                    Require(result.Header.text.Contains("TIME") && result.Score.text.Contains("23"), "Time-up display failed.");
                }
            }
            Debug.Log("RUNTIME_MENUS_PASS: controller-only prefabs, runtime pause/options/results, scores, winners, draws, and explicit navigation.");
        }
        finally
        {
            EditorSceneManager.CloseScene(test, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static void ValidatePrefab(string path, Type controllerType)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Require(root.GetComponent(controllerType) != null, "Missing controller: " + path);
            Require(root.transform.childCount == 0, "Legacy layout remains: " + path);
            Require(root.GetComponentsInChildren<Graphic>(true).Length == 0, "Legacy graphics remain: " + path);
            Require(root.GetComponentsInChildren<Selectable>(true).Length == 0, "Legacy buttons remain: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ValidateNavigation(Button[] buttons, bool vertical)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation navigation = buttons[i].navigation;
            Require(navigation.mode == Navigation.Mode.Explicit, "Menu navigation escaped to the HUD.");
            Require((vertical ? navigation.selectOnUp : navigation.selectOnLeft) == (i > 0 ? buttons[i - 1] : null), "Previous button link failed.");
            Require((vertical ? navigation.selectOnDown : navigation.selectOnRight) == (i + 1 < buttons.Length ? buttons[i + 1] : null), "Next button link failed.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
