using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector-art renderer for the match menus. No stretched bitmap borders.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DerbyGraphic : MaskableGraphic
{
    public enum Artwork { Panel, Play, Restart, Home, Options, Exit, Skull, Crown, Flag }

    private Artwork artwork;
    private Vector2[] silhouette;
    private Color top = Color.white;
    private Color bottom = Color.white;
    private float border;
    private bool sheen;

    public void Configure(Artwork art, Color upper, Color lower, Vector2[] points = null,
                          float outline = 0f, bool highlight = false)
    {
        artwork = art;
        top = upper;
        bottom = lower;
        silhouette = points;
        border = outline;
        sheen = highlight;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (artwork)
        {
            case Artwork.Panel:
                Vector2[] points = silhouette ?? P(0, 0, 100, 0, 100, 100, 0, 100);
                if (border > 0f)
                    Poly(mesh, points, Ink, Ink);
                float insetX = border * 100f / Mathf.Max(1f, rectTransform.rect.width);
                float insetY = border * 100f / Mathf.Max(1f, rectTransform.rect.height);
                Vector2[] inset = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                    inset[i] = new Vector2(Mathf.Lerp(insetX, 100f - insetX, points[i].x / 100f),
                                           Mathf.Lerp(insetY, 100f - insetY, points[i].y / 100f));
                Poly(mesh, inset, top, bottom);
                if (sheen)
                {
                    Poly(mesh, P(5, 89, 95, 89, 93, 95, 6, 95), new Color(1, 1, 1, .25f));
                    Poly(mesh, P(78, 5, 98, 69, 96, 89, 70, 5), new Color(1, 1, 1, .12f));
                    Poly(mesh, P(5, 5, 94, 5, 98, 16, 2, 16), new Color(0, 0, 0, .08f));
                }
                break;
            case Artwork.Play:
                Poly(mesh, P(26, 9, 26, 91, 89, 50), top);
                break;
            case Artwork.Restart:
                Arc(mesh, 48, 49, 33, 18, 49, 343, top);
                Poly(mesh, P(55, 90, 92, 83, 78, 49), top);
                break;
            case Artwork.Home:
                Poly(mesh, P(3, 48, 50, 94, 96, 48, 82, 48, 82, 9, 58, 9,
                             58, 37, 40, 37, 40, 9, 18, 9, 18, 48), top);
                Poly(mesh, P(68, 69, 68, 94, 81, 94, 81, 57), top);
                break;
            case Artwork.Exit:
                Poly(mesh, P(12, 7, 12, 93, 83, 93, 83, 7), top);
                Poly(mesh, P(25, 20, 25, 80, 69, 80, 69, 20), new Color(1, .31f, .33f));
                Poly(mesh, P(28, 42, 52, 42, 52, 26, 80, 51, 52, 77, 52, 60, 28, 60), top);
                break;
            case Artwork.Options:
                for (int i = 0; i < 8; i++)
                    RotatedBox(mesh, 50, 50, 15, 84, i * 45, top);
                Disc(mesh, 50, 50, 35, top);
                Disc(mesh, 50, 50, 17, new Color(0, .66f, .96f));
                break;
            case Artwork.Skull: Skull(mesh); break;
            case Artwork.Crown: Crown(mesh); break;
            case Artwork.Flag: Flag(mesh); break;
        }
    }

    private static Color Ink => new Color32(8, 10, 13, 255);
    private static Color Gold => new Color32(255, 190, 0, 255);

    private void Skull(VertexHelper mesh)
    {
        Vector2[] head = P(50, 99, 77, 91, 86, 75, 87, 47, 76, 30, 70, 28,
                          69, 8, 51, 3, 32, 8, 31, 28, 22, 31, 12, 47, 14, 76, 23, 91);
        Poly(mesh, head, Ink);
        Poly(mesh, P(50, 95, 74, 88, 81, 73, 82, 49, 72, 34, 65, 32,
                     65, 12, 50, 8, 36, 12, 36, 32, 26, 34, 17, 49, 19, 74, 26, 88),
             new Color32(255, 255, 245, 255), new Color32(157, 161, 166, 255));
        Poly(mesh, P(50, 95, 50, 57, 42, 46, 48, 35, 50, 8, 36, 12, 36, 32,
                     26, 34, 17, 49, 19, 74, 26, 88), new Color(0, 0, 0, .16f));
        Poly(mesh, P(19, 74, 26, 88, 29, 59, 22, 50), new Color(1, 1, 1, .5f));
        Poly(mesh, P(26, 62, 37, 66, 45, 53, 37, 39, 25, 44), Ink);
        Poly(mesh, P(56, 53, 64, 66, 77, 62, 77, 44, 65, 39), Ink);
        Cross(mesh, 35, 53);
        Cross(mesh, 67, 53);
        Poly(mesh, P(50, 36, 44, 26, 49, 24, 55, 27), Ink);
        for (int i = 0; i < 3; i++)
            Poly(mesh, P(41 + i * 8, 9, 44 + i * 8, 9, 44 + i * 8, 22, 41 + i * 8, 22), Ink);
    }

    private void Cross(VertexHelper mesh, float x, float y)
    {
        Color red = new Color32(255, 32, 47, 255);
        RotatedBox(mesh, x, y, 5, 19, 38, red);
        RotatedBox(mesh, x, y, 5, 19, -38, red);
    }

    private void Crown(VertexHelper mesh)
    {
        Poly(mesh, P(8, 68, 24, 59, 20, 88, 40, 67, 50, 100, 61, 67, 81, 88,
                     76, 58, 93, 68, 84, 20, 70, 10, 30, 10, 15, 20), Ink);
        Poly(mesh, P(12, 63, 29, 51, 25, 79, 42, 60, 50, 92, 59, 60, 76, 79,
                     71, 50, 88, 63, 80, 23, 68, 15, 32, 15, 19, 23),
             new Color32(255, 239, 67, 255), Gold);
        Poly(mesh, P(26, 79, 32, 23, 47, 21, 50, 91, 42, 60), new Color(1, 1, .7f, .65f));
        Poly(mesh, P(76, 79, 67, 22, 79, 23, 88, 63, 71, 50), new Color32(255, 147, 0, 255));
        Poly(mesh, P(16, 28, 34, 34, 67, 34, 84, 28, 81, 16, 67, 8, 34, 8, 19, 16), Ink);
        Poly(mesh, P(20, 26, 35, 30, 66, 30, 80, 26, 77, 18, 65, 12, 35, 12, 23, 18), Gold);
        Poly(mesh, P(24, 25, 36, 28, 65, 28, 76, 25, 64, 22, 36, 22), new Color32(255, 239, 86, 255));
        Disc(mesh, 50, 23, 4.4f, Ink, 6);
        Disc(mesh, 50, 23.5f, 2.8f, new Color32(255, 74, 31, 255), 6);
        foreach (Vector2 point in P(9, 68, 21, 88, 50, 97, 81, 88, 92, 68))
        {
            Disc(mesh, point.x, point.y, 5.4f, Ink, 6);
            Disc(mesh, point.x, point.y + .7f, 3.8f, new Color32(255, 213, 17, 255), 6);
        }
    }

    private void Flag(VertexHelper mesh)
    {
        Poly(mesh, P(8, 2, 14, 0, 93, 91, 89, 96), Ink);
        Poly(mesh, P(6, 30, 26, 34, 46, 28, 66, 27, 86, 34,
                     95, 91, 75, 84, 55, 85, 35, 91, 15, 87), Ink);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                Color tint = (row + col) % 2 == 0 ? new Color32(249, 248, 236, 255) : new Color32(29, 34, 40, 255);
                Poly(mesh, new[] { FlagPoint(col, row), FlagPoint(col + 1, row),
                                  FlagPoint(col + 1, row + 1), FlagPoint(col, row + 1) },
                     tint, tint * new Color(.78f, .81f, .86f, 1));
            }
        }
    }

    private static Vector2 FlagPoint(int column, int row)
    {
        float wave = column == 1 || column == 4 ? 4 : column == 2 ? -2 : column == 3 ? -3 : 0;
        return new Vector2(9 + column * 19 + row * 3, 33 + row * 17 + wave);
    }

    private void Arc(VertexHelper mesh, float x, float y, float outer, float inner,
                     float from, float to, Color tint)
    {
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(from, to, (i + 1f) / segments) * Mathf.Deg2Rad;
            Poly(mesh, new[] { new Vector2(x + Mathf.Cos(a) * outer, y + Mathf.Sin(a) * outer),
                              new Vector2(x + Mathf.Cos(b) * outer, y + Mathf.Sin(b) * outer),
                              new Vector2(x + Mathf.Cos(b) * inner, y + Mathf.Sin(b) * inner),
                              new Vector2(x + Mathf.Cos(a) * inner, y + Mathf.Sin(a) * inner) }, tint);
        }
    }

    private void Disc(VertexHelper mesh, float x, float y, float radius, Color tint, int segments = 28)
    {
        Vector2[] points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments * 360f + 30f) * Mathf.Deg2Rad;
            points[i] = new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius);
        }
        Poly(mesh, points, tint);
    }

    private void RotatedBox(VertexHelper mesh, float x, float y, float width, float height, float angle, Color tint)
    {
        Vector2[] points = P(-width / 2, -height / 2, width / 2, -height / 2,
                            width / 2, height / 2, -width / 2, height / 2);
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < points.Length; i++)
            points[i] = (Vector2)(rotation * points[i]) + new Vector2(x, y);
        Poly(mesh, points, tint);
    }

    public static Vector2[] P(params float[] xy)
    {
        Vector2[] points = new Vector2[xy.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return points;
    }

    private void Poly(VertexHelper mesh, Vector2[] points, Color tint) => Poly(mesh, points, tint, tint);

    // Ear clipping also handles the concave crown, skull and lightning silhouettes.
    private void Poly(VertexHelper mesh, Vector2[] points, Color upper, Color lower)
    {
        int start = mesh.currentVertCount;
        Rect rect = rectTransform.rect;
        float area = 0;
        var remaining = new List<int>(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 point = points[i];
            Color tint = Color.Lerp(lower, upper, point.y / 100f) * color;
            mesh.AddVert(new Vector3(rect.xMin + point.x / 100f * rect.width,
                                    rect.yMin + point.y / 100f * rect.height), tint, Vector2.zero);
            Vector2 next = points[(i + 1) % points.Length];
            area += point.x * next.y - next.x * point.y;
            remaining.Add(i);
        }
        if (area < 0) remaining.Reverse();
        int guard = points.Length * points.Length;
        while (remaining.Count > 2 && guard-- > 0)
        {
            bool found = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                int b = remaining[i];
                int c = remaining[(i + 1) % remaining.Count];
                if (Cross(points[b] - points[a], points[c] - points[b]) <= .0001f) continue;
                bool contains = false;
                foreach (int p in remaining)
                {
                    if (p == a || p == b || p == c) continue;
                    if (Cross(points[b] - points[a], points[p] - points[a]) >= 0 &&
                        Cross(points[c] - points[b], points[p] - points[b]) >= 0 &&
                        Cross(points[a] - points[c], points[p] - points[c]) >= 0)
                    { contains = true; break; }
                }
                if (contains) continue;
                mesh.AddTriangle(start + a, start + b, start + c);
                remaining.RemoveAt(i);
                found = true;
                break;
            }
            if (!found) break;
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
