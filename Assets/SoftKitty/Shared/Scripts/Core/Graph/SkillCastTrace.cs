using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    // Owned by one observed cast; detached projectiles retain that same trace.
    [Serializable]
    public sealed class SkillCastTrace
    {
        public enum Event { Contact, HealthLoss }

        [Serializable]
        public struct Mark
        {
            public Event kind;
            public string node, trigger, emitter, target, via, viaTrigger;
            public int flight, segment;
            public float seconds, loss;
        }

        [SerializeField] private List<Mark> marks = new List<Mark>();
        public IReadOnlyList<Mark> Marks => marks;
        private double started;
        private int flights;
        internal void Begin() => started = Time.timeAsDouble;
        internal int NextFlight() => ++flights;

        internal void Add(Event kind, string node, Entity target, GraphRuntimeState state, int segment = 0, float loss = 0, string via = null, string viaTrigger = null)
        {
            marks.Add(new Mark {
                kind = kind, node = state.traceOwner ?? node, trigger = state.traceTrigger, via = via, viaTrigger = viaTrigger,
                emitter = state.traceEmitter, flight = state.traceFlight, target = target.CombatRandomId,
                seconds = Mathf.Max(0, (float)(Time.timeAsDouble - started)), segment = segment, loss = loss
            });
        }
    }
}
