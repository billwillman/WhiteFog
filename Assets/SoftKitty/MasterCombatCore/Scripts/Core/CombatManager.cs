using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterCombatCore
{
    public class CombatManager : MonoBehaviour
    {
        private static Dictionary<string, OverTimeEffectInstance> OverTimeEffectDic = new Dictionary<string, OverTimeEffectInstance>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRandomSession() => CombatRandom.BeginBattle(0);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            Entity.ClearOverTimeTickCallback();
            Entity.RegisterOverTimeTickCallback(ExecuteOverTimeEffect);
        }

   

        public static void ExecuteOverTimeEffect(string _uid, int _layer, Entity _dealer, Entity _target)
            => ExecuteOverTimeEffect(_uid, _layer, _dealer, _target, null);

        public static void ExecuteOverTimeEffect(string _uid, int _layer, Entity _dealer, Entity _target, ulong? eventId)
        {
            if (!OverTimeEffectDic.TryGetValue(_uid, out var effect))
            {
                OverTimeEffect _setting = GameManager.GetOverTimeEffect(_uid);
                if (_setting == null) return;
                effect = new OverTimeEffectInstance(_setting);
                OverTimeEffectDic.Add(_uid, effect);
            }
            effect.Execute(_uid, _layer, _dealer, _target, eventId);
        }



    }
}
