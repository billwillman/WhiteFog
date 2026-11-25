using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    #region Enum

    public enum NodeType
    {
        Action,
        Trigger,
        ConditionGroup,
        Math,
        Variable,
        Entity,
        Branch,
        Root,
        Handler
    }

    public enum NodeInterfaceElementType {
        Bool_Variable,
        String_Variable,
        Float_Variable,
        Int_Variable,
        Input_Float,
        Label,
        Separator,
        Enum,
        Input_Entity
    }
    public enum HandlerStateType
    {
        Default,
        Switch,
        Every_X_Time,
        Keep_1_Frame,
        Keep_5_Frame,
        Keep_1_Second
    }

   
    public enum LogicType
    {
        And,
        Or,
        Xor,
        Nor
    }

    public enum ActionType
    {
        Only_Once,
        Multiple_Time,
        For_X_Seconds
    }

    public enum BranchType
    {
        Variable,
        Entity,
        Action
    }

    public enum MathType
    {
        Add,
        Substract,
        Multiply,
        Divide,
        Power,
        Sqrt,
        OneMinus,
        DivRem,
        Floor,
        Ceil,
        Round,
        Abs,
        Min,
        Max,
        Clamp,
        Lerp
    }

    public enum VariableType
    {
        Constant,
        Attribute,
        DynamicVariable,
        Random
    }

    public enum EntityType
    {
        Player,
        Dealer,
        Target,
        Specified,
        Instance
    }
    #endregion

    #region Base Class
    [System.Serializable]
    public class NodeInterfaceElement
    {
        public string Uid;
        public string Label;
        public string ToolTips;
        public NodeInterfaceElementType Type;
        public int Index;
        public string VisibleBind = "";
        public string[] EnumOptions=new string[0];
        public NodeInterfaceElement Copy()
        {
            NodeInterfaceElement _copy = new NodeInterfaceElement();
            _copy.Uid = Uid;
            _copy.Label = Label;
            _copy.ToolTips = ToolTips;
            _copy.Type = Type;
            _copy.Index = Index;
            _copy.VisibleBind = VisibleBind;
            _copy.EnumOptions = new string[EnumOptions.Length];
            for (int i = 0; i < _copy.EnumOptions.Length; i++) _copy.EnumOptions[i] = EnumOptions[i];
            return _copy;
        }
    }


    [System.Serializable]
    public class CustomInterface
    {
        public NodeInterfaceElement[] Elements;

        public CustomInterface(){
            Elements = new NodeInterfaceElement[0];
         }
        public CustomInterface Copy()
        {
            CustomInterface _copy = new CustomInterface();
            if (Elements != null)
            {
                _copy.Elements = new NodeInterfaceElement[Elements.Length];
                for (int i = 0; i < Elements.Length; i++)
                {
                    _copy.Elements[i] = Elements[i].Copy();
                }
            }
            return _copy;
        }
    }


    [System.Serializable]
    public class PortSetting
    {
        public string Name;
        public string Type;
        public List<string> acceptedTypes = new List<string>();
        public List<string> uid = new List<string>();//Editor link
        [SerializeReference,HideInInspector]
        public List<NodeBase> connectedNodes = new List<NodeBase>();//Runtime link
        public bool allowMutipleConnection = true;
        public bool createByDefault = true;
        public PortSetting()
        {
            
        }

        public PortSetting(string _name,string _type,string [] _acceptedTypes,bool _allowMutipleConnection)
        {
            Name = _name;
            Type = _type;
            acceptedTypes.Clear();
            foreach(string obj in _acceptedTypes)acceptedTypes.Add(obj);
            uid.Clear();
            allowMutipleConnection = _allowMutipleConnection;
            createByDefault = true;
        }

        public PortSetting Copy()
        {
            PortSetting _copy = new PortSetting();
            _copy.Name = Name;
            _copy.Type = Type;
            _copy.acceptedTypes.Clear();
            foreach (string obj in acceptedTypes) _copy.acceptedTypes.Add(obj);
            _copy.uid.Clear();
            foreach (string obj in uid) _copy.uid.Add(obj);
            _copy.allowMutipleConnection = allowMutipleConnection;
            _copy.createByDefault = createByDefault;
            return _copy;
        }

    }
    #endregion

}