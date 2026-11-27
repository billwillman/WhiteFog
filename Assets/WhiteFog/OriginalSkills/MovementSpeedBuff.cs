using System.Runtime.CompilerServices;
using UnityEngine;

namespace SoftKitty
{
    // Gameplay bridge for the shared movement Buff. MCC owns duration, layers and dispel.
    internal sealed class MovementSpeedBuff
    {
        private const string PerLayer = "每层移速倍率";
        private static readonly ConditionalWeakTable<Entity, MovementSpeedBuff> observers =
            new ConditionalWeakTable<Entity, MovementSpeedBuff>();
        private readonly Entity target;

        internal static void Observe(Entity entity)
        {
            if (entity != null) observers.GetValue(entity, Attach);
        }

        private static MovementSpeedBuff Attach(Entity entity) => new MovementSpeedBuff(entity);

        private MovementSpeedBuff(Entity entity)
        {
            target = entity;
            entity.RegisterOverTimeChangeCallback(OnChange);
            foreach (var state in entity.GetOverTimeEffectList())
                OnChange(state.uid, state, OverTimeEffectEventType.Add);
        }

        private void OnChange(string uid, OverTimeEffectData state, OverTimeEffectEventType change)
        {
            var definition = GameManager.GetOverTimeEffect(uid);
            if (!(definition?.DesignGraph is GraphObject graph)) return;
            var variable = graph.RootSet.Variables.Find(v =>
                v.Type == VariableType.DynamicVariable && v.VariableUid == PerLayer);
            if (variable == null) return;
            var factor = definition.DynamicVariables?.Find(v => v.uid == PerLayer)?.value ?? variable.Constant;

            // Use the same source as the MCC effect graph, including on its final tick.
            // Replacing this source with the neutral multiplier preserves every other modifier.
            bool removed = change == OverTimeEffectEventType.Remove;
            float multiplier = removed ? 1f : Mathf.Pow(factor, state.layer);
            float duration = removed ? 0f : state.timer + GameManager.OverTimeEffectData.OverTimeEffectInterval;
            target.AddTempAttributes("speed", multiplier, duration, BasicMathFunction.Multiply, uid);
        }
    }
}
