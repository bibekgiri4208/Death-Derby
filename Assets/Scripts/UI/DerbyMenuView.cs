using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static DerbyGraphic;

/// <summary>
/// Reference-style presentation shared by the existing menu controllers. Building
/// under the controllers keeps scene-specific logic and serialized actions intact.
/// </summary>
public sealed class DerbyMenuView
{
    public GameObject Main { get; private set; }
    public GameObject Options { get; private set; }
    public Button[] Buttons { get; private set; }
    public Button[] OptionButtons { get; private set; }
    public TextMeshProUGUI Header { get; private set; }
    public TextMeshProUGUI Score { get; private set; }
    public TextMeshProUGUI Winner { get; private set; }

    private readonly RectTransform root;
    private readonly GameObject shield;
    private readonly List<GameObject> standings = new List<GameObject>();
    private RectTransform leaderboardHousing;
    public TextMeshProUGUI MusicStateLabel { get; private set; }
    public TextMeshProUGUI FpsStateLabel { get; private set; }
    private static TMP_FontAsset displayFont;

    private static Color Ink => Hex(0x0B0D10);
    private static Color Red => Hex(0xFF2437);
    private static Color Gold => Hex(0xFFD31A);
    private static Color Blue => Hex(0x00B8FF);
    private static Color Green => Hex(0x9AFF42);
    private static readonly Vector2[] Cut = P(4, 0, 96, 0, 100, 8, 100, 92, 96, 100, 4, 100, 0, 92, 0, 8);
    private static readonly Vector2[] ButtonCut = P(4, 0, 96, 0, 100, 22, 100, 78, 96, 100, 4, 100, 0, 78, 0, 22);
    private static readonly Vector2[] Banner = P(0, 0, 94, 9, 100, 36, 99, 100, 5, 85, 0, 60);

    private DerbyMenuView(Transform host)
    {
        root = Rect("Derby match UI", host, Vector2.zero, new Vector2(1280, 720));
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<CanvasGroup>();
        root.gameObject.AddComponent<DerbyMenuViewport>().Initialize(root);

        RectTransform blocker = Rect("Arena backdrop", root, Vector2.zero, Vector2.zero);
        Stretch(blocker);
        Image dim = blocker.gameObject.AddComponent<Image>();
        // Keep rendering the actual arena and HUD. A captured screen both hides
        // live setting changes and has platform-dependent render-texture UVs.
        dim.color = new Color(.025f, .03f, .04f, .12f);
        dim.raycastTarget = true;
        shield = blocker.gameObject;
        shield.SetActive(false);
    }

    public void ShowBackdrop()
    {
        shield.SetActive(true);
    }

    public void HideBackdrop()
    {
        if (shield != null) shield.SetActive(false);
    }

    public static DerbyMenuView CreatePause(PauseMenu controller, GameObject oldMain, GameObject oldOptions)
    {
        if (oldMain != null) oldMain.SetActive(false);
        if (oldOptions != null) oldOptions.SetActive(false);
        var view = new DerbyMenuView(controller.transform);
        RectTransform panel = Rect("Pause panel", view.root, Vector2.zero, new Vector2(440, 414));
        view.Main = panel.gameObject;
        PauseFrame(panel, "PAUSED");
        view.Buttons = new[]
        {
            MenuButton(panel, "RESUME", Artwork.Play, Green, Hex(0x40DA0E), new Vector2(0, 68), new Vector2(278, 62), controller.Resume),
            MenuButton(panel, "RESTART", Artwork.Restart, Hex(0xFFE858), Hex(0xFFB817), new Vector2(0, -3), new Vector2(278, 62), controller.Restart),
            MenuButton(panel, "OPTIONS", Artwork.Options, Blue, Hex(0x0088EF), new Vector2(0, -74), new Vector2(278, 62), controller.OpenOptions),
            MenuButton(panel, "MAIN MENU", Artwork.Exit, Hex(0xFF6868), Red, new Vector2(0, -145), new Vector2(278, 62), controller.ReturnToGarage)
        };
        Navigate(view.Buttons, true);

        RectTransform options = Rect("Options panel", view.root, Vector2.zero, new Vector2(440, 414));
        view.Options = options.gameObject;
        PauseFrame(options, "OPTIONS");
        Button music = MenuButton(options, "MUSIC", Artwork.Options, Blue, Hex(0x0088EF), new Vector2(0, 60), new Vector2(300, 65), controller.ToggleMusic);
        view.MusicStateLabel = music.GetComponentInChildren<TextMeshProUGUI>();
        view.MusicStateLabel.gameObject.name = "Music Text";
        Button fps = MenuButton(options, "FPS", Artwork.Options, Hex(0xFFE858), Hex(0xFFB817), new Vector2(0, -20), new Vector2(300, 65), controller.ToggleFpsMeter);
        view.FpsStateLabel = fps.GetComponentInChildren<TextMeshProUGUI>();
        view.FpsStateLabel.gameObject.name = "Fps Text";
        Button back = MenuButton(options, "BACK", Artwork.Play, Green, Hex(0x40DA0E), new Vector2(0, -112), new Vector2(300, 65), controller.CloseOptions);
        view.OptionButtons = new[] { music, fps, back };
        Navigate(view.OptionButtons, true);
        options.gameObject.SetActive(false);
        return view;
    }

    public static DerbyMenuView CreateResults(Transform host, GameObject oldBoard, Button[] oldButtons, bool multiplayer)
    {
        // Preserve the serialized UnityEvents (including scene-specific loading/audio).
        var actions = new UnityAction[2];
        for (int i = 0; i < actions.Length; i++)
        {
            if (oldButtons != null && i < oldButtons.Length && oldButtons[i] != null)
            {
                Button original = oldButtons[i];
                actions[i] = original.onClick.Invoke;
            }
        }
        if (oldBoard != null)
        {
            foreach (Transform child in oldBoard.transform) child.gameObject.SetActive(false);
            // Some boards also have an Image directly on the controller object.
            Graphic background = oldBoard.GetComponent<Graphic>();
            if (background != null) background.enabled = false;
        }

        var view = new DerbyMenuView(host);
        RectTransform panel = Rect(multiplayer ? "Winner composition" : "Death composition", view.root,
                                   new Vector2(0, multiplayer ? -20 : 0), new Vector2(560, multiplayer ? 460 : 390));
        view.Main = panel.gameObject;
        if (multiplayer) view.BuildWinner(panel);
        else view.BuildDeath(panel);

        float y = multiplayer ? -133 : -121;
        view.Buttons = new[]
        {
            MenuButton(panel, "RESTART", Artwork.Restart, Hex(0xFFE858), Hex(0xFFB817), new Vector2(-122, y), new Vector2(224, 54), actions[0]),
            MenuButton(panel, "MAIN MENU", Artwork.Home, Blue, Hex(0x0088EF), new Vector2(122, y), new Vector2(224, 54), actions[1])
        };
        Navigate(view.Buttons, false);
        return view;
    }

    private void BuildDeath(RectTransform panel)
    {
        RectTransform crest = Rect("Death badge", panel, new Vector2(0, 36), new Vector2(490, 260));
        Vector2[] plaque = P(2, 7, 93, 0, 100, 34, 97, 96, 4, 92, 0, 61);
        Shape("Badge shadow", crest, new Vector2(1, -4), new Vector2(478, 202), new Color(0, 0, 0, .5f), new Color(0, 0, 0, .5f), plaque);
        Shape("Red edge", crest, new Vector2(0, 2), new Vector2(478, 202), Red, Hex(0x950C20), plaque, 2);
        Shape("Steel rim", crest, new Vector2(0, 4), new Vector2(461, 185), Hex(0x4C535D), Hex(0x20242B), plaque, 2);
        Shape("Charcoal face", crest, new Vector2(0, 5), new Vector2(456, 180), Hex(0x272D35), Hex(0x12161D), plaque);
        Shape("Top red slash", crest, new Vector2(106, 85), new Vector2(191, 9), Red, Hex(0xAD1326), P(0, 0, 96, 0, 100, 100, 5, 100));
        Shape("Lower red slash", crest, new Vector2(-99, -77), new Vector2(180, 5), Red, Hex(0xAD1326), P(0, 0, 96, 0, 100, 100, 5, 100));
        Icon("Faceted skull", crest, Artwork.Skull, new Vector2(0, 97), new Vector2(84, 87), Color.white);
        Shape("Left impact mark", crest, new Vector2(-66, 107), new Vector2(18, 29), Red, Red, P(0, 100, 60, 100, 100, 0));
        Shape("Right impact mark", crest, new Vector2(66, 107), new Vector2(18, 29), Red, Red, P(0, 0, 40, 100, 100, 100));

        Header = Label("Death headline", crest, "YOU <color=#FF293C>DIED</color>", new Vector2(0, 14), new Vector2(423, 100), 94, Color.white, true, true);
        Header.fontStyle = FontStyles.Bold | FontStyles.Italic;
        Header.rectTransform.localRotation = Quaternion.Euler(0, 0, 3);
        Header.rectTransform.localScale = new Vector3(1.22f, 1f, 1f);
        Shape("Score divider", crest, new Vector2(0, -39), new Vector2(286, 1), Hex(0x454B53), Hex(0x454B53), null);
        Score = Label("Final score", crest, "SCORE: <color=#FFD31A>0</color>", new Vector2(0, -61), new Vector2(320, 42), 32, Color.white, true, true);
    }

    private void BuildWinner(RectTransform panel)
    {
        RectTransform crest = Rect("Victory badge", panel, new Vector2(0, 106), new Vector2(490, 220));
        Icon("Left chequered flag", crest, Artwork.Flag, new Vector2(-177, 65), new Vector2(114, 104), Color.white)
            .rectTransform.localRotation = Quaternion.Euler(0, 0, 14);
        DerbyGraphic right = Icon("Right chequered flag", crest, Artwork.Flag, new Vector2(177, 65), new Vector2(114, 104), Color.white);
        right.rectTransform.localScale = new Vector3(-1, 1, 1);
        right.rectTransform.localRotation = Quaternion.Euler(0, 0, -14);
        Shape("Badge shadow", crest, new Vector2(1, -4), new Vector2(480, 132), new Color(0, 0, 0, .5f), new Color(0, 0, 0, .5f), Cut);
        Shape("Gold rim", crest, Vector2.zero, new Vector2(480, 132), Hex(0xFFE779), Hex(0xAF6E0A), Cut, 2);
        Shape("Winner plaque", crest, Vector2.zero, new Vector2(472, 124), Hex(0x2D3034), Hex(0x12161C), Cut);
        Icon("Golden crown", crest, Artwork.Crown, new Vector2(0, 90), new Vector2(116, 88), Color.white);
        Header = Label("Winner headline", crest, "WINNER", new Vector2(0, 8), new Vector2(420, 90), 85, Color.white, true, true);
        Header.fontStyle = FontStyles.Bold | FontStyles.Italic;
        Header.enableVertexGradient = true;
        Header.colorGradient = new VertexGradient(Hex(0xFFF5AE), Hex(0xFFF5AE), Hex(0xFFB800), Hex(0xFFB800));
        Header.rectTransform.localScale = new Vector3(1.08f, 1f, 1f);
        Winner = Label("Winning player", crest, "PLAYER 1", new Vector2(0, -39), new Vector2(360, 40), 28, Color.white, true, true);
        leaderboardHousing = Shape("Leaderboard housing", panel, new Vector2(0, -32), new Vector2(454, 127), Hex(0x262B32), Hex(0x11161D), Cut, 2).rectTransform;
        TextMeshProUGUI rank = Label("Rank heading", panel, "#", new Vector2(-190, 17), new Vector2(32, 19), 13, Hex(0xA4ABB4));
        rank.characterSpacing = 1;
        TextMeshProUGUI driver = Label("Driver heading", panel, "DRIVER", new Vector2(-45, 17), new Vector2(210, 19), 13, Hex(0xA4ABB4));
        driver.alignment = TextAlignmentOptions.MidlineLeft;
        driver.characterSpacing = 1;
        Label("Kills heading", panel, "KILLS", new Vector2(190, 17), new Vector2(54, 19), 13, Hex(0xA4ABB4));
    }

    public void SetSoloResult(bool timeExpired, int score)
    {
        Header.text = timeExpired ? "TIME <color=#FFD31A>UP</color>" : "YOU <color=#FF293C>DIED</color>";
        Score.text = $"SCORE: <color=#FFD31A>{score}</color>";
    }

    public void SetWinner(int winnerIndex)
    {
        Header.text = winnerIndex < 0 ? "DRAW" : "WINNER";
        Winner.text = winnerIndex < 0 ? "TIED ROUND" : $"PLAYER {winnerIndex + 1}";
        foreach (GameObject row in standings)
        {
            row.SetActive(false);
            Object.Destroy(row);
        }
        standings.Clear();

        // Counters identify real participants. Do not invent the sample's P3/P4.
        var scores = new Dictionary<int, int>();
        foreach (KillCounter counter in Object.FindObjectsByType<KillCounter>(FindObjectsInactive.Include))
        {
            if (counter.PlayerIndex >= 0) scores[counter.PlayerIndex] = counter.KillCount;
        }
        // PvP can end by destruction even in scenes without kill-counter HUDs.
        if (scores.Count == 0) { scores[0] = 0; scores[1] = 0; }
        var players = new List<int>(scores.Keys);
        players.Sort((a, b) =>
        {
            if (a == winnerIndex && b != winnerIndex) return -1;
            if (b == winnerIndex && a != winnerIndex) return 1;
            int difference = scores[b].CompareTo(scores[a]);
            return difference != 0 ? difference : a.CompareTo(b);
        });
        int count = Mathf.Min(4, players.Count);
        float rowHeight = count > 2 ? 36 : 42;
        float gap = 5;
        float total = count * rowHeight + (count - 1) * gap;
        // Size the table to the actual player count instead of stretching two
        // rows to fill a four-player mockup. Keep the header fixed above it.
        float housingHeight = total + 38;
        leaderboardHousing.sizeDelta = new Vector2(454, housingHeight);
        leaderboardHousing.anchoredPosition = new Vector2(0, 32 - housingHeight / 2);
        float buttonsY = 32 - housingHeight - 38;
        foreach (Button button in Buttons)
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, buttonsY);
        }
        for (int i = 0; i < count; i++)
        {
            int player = players[i];
            bool winner = player == winnerIndex;
            RectTransform row = Rect($"Rank {i + 1} - Player {player + 1}", Main.transform,
                new Vector2(0, -rowHeight / 2 - i * (rowHeight + gap)), new Vector2(430, rowHeight));
            standings.Add(row.gameObject);
            Shape("Rank row", row, Vector2.zero, row.sizeDelta,
                  winner ? Hex(0xFFE63D) : Hex(0x30363E), winner ? Hex(0xFFC400) : Hex(0x1E232A), ButtonCut, 2);
            Shape("Rank badge", row, new Vector2(-188, 0), new Vector2(51, rowHeight - 3),
                  winner ? Hex(0xFFD521) : Hex(0x535C66), winner ? Hex(0xFFB500) : Hex(0x353D46),
                  P(0, 22, 14, 0, 79, 0, 100, 100, 14, 100, 0, 80), 1);
            Color text = winner ? Ink : Color.white;
            Label("Rank", row, (i + 1).ToString(), new Vector2(-190, 0), new Vector2(38, rowHeight), 24, text, false, !winner);
            TextMeshProUGUI name = Label("Player", row, $"Player {player + 1}", new Vector2(-12, 0), new Vector2(276, rowHeight), 22, text);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            Label("Kills", row, scores[player].ToString(), new Vector2(190, 0), new Vector2(43, rowHeight), 26, text, false, !winner);
        }
    }

    private static void PauseFrame(RectTransform panel, string title)
    {
        Shape("Charcoal cutout", panel, Vector2.zero, panel.sizeDelta, new Color(.12f, .14f, .15f, .95f), new Color(.025f, .03f, .035f, .92f),
              P(5, 0, 94, 1, 100, 9, 100, 93, 94, 100, 5, 97, 0, 90, 0, 7));
        Shape("Header shadow", panel, new Vector2(0, 149), new Vector2(370, 90), Ink, Ink, Banner);
        Shape("Red title banner", panel, new Vector2(0, 155), new Vector2(370, 90), Red, Hex(0xF00020), Banner, 3);
        for (int i = 0; i < 7; i++)
            Shape("Banner speed stripe", panel, new Vector2(102 + i * 9, 161 + i * .5f), new Vector2(13, 61),
                  new Color(1, .4f, .4f, .25f), new Color(1, .4f, .4f, .05f), P(0, 0, 30, 0, 100, 100, 70, 100));
        TextMeshProUGUI label = Label("Title", panel, title, new Vector2(0, 153), new Vector2(338, 73), 49, Color.white, true);
        label.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
    }

    private static Button MenuButton(Transform parent, string title, Artwork icon, Color upper, Color lower,
                                     Vector2 position, Vector2 size, UnityAction action)
    {
        RectTransform rect = Rect(title + " button", parent, position, size);
        Shape("Hard drop shadow", rect, new Vector2(0, -3), size, new Color(0, 0, 0, .45f), new Color(0, 0, 0, .45f), ButtonCut);
        DerbyGraphic face = Shape("Beveled face", rect, Vector2.zero, size, upper, lower, ButtonCut, 2, true);
        face.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f);
        colors.selectedColor = new Color(1.13f, 1.13f, 1.13f);
        colors.pressedColor = new Color(.77f, .77f, .77f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);
        Icon(title + " icon", rect, icon, new Vector2(-size.x * .326f, 1), new Vector2(size.y * .63f, size.y * .63f), Ink);
        Label("Label", rect, title, new Vector2(size.x * .075f, 0), new Vector2(size.x * .70f, size.y * .8f), size.y * .44f, Ink);
        rect.gameObject.AddComponent<ButtonHoverScale>();
        return button;
    }

    private static void Navigate(Button[] buttons, bool vertical)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            Button previous = i > 0 ? buttons[i - 1] : null;
            Button next = i + 1 < buttons.Length ? buttons[i + 1] : null;
            if (vertical) { nav.selectOnUp = previous; nav.selectOnDown = next; }
            else { nav.selectOnLeft = previous; nav.selectOnRight = next; }
            buttons[i].navigation = nav;
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static DerbyGraphic Shape(string name, Transform parent, Vector2 position, Vector2 size,
                                      Color upper, Color lower, Vector2[] points, float outline = 0, bool sheen = false)
    {
        DerbyGraphic graphic = Rect(name, parent, position, size).gameObject.AddComponent<DerbyGraphic>();
        graphic.Configure(Artwork.Panel, upper, lower, points, outline, sheen);
        return graphic;
    }

    private static DerbyGraphic Icon(string name, Transform parent, Artwork art, Vector2 position, Vector2 size, Color tint)
    {
        DerbyGraphic graphic = Rect(name, parent, position, size).gameObject.AddComponent<DerbyGraphic>();
        graphic.Configure(art, tint, tint);
        return graphic;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 position,
                                        Vector2 size, float fontSize, Color tint, bool italic = false, bool outlined = false)
    {
        TextMeshProUGUI label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        if (displayFont == null || displayFont.atlasTexture == null)
        {
            Font source = Resources.Load<Font>("DerbyUI/Anton-Regular");
            if (source != null)
            {
                displayFont = TMP_FontAsset.CreateFontAsset(source, 90, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                displayFont.name = "Anton - Derby menus";
                displayFont.hideFlags = HideFlags.DontSave;
                displayFont.material.hideFlags = HideFlags.DontSave;
                foreach (Texture2D atlas in displayFont.atlasTextures)
                    if (atlas != null) atlas.hideFlags = HideFlags.DontSave;
            }
            else displayFont = TMP_Settings.defaultFontAsset;
        }
        label.font = displayFont;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
        label.color = tint;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * .6f;
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (outlined)
        {
            label.outlineColor = Ink;
            label.outlineWidth = .13f;
            Material material = label.fontMaterial;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, .95f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .3f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, .12f);
            label.extraPadding = true;
        }
        return label;
    }

    private static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
}
