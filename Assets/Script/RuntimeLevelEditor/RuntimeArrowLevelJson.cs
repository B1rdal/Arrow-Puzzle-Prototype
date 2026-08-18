/*
Summary:
RuntimeArrowLevelJson contains the serializable JSON shape used by the standalone
level editor. The runtime editor saves this format, and Unity editor import tools can
convert it back into PathArrowLevelData assets. activeCells is optional; when it is
empty or missing, the level uses the full rectangular board unless hasCustomShape is
true. That flag lets the editor represent an empty custom board while painting shapes.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuntimeArrowLevelDocument
{
    public int width = 15;
    public int height = 15;
    public bool hasCustomShape;
    public List<IntPoint> activeCells = new List<IntPoint>();
    public List<RuntimeArrowJson> arrows = new List<RuntimeArrowJson>();

    public bool UsesCustomShape => hasCustomShape || (activeCells != null && activeCells.Count > 0);
}

[Serializable]
public class RuntimeArrowJson
{
    public string id = "Arrow";
    public SerializableColor color = SerializableColor.FromColor(Color.black);
    public List<IntPoint> points = new List<IntPoint>();
}

[Serializable]
public struct IntPoint
{
    public int x;
    public int y;

    public IntPoint(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }

    public static IntPoint FromVector2Int(Vector2Int value)
    {
        return new IntPoint(value.x, value.y);
    }
}

[Serializable]
public struct SerializableColor
{
    public float r;
    public float g;
    public float b;
    public float a;

    public Color ToColor()
    {
        return new Color(r, g, b, a);
    }

    public static SerializableColor FromColor(Color color)
    {
        return new SerializableColor
        {
            r = color.r,
            g = color.g,
            b = color.b,
            a = color.a
        };
    }
}
