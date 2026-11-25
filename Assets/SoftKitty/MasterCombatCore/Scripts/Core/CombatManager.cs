using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore
{
    public class CombatManager : MonoBehaviour
    {
        private static Dictionary<string, OverTimeEffectInstance> OverTimeEffectDic = new Dictionary<string, OverTimeEffectInstance>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            Entity.ClearOverTimeTickCallback();
            Entity.RegisterOverTimeTickCallback(ExecuteOverTimeEffect);
        }

   

        public static void ExecuteOverTimeEffect(string _uid, int _layer, Entity _dealer, Entity _target)
        {
            if (OverTimeEffectDic.ContainsKey(_uid))
            {
                OverTimeEffectDic[_uid].Execute(_uid,_layer, _dealer, _target);
            }
            else
            {
                OverTimeEffect _setting = GameManager.GetOverTimeEffect(_uid);
                if (_setting != null)
                {
                    OverTimeEffectDic.Add(_uid, new OverTimeEffectInstance(_setting));
                    OverTimeEffectDic[_uid].Execute(_uid, _layer, _dealer, _target);
                }
            }
        }



    }
}
