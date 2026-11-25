using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

namespace SoftKitty
{
    public class ActionNode : CustomNodeEditor
    {
        private Action m_data;
        public Action data => m_data;
        public ActionNode(Action _action)
        {
            m_data = _action;
        }
       

        void CountUp()
        {
            baseSettings.Q<IntegerField>("Count").value++;
        }

        void CountDown()
        {
            baseSettings.Q<IntegerField>("Count").value--;
        }



        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[1]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.4F, 0.2F, 0.2F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/ActionNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.style.height = 180;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(mainDataObject.ActionClassNames);
            baseSettings.Q<DropdownField>("Type").index = mainDataObject.ActionClassNames.IndexOf(m_data.ClassName);
            baseSettings.Q<DropdownField>("Type").value = m_data.ClassName;
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.ClassName  = x.newValue;
                DeleteAllCustomPort(m_data,1);
                m_data.ResetInputPortSetting();
                LoadClassInterface(m_data,"Action",1);
            });

            baseSettings.Q<DropdownField>("ExcuteType").choices.Clear();
            baseSettings.Q<DropdownField>("ExcuteType").choices.AddRange(Enum.GetNames(typeof(ActionType)));
            baseSettings.Q<DropdownField>("ExcuteType").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("ExcuteType").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("ExcuteType").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type =(ActionType)((DropdownField)x.target).index;
                FormatType();
            });

            baseSettings.Q<Button>("AdjustUp").clicked += CountUp;
            baseSettings.Q<Button>("AdjustDown").clicked += CountDown;

            baseSettings.Q<FloatField>("WaitTime").value = m_data.WaitTime;
            baseSettings.Q<FloatField>("WaitTime").RegisterValueChangedCallback(x =>
            {
                m_data.WaitTime = x.newValue;
            });

            baseSettings.Q<IntegerField>("Count").value = m_data.Count;
            baseSettings.Q<IntegerField>("Count").RegisterValueChangedCallback(x =>
            {
                m_data.Count =x.newValue;
            });

            baseSettings.Q<FloatField>("KeepTime").value = m_data.KeepTime;
            baseSettings.Q<FloatField>("KeepTime").RegisterValueChangedCallback(x =>
            {
                m_data.KeepTime = x.newValue;
            });

            baseSettings.Q<FloatField>("IntervalTime").value = m_data.Interval;
            baseSettings.Q<FloatField>("IntervalTime").RegisterValueChangedCallback(x =>
            {
                m_data.Interval = x.newValue;
            });
            FormatType();
            extensionContainer.Add(baseSettings);
            m_data.ResetInputPortSetting();
            LoadClassInterface(m_data,"Action",1);
            RefreshExpandedState();
        }


        private void FormatType()
        {
            baseSettings.Q<Button>("AdjustUp").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<Button>("AdjustDown").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<IntegerField>("Count").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<FloatField>("KeepTime").style.display = m_data.Type == ActionType.For_X_Seconds ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<FloatField>("IntervalTime").style.display = m_data.Type != ActionType.Only_Once ? DisplayStyle.Flex : DisplayStyle.None;
        }


    }
}
