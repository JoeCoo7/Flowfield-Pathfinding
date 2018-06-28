using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InitializationData))]
public class InitializationDataEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (GUILayout.Button("Refresh Terrain"))
			Main.Instance.m_InitData.BuildWorld();
	}
}
