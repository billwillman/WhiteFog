using UnityEngine;
using System.Collections.Generic;
using System.IO;


namespace SoftKitty
{
    public class CustomNode : NodeBase
    {
        public string ClassName;
        public List<int> IntValues = new List<int>();
        public List<float> FloatValues = new List<float>();
        public List<string> StringValues = new List<string>();
        public List<bool> BoolValues = new List<bool>();
        [HideInInspector]
        public CustomInterface CustomInterface = new CustomInterface();

        public virtual void CustomInit(string _typeName, string _defaultClassName)
        {
#if UNITY_EDITOR
            string _interfacePath = Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/" + _typeName + "/" + _defaultClassName + _typeName + "_interface.txt";
            if (File.Exists(_interfacePath))
            {
                string _json = File.ReadAllText(_interfacePath, System.Text.Encoding.UTF8);
                CustomInterface = JsonUtility.FromJson<CustomInterface>(_json);
            }
#endif
            int _index = inputPort.Count;
            for (int i = 0; i < CustomInterface.Elements.Length; i++)
            {
                if (CustomInterface.Elements[i].Type == NodeInterfaceElementType.Input_Float)
                {
                    inputPort.Add(new PortSetting(inputPort.Count.ToString(), "CustomVariable", new string[1] { "Variable" }, false));
                }
                else if (CustomInterface.Elements[i].Type == NodeInterfaceElementType.Input_Entity)
                {
                    inputPort.Add(new PortSetting(inputPort.Count.ToString(), "EntityIn", new string[1] { "EntityOut" }, false));
                }
            }
        }

        public CustomNode CopyCustomSetting(CustomNode _copy)
        {
            IntValues.Clear();
            foreach (var obj in _copy.IntValues) IntValues.Add(obj);
            FloatValues.Clear();
            foreach (var obj in _copy.FloatValues) FloatValues.Add(obj);
            StringValues.Clear();
            foreach (var obj in _copy.StringValues) StringValues.Add(obj);
            BoolValues.Clear();
            foreach (var obj in _copy.BoolValues) BoolValues.Add(obj);
            CustomInterface = _copy.CustomInterface.Copy();
            ClassName = _copy.ClassName;
            return _copy;
        }

        public Entity GetEntity(NodeBase _node, GraphRuntimeState _runtime)
        {
            return _node.GetEntity(_runtime);
        }

        public float GetVariableValue(NodeBase _node, GraphRuntimeState _runtime, float _default=0F)
        {
            return _node.GetVariableValue(_runtime, _default);
        }
    }
}