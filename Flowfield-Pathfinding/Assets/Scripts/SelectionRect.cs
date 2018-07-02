using UnityEngine;

//-----------------------------------------------------------------------------
public class SelectionRect : MonoBehaviour
{
    public Color SelectionColor = Color.green;
    [Range(1f, 10f)] public float SelectionWeight = 2.0f;

    public Vector3 Start { get; set; }
    public Vector3 Stop { get; set; }
    private Texture2D m_Texture;

    //-----------------------------------------------------------------------------
    private void Awake()
    {
        m_Texture = new Texture2D(1, 1);
        m_Texture.SetPixel(0, 0, Color.white);
        m_Texture.Apply();
    }

    //-----------------------------------------------------------------------------
    private void OnGUI()
    {
        Rect rect = GetScreenRect(Start, Stop);
        SelectionColor.a = 0.25f;
        DrawScreenRect(rect, SelectionColor);
        SelectionColor.a = 1f;
        DrawScreenRectBorder(rect, SelectionWeight, SelectionColor);
    }

    //-----------------------------------------------------------------------------
    private Rect GetScreenRect(Vector3 screenPosition1, Vector3 screenPosition2)
    {
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;
        Vector3 topLeft = Vector3.Min(screenPosition1, screenPosition2);
        Vector3 bottomRight = Vector3.Max(screenPosition1, screenPosition2);
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }

    //-----------------------------------------------------------------------------
    private void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, m_Texture);
        GUI.color = Color.white;
    }

    //-----------------------------------------------------------------------------
    private void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
    }
}