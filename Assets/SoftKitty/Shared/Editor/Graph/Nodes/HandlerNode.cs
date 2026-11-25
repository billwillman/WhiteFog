using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace SoftKitty
{
    public class HandlerNode : CustomNodeEditor
    {
        private Handler m_data;
        public Handler data => m_data;
        public HandlerNode(Handler _handler)
        {
            m_data = _handler;
        }
        private Label StateHintLabel;
        private IntegerField TicksField;
        void TickUp()
        {
            if (TicksField.value < 30)
                TicksField.value++;
        }

        void TickDown()
        {
            if (TicksField.value > 2)
                TicksField.value--;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[1]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.2F, 0.4F, 0.2F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/HandlerNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 50;
            StateHintLabel = baseSettings.Q<Label>("StateHint");
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(mainDataObject.HandlerClassNames);
            baseSettings.Q<DropdownField>("Type").index = mainDataObject.HandlerClassNames.IndexOf(m_data.ClassName);
            baseSettings.Q<DropdownField>("Type").value = m_data.ClassName;
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.ClassName = x.newValue;
                DeleteAllCustomPort(m_data, 1);
                m_data.ResetInputPortSetting();
                LoadClassInterface(m_data, "Handler", 0);
            });
            baseSettings.Q<DropdownField>("StateType").choices.Clear();
            baseSettings.Q<DropdownField>("StateType").choices.AddRange(Enum.GetNames(typeof(HandlerStateType)));
            baseSettings.Q<DropdownField>("StateType").index = (int)m_data.StateType;
            baseSettings.Q<DropdownField>("StateType").value = m_data.StateType.ToString();
            baseSettings.Q<DropdownField>("StateType").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.StateType = (HandlerStateType)((DropdownField)x.target).index;
                FormatStateType();
                RefreshExpandedState();
            });
            baseSettings.Q<Button>("TickUp").clicked += TickUp;
            baseSettings.Q<Button>("TickDown").clicked += TickDown;

            TicksField = baseSettings.Q<IntegerField>("Ticks");
            TicksField.value = m_data.xTimeCount;
            TicksField.RegisterValueChangedCallback(x =>
            {
                if (x.newValue < 2 || x.newValue > 30)
                {
                    TicksField.value = Mathf.Clamp(x.newValue, 2, 30);
                    m_data.xTimeCount = TicksField.value;
                }
                else
                {
                    m_data.xTimeCount = x.newValue;
                }
            });
            extensionContainer.Add(baseSettings);
            FormatStateType();

            m_data.ResetInputPortSetting();
            LoadClassInterface(m_data, "Handler",0);
            RefreshExpandedState();
        }

        private void FormatStateType()
        {
            switch (m_data.StateType)
            {
                case HandlerStateType.Default:
                    StateHintLabel.text = "条件满足时，向触发器输出一次真。";
                    break;
                case HandlerStateType.Every_X_Time:
                    StateHintLabel.text = "每累计满足指定次数，向触发器输出一次真。";
                    break;
                case HandlerStateType.Keep_1_Frame:
                    StateHintLabel.text = "条件满足时，向触发器持续输出真 1 帧。";
                    break;
                case HandlerStateType.Keep_5_Frame:
                    StateHintLabel.text = "条件满足时，向触发器持续输出真 5 帧。";
                    break;
                case HandlerStateType.Keep_1_Second:
                    StateHintLabel.text = "条件满足时，向触发器持续输出真 1 秒。";
                    break;
                case HandlerStateType.Switch:
                    StateHintLabel.text = "条件满足时输出真，保持到触发器重置。";
                    break;
            }
            TicksField.style.display = m_data.StateType == HandlerStateType.Every_X_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.style.height = m_data.StateType== HandlerStateType.Every_X_Time?180:150;
            
        }

    }
}
