using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;


namespace SoftKitty
{
    public class TriggerNode : NodeBaseEditor
    {
        private Trigger m_data;
        public Trigger data => m_data;
        public Port ResetPort;

        public TriggerNode(Trigger _trigger)
        {
            m_data = _trigger;
        }

        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[1]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.45F, 0.4F, 0.1F, 1F));
            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/TriggerNode.uxml");
            baseSettings = uiAsset.Instantiate();
            baseSettings.style.width = 300;
            baseSettings.style.height = 35;

            
            ResetPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(NodeBase));
            ResetPort.portName = GraphEditorText.Localize(m_data.inputPort[1].Name);
            ResetPort.portColor = portColors[m_data.inputPort[1].Type];
            input.Add(ResetPort);
            ResetPort.style.display = DisplayStyle.None;
            baseSettings.Q<VisualElement>("Root").Add(ResetPort);

            baseSettings.Q<Toggle>("AutoReset").value = m_data.AutoReset;
            baseSettings.Q<Toggle>("AutoReset").RegisterValueChangedCallback(x =>
            {
                if (x.newValue && !m_data.AutoReset) DisconnectInput(1);
                m_data.AutoReset = x.newValue;
                ResetPort.style.display = m_data.AutoReset?DisplayStyle.None: DisplayStyle.Flex;
              
            });
            ResetPort.style.display = m_data.AutoReset ? DisplayStyle.None : DisplayStyle.Flex;


            extensionContainer.Add(baseSettings);
            RefreshExpandedState();
        }

    }
}
