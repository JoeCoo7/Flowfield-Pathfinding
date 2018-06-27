using System;
using System.Collections.Generic;
using System.Linq;
using RSGLib;
using RSGLib.utility;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

//----------------------------------------------------------------------------------------
namespace ECSInput
{
    //----------------------------------------------------------------------------------------
    public struct MouseDoubleClick : IComponentData
    {
        public int Value;
        public int Reset;
    }
    //----------------------------------------------------------------------------------------
    public struct MousePosition : IComponentData
    {
        public float3 Value;
    }

    public struct PlayerInputTag : IComponentData { }

    //----------------------------------------------------------------------------------------
    public struct InputButtons : ISharedComponentData
    {
        public struct Command
        {
            public int Status;
            public string Key;
            public string AltKey;
            public string Mod;
            public string AltMod;
        }
        public const int NONE = 0;
        public const int DOWN = 1;
        public const int PRESSED = 2;
        public const int UP = 3;
        public Dictionary<string, Command> Values;
    }

    //---------------------------------------------------------------------------------------
    struct InputDataGroup
    {
        [ReadOnly] public SharedComponentDataArray<InputButtons> Buttons;
        [ReadOnly] public ComponentDataArray<MousePosition> MousePos;
    }


    [UpdateInGroup(typeof(InputGroup))]
    public class PlayerInputSystem : ComponentSystem
    {
        public const float DOUBLE_CLICK_TIME_WINDOW = 0.2f;

        //----------------------------------------------------------------------------------------
        struct InjectedPlayerInput
        {
            public int Length;
            [ReadOnly] private ComponentDataArray<PlayerInputTag> m_inputFilter;
            [ReadOnly] public SharedComponentDataArray<InputButtons> Buttons;
            public ComponentDataArray<MousePosition> MousePos;
            public ComponentDataArray<MouseDoubleClick> DoubleClick;
        }

        [Inject] private InjectedPlayerInput m_playerInput;
        private float m_timer;
        private int m_numberOfClicks = 0;



        //----------------------------------------------------------------------------------------
        protected override void OnCreateManager(int _capacity)
        {
            base.OnCreateManager(_capacity);
        }

        //----------------------------------------------------------------------------------------
        protected override void OnUpdate()
        {
            CreateInputSettings();
            UpdateMouseInput();
            UpdateButtonsInput();
        }

        //----------------------------------------------------------------------------------------
        private void CreateInputSettings()
        {
            if (m_playerInput.Buttons[0].Values != null)
                return;

            ProcessInputSettings();
        }

        //----------------------------------------------------------------------------------------
        public static InputButtons ProcessInputSettings()
        {
            var inputButtons = new InputButtons { Values = new Dictionary<string, InputButtons.Command>() };
            var settings = Utils.InstantiateAssetFromResource<InputSettings>("InputSettings");
            foreach (var entry in settings.Commands)
            {
                var fullKey = entry.Value.Split(';');
                if (fullKey.Length < 2)
                    throw new ApplicationException("full key needs at least 2 strings!");

                GetKeys(fullKey[0], out string mod, out string altMod);
                GetKeys(fullKey[1], out string key, out string altKey);
                if (string.IsNullOrEmpty(key))
                    throw new ApplicationException("main key has to be defined!!");

                inputButtons.Values.Add(entry.Key, new InputButtons.Command
                {
                    Status = InputButtons.NONE,
                    Key = key,
                    AltKey = altKey,
                    Mod = mod,
                    AltMod = altMod
                });
            }

            return inputButtons;
        }

        //----------------------------------------------------------------------------------------
        private static void GetKeys(string _keys, out string _main, out string _alt)
        {
            _main = _alt = "";
            if (string.IsNullOrEmpty(_keys))
                return;

            var keys = _keys.Split('|');
            if (string.IsNullOrEmpty(keys[0]))
                return;

            _main = keys[0];
            if (keys.Length > 1)
                _alt = keys[1];
        }

        //----------------------------------------------------------------------------------------
        private void UpdateButtonsInput()
        {
            var buttons = m_playerInput.Buttons[0];
            var keys = buttons.Values.Keys.ToList();
            foreach (var key in keys)
            {
                var buttonsValue = buttons.Values[key];
                GetButtonStatus(ref buttonsValue);
                buttons.Values[key] = buttonsValue;
            }
        }

        //----------------------------------------------------------------------------------------
        private void UpdateMouseInput()
        {

            var pos = m_playerInput.MousePos[0];
            pos.Value = Input.mousePosition;
            m_playerInput.MousePos[0] = pos;

            var doubleClick = m_playerInput.DoubleClick[0];
            if (doubleClick.Reset > 0)
            {
                doubleClick.Value = 0;
                doubleClick.Reset = 0;
                m_numberOfClicks = 0;
            }

            if (doubleClick.Value == 0)
                doubleClick.Value = GetDoubleClick();

            m_playerInput.DoubleClick[0] = doubleClick;
        }

        //----------------------------------------------------------------------------------------
        private void GetButtonStatus(ref InputButtons.Command _command)
        {
            bool mod = true;
            if (!string.IsNullOrEmpty(_command.Mod))
                mod = Input.GetKey(_command.Mod);
            if (!string.IsNullOrEmpty(_command.AltMod))
                mod |= Input.GetKey(_command.AltMod);

            if (!mod)
            {
                _command.Status = InputButtons.NONE;
                return;
            }

            int status = GetMainKeyStatus(_command.Key);
            if (status == InputButtons.NONE && !string.IsNullOrEmpty(_command.AltKey))
                status = GetMainKeyStatus(_command.AltKey);
            _command.Status = status;
        }

        //----------------------------------------------------------------------------------------
        public int GetMainKeyStatus(string _key)
        {
            if (Input.GetKeyUp(_key))
                return InputButtons.UP;

            if (Input.GetKeyDown(_key))
                return InputButtons.DOWN;

            if (Input.GetKey(_key))
                return InputButtons.PRESSED;

            return InputButtons.NONE;
        }

        //----------------------------------------------------------------------------------------
        public int GetDoubleClick()
        {
            m_timer += Time.deltaTime;
            if (m_timer > DOUBLE_CLICK_TIME_WINDOW)
                m_numberOfClicks = 0;

            if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON))
                return 0;

            m_numberOfClicks++;
            m_timer = 0f;

            return (m_numberOfClicks >= 2) ? 1 : 0;
        }

    }
}