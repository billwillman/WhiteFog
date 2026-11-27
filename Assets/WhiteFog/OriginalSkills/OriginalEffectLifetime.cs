using UnityEngine;

namespace SoftKitty
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class OriginalEffectLifetime : MonoBehaviour
    {
        [Min(0)] public float seconds;
    }
}
