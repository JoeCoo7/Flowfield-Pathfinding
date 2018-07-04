using UnityEngine;

public class FpsDisplay : MonoBehaviour
{
	double m_deltaTime = 0.0f;
 
	private void Update()
	{
		m_deltaTime += (Time.unscaledDeltaTime - m_deltaTime) * 0.1f;
	}
 
	private void OnGUI()
	{
		int w = Screen.width, h = Screen.height;
		GUIStyle style = new GUIStyle();
		Rect rect = new Rect(w-300, 0, 300, 30);
		style.alignment = TextAnchor.UpperRight;
		style.fontSize = h / 100;
		style.normal.textColor = new Color(1f, 1f, 1f);
		double msec = m_deltaTime * 1000.0f;
		double fps = 1.0f / m_deltaTime;
		string text = $"{msec:0.0} ms ({fps:0.} fps)";
		GUI.Label(rect, text, style);
	}
}


