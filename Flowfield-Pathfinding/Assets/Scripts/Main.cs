using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
	public InitializationData m_InitData;
	void Start ()
	{
		m_InitData.Initalize();
	}
}
