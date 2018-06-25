using System;
using UnityEngine;

//----------------------------------------------------------------------------------------
namespace RSGLib
{
    //----------------------------------------------------------------------------------------
    public class DoubleClickDetector : MonoBehaviour
    {
        public float DoubleClickTimeWindow = 0.2f;
        private int m_numberOfClicks = 0;
        private float m_timer;
        private Action m_action;
        private bool m_doubleClicked;
        public void SetCallback(Action _action) { m_action = _action; }

        //----------------------------------------------------------------------------------------
        public bool DoubleClicked
        {
            get
            {
                if (!m_doubleClicked)
                    return false;

                m_numberOfClicks = 0;
                m_doubleClicked = false;
                return true;
            }
            private set
            {
                m_doubleClicked = value;
            }
        }

        //----------------------------------------------------------------------------------------
        public void Update()
        {
            m_timer += Time.deltaTime;
            if (m_timer > DoubleClickTimeWindow)
                m_numberOfClicks = 0;

            if (!Input.GetMouseButtonDown(0))
                return;

            m_numberOfClicks++;
            m_timer = 0f;

            if (m_numberOfClicks >= 2)
            {
                if (m_action != null)
                    m_action.Invoke();
                else
                    DoubleClicked = true;
            }
        }
    }
}