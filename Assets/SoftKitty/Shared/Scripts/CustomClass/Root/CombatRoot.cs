using UnityEngine;
namespace SoftKitty{
    public class CombatRoot : Root{
 
        public CombatRoot(){}
 
        public CombatRoot(Root _baseClass) : base(_baseClass){}
 
        public CombatRoot InitRoot(Root _baseClass){
            CombatRoot _newObj =new CombatRoot(_baseClass);
            return _newObj;
        }
 
     
        public override void StructureBuild()//New Root Class
        {

        }

        public override void StructureReset()//New Root Class with everything reset to default
        {

        }

      

    }
}
