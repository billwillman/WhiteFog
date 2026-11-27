using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{

    [System.Serializable]
    public class EntityClass : NodeBase
    {
        public EntityType Type = EntityType.Player;
        public string EntityUid = "";


        public EntityClass()
        {

        }
        public EntityClass(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Entity (Player)";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            Position = Vector2.zero;
            Type = EntityType.Player;
            EntityUid = "";

            inputPort.Clear();
            outputPort.Clear();
            outputPort.Add(new PortSetting("Entity", "EntityOut", new string[2] { "EntityIn", "BranchInEntity" }, true));
        }

        public EntityClass Copy(bool _runtime = false)
        {
            EntityClass _copy = new EntityClass(mRoot);
            _copy.Name = Name;
            _copy.Uid = Uid;
            _copy.Position = Position;
            _copy.minimized = minimized;
            _copy.inputPort.Clear();
            for (int i = 0; i < inputPort.Count; i++)
            {
                _copy.inputPort.Add(inputPort[i].Copy());
            }
            _copy.outputPort.Clear();
            for (int i = 0; i < outputPort.Count; i++)
            {
                _copy.outputPort.Add(outputPort[i].Copy());
            }
            _copy.Type = Type;
            _copy.EntityUid = EntityUid;
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Entity;
            return _copy;
        }

        public override Entity GetEntity(GraphRuntimeState _runtime)
        {
            if (Type == EntityType.NextCastTarget) return _runtime.projectileHit?.NextTarget;
            if (Type == EntityType.Player)
            {
                return GameManager.GetPlayer();
            }
            else if (Type == EntityType.Specified)
            {
                return GameManager.GetEntity(EntityUid);
            }
            else if (Type == EntityType.Target)
            {
                return _runtime.RootState.GetInvolvedEntity("Target");
            }
            else if (Type == EntityType.Dealer)
            {
                return _runtime.RootState.GetInvolvedEntity("Dealer");
            }
            else if (Type == EntityType.Instance)
            {
                return _runtime.RootState.GetInvolvedEntity("Instance");
            }
            return null;
        }
    }
 

 
}
