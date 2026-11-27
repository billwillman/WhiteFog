using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

namespace SoftKitty
{
    public class CustomNodeEditor : NodeBaseEditor
    {
        protected Dictionary<string, VisualElement> customItemDic = new Dictionary<string, VisualElement>();
        protected CustomNode _customData;

        protected void CheckVisibleBinding(string _uid, bool _boolValue, int _intValue)
        {
            foreach (var obj in _customData.CustomInterface.Elements)
            {
                string _bindUid = obj.VisibleBind.Replace("!", "");
                int _bindEnumId = 0;
                if (_bindUid.Contains("#"))
                {
                    string[] _args = _bindUid.Split("#");
                    _bindUid = _args[0];
                    int.TryParse(_args[1], out _bindEnumId);
                }
                if (obj.Uid != _uid && customItemDic.ContainsKey(obj.Uid) && _bindUid == _uid)
                {
                    if (obj.VisibleBind.Contains("#"))
                    {
                        if (obj.VisibleBind.Contains("!"))
                        {
                            customItemDic[obj.Uid].style.display = (_intValue == _bindEnumId) ? DisplayStyle.None : DisplayStyle.Flex;
                        }
                        else
                        {
                            customItemDic[obj.Uid].style.display = (_intValue == _bindEnumId) ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                    }
                    else
                    {
                        if (obj.VisibleBind.Contains("!"))
                        {
                            customItemDic[obj.Uid].style.display = _boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                        }
                        else
                        {
                            customItemDic[obj.Uid].style.display = _boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                    }
                }
            }
        }

        public void DeleteAllCustomPort(CustomNode _data, int _baseInputCount)
        {
            for (int i = _data.inputPort.Count-1; i>=_baseInputCount; i--)
            {
                DisconnectInput(i);
                //Debug.Log("DeleteAllCustomPort > "+i.ToString()+"/"+ input.Count.ToString());
                if (inputContainer.Contains(input[i])) inputContainer.Remove(input[i]);
                input.RemoveAt(i);
            }

        }

        protected virtual void OnIntegerSettingChanged(string uid) { }

        public virtual void LoadClassInterface(CustomNode _data,string _typeName, int _baseInputCount)
        {
            _customData = _data;
            string defaultName = _typeName == "Handler" ? "Condition" : _typeName;
            if (_data.Name == defaultName)
                title = GraphEditorText.TypedTitle(defaultName, _data.ClassName);
            for (int i = _baseInputCount; i < _data.inputPort.Count; i++)
            {
                if (i >= input.Count)
                {
                    Port _newPort = InstantiatePort(Orientation.Horizontal, Direction.Input, _data.inputPort[i].allowMutipleConnection ? Port.Capacity.Multi : Port.Capacity.Single, typeof(NodeBase));
                    _newPort.portName = GraphEditorText.Localize(_data.inputPort[i].Name);
                    _newPort.portColor = portColors[_data.inputPort[i].Type];
                    input.Add(_newPort);
                }
                input[i].style.display = _data is Action action && action.ExecutionConditionPort == i
                    ? DisplayStyle.Flex : DisplayStyle.None;
                if (!inputContainer.Contains(input[i])) inputContainer.Add(input[i]);
            }
            if (extraSettings != null && extensionContainer.Contains(extraSettings))
            {
                extensionContainer.Remove(extraSettings);
            }
            string _interfacePath = Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/"+ _typeName + "/" + _data.ClassName + _typeName + "_interface.txt";
            if (File.Exists(_interfacePath))
            {
                string _json = File.ReadAllText(_interfacePath, System.Text.Encoding.UTF8);
                _data.CustomInterface = JsonUtility.FromJson<CustomInterface>(_json);
                VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/CustomPanel.uxml");
                extraSettings = uiAsset.Instantiate();
                extraSettings.style.width = 250;
                customItemDic.Clear();
                List<string> _toggles = new List<string>();
                List<string> _dropDowns = new List<string>();
                for (int i = 0; i < _data.CustomInterface.Elements.Length; i++)
                {
                    if (GraphEditorText.IsLegacyProjectileField(_data, _data.CustomInterface.Elements[i])) continue;
                    int _index = _data.CustomInterface.Elements[i].Index;
                    string _uid = _data.CustomInterface.Elements[i].Uid;
                    string label = GraphEditorText.Localize(_data.CustomInterface.Elements[i].Label);
                    string tooltip = GraphEditorText.Localize(_data.CustomInterface.Elements[i].ToolTips);
                    if (_data.CustomInterface.Elements[i].Type == NodeInterfaceElementType.Input_Float || _data.CustomInterface.Elements[i].Type == NodeInterfaceElementType.Input_Entity)
                    {
                        customItemDic.Add(_uid, input[_index + _baseInputCount]);
                        input[_index + _baseInputCount].style.display = DisplayStyle.Flex;
                        input[_index + _baseInputCount].portName = label;
                        input[_index + _baseInputCount].tooltip = tooltip;
                        extraSettings.Add(input[_index + _baseInputCount]);
                    }
                    else
                    {
                        VisualTreeAsset newItemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/Custom" + _data.CustomInterface.Elements[i].Type.ToString() + ".uxml");
                        VisualElement newElement = newItemAsset.Instantiate();
                        newElement.style.width = 250;
                        newElement.style.height = 32;
                        customItemDic.Add(_uid, newElement);
                        switch (_data.CustomInterface.Elements[i].Type)
                        {
                            case NodeInterfaceElementType.Bool_Variable:
                                newElement.Q<Toggle>("Item").label = label;
                                if (_data.BoolValues.Count <= _index)
                                {
                                    for (int u = _data.BoolValues.Count; u <= _index; u++) _data.BoolValues.Add(false);
                                }
                                newElement.Q<Toggle>("Item").value = _data.BoolValues[_index];
                                newElement.Q<Toggle>("Item").RegisterValueChangedCallback(x =>
                                {
                                    _data.BoolValues[_index] = x.newValue;
                                    CheckVisibleBinding(_uid, x.newValue, 0);
                                });
                                _toggles.Add(_uid);
                                break;
                            case NodeInterfaceElementType.Int_Variable:
                                newElement.Q<IntegerField>("Item").label = label;
                                if (_data.IntValues.Count <= _index)
                                {
                                    for (int u = _data.IntValues.Count; u <= _index; u++) _data.IntValues.Add(0);
                                }
                                newElement.Q<IntegerField>("Item").value = _data.IntValues[_index];
                                newElement.Q<IntegerField>("Item").RegisterValueChangedCallback(x =>
                                {
                                    _data.IntValues[_index] = x.newValue;
                                    OnIntegerSettingChanged(_uid);
                                });
                                newElement.Q<Button>("AdjustUp").clicked += () =>
                                {
                                    _data.IntValues[_index]++;
                                    newElement.Q<IntegerField>("Item").value = _data.IntValues[_index];
                                };
                                newElement.Q<Button>("AdjustDown").clicked += () =>
                                {
                                    _data.IntValues[_index]--;
                                    newElement.Q<IntegerField>("Item").value = _data.IntValues[_index];
                                };
                                break;
                            case NodeInterfaceElementType.Float_Variable:
                                newElement.Q<FloatField>("Item").label = label;
                                if (_data.FloatValues.Count <= _index)
                                {
                                    for (int u = _data.FloatValues.Count; u <= _index; u++) _data.FloatValues.Add(0F);
                                }
                                newElement.Q<FloatField>("Item").value = _data.FloatValues[_index];
                                newElement.Q<FloatField>("Item").RegisterValueChangedCallback(x =>
                                {
                                    _data.FloatValues[_index] = x.newValue;
                                    Debug.Log(_index);
                                });
                                break;
                            case NodeInterfaceElementType.String_Variable:
                                newElement.Q<TextField>("Item").label = label;
                                if (_data.StringValues.Count <= _index)
                                {
                                    for (int u = _data.StringValues.Count; u <= _index; u++) _data.StringValues.Add("");
                                }
                                newElement.Q<TextField>("Item").value = _data.StringValues[_index];
                                newElement.Q<TextField>("Item").RegisterValueChangedCallback(x =>
                                {
                                    _data.StringValues[_index] = x.newValue;
                                    Debug.Log(_index);
                                });
                                break;
                            case NodeInterfaceElementType.Label:
                                newElement.Q<Label>("Item").text = label;
                                break;
                            case NodeInterfaceElementType.Enum:
                                var dropdown = newElement.Q<DropdownField>("Item");
                                bool isHitMode = GraphEditorText.IsHitMode(_data, _data.CustomInterface.Elements[i]);
                                if (isHitMode) tooltip = GraphEditorText.HitModeHelp;
                                dropdown.label = isHitMode ? GraphEditorText.HitModeLabel : label;
                                GraphEditorText.FormatDropdown(dropdown);
                                if (_data.IntValues.Count <= _index)
                                {
                                    for (int u = _data.IntValues.Count; u <= _index; u++) _data.IntValues.Add(0);
                                }
                                var options = isHitMode ? GraphEditorText.HitModeOptions : _data.CustomInterface.Elements[i].EnumOptions;
                                dropdown.choices = new List<string>(options);
                                if (!isHitMode) _data.IntValues[_index] = Mathf.Clamp(_data.IntValues[_index], 0, options.Length - 1);
                                dropdown.index = isHitMode ? GraphEditorText.HitModeIndex(_data.IntValues[_index]) : _data.IntValues[_index];
                                dropdown.RegisterValueChangedCallback(x =>
                                {
                                    if (x.target is not DropdownField field) return;
                                    _data.IntValues[_index] = isHitMode ? GraphEditorText.HitModeValue(field.index) : field.index;
                                    OnIntegerSettingChanged(_uid);
                                    CheckVisibleBinding(_uid, true, field.index);
                                });
                                _dropDowns.Add(_uid);
                                break;
                        }
                        newElement.Q<VisualElement>("Item").tooltip = tooltip;
                        extraSettings.Add(newElement);
                    }
                }

                foreach (var obj in _toggles)
                {
                    if (customItemDic.ContainsKey(obj))
                    {
                        CheckVisibleBinding(obj, customItemDic[obj].Q<Toggle>("Item").value, 0);
                    }
                }
                foreach (var obj in _dropDowns)
                {
                    if (customItemDic.ContainsKey(obj))
                    {
                        CheckVisibleBinding(obj, true, customItemDic[obj].Q<DropdownField>("Item").index);
                    }
                }
                extensionContainer.Add(extraSettings);
                extensionContainer.style.backgroundColor = new StyleColor(new Color(0.2F, 0.2F, 0.2F, 1F));
            }
            else
            {
                Debug.LogError("Interface file missing: " + _interfacePath);
            }
        }
    }
}
