using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "新角色", menuName = "Soft Kitty/技能库角色")]
    public sealed class SkillLibraryCharacter : ScriptableObject
    {
        public enum Camp { [InspectorName("友方")] Friendly, [InspectorName("敌方")] Enemy }
        [InspectorName("阵营")] public Camp camp;
    }
}
