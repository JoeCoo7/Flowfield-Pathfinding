using TMPro;
using UnityEngine;

namespace FlowField.UI
{
	public class TileInfo : MonoBehaviour
	{
		private TextMeshPro m_label;

		public void Awake()
		{
			m_label = GetComponent<TextMeshPro>();
		}

		public void SetText(float distance)
		{
			m_label.text = $"{distance:0.##}";
		}
	}
}