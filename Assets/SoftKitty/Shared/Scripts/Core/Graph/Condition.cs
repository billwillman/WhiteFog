using UnityEngine;
using System.Collections.Generic;
 

namespace SoftKitty
{
    [System.Serializable]
    public class Condition : CustomNode
    {
        public int ClassType = 0;//0 Group  1 Condition
       

        public Condition()
        {

        }
        public override bool isActive(GraphRuntimeState _runtime)
        {
            return false;
        }

        public override void Update(GraphRuntimeState _runtime)
        {

        }

        public override void Init()
        {

        }

        public override void Reset()
        {

        }

        public override void OnDestroy()
        {

        }
    }

}