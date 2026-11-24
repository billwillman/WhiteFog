using System;
using UnityEngine;

[CreateAssetMenu(menuName = "White Fog/Fog preset")]
public sealed class FogPreset : ScriptableObject
{
    [Serializable]
    public class Transition
    {
        public int start, end;
        public bool twoWay;
        public float duration;
        public AnimationCurve visibility, radius;
    }

    public Gradient tint, directionalColor;
    public AnimationCurve directionalIntensity;
    public Transition[] transitions;
    public Transition GetTransition(int from, int to)
    {
        foreach (var entry in transitions)
        {
            if (entry.twoWay)
            {
                if ((from == entry.start || from == entry.end) && (to == entry.start || to == entry.end))
                    return entry;
            }
            else if (entry.start == from && entry.end == to)
                return entry;
        }
        return transitions[transitions.Length - 1];
    }
}
