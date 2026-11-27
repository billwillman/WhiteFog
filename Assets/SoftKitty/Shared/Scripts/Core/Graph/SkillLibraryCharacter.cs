using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "新角色", menuName = "Soft Kitty/技能库角色")]
    public sealed class SkillLibraryCharacter : ScriptableObject
    {
        public enum Camp { [InspectorName("友方")] Friendly, [InspectorName("敌方")] Enemy }
        [InspectorName("阵营")] public Camp camp;
        [InspectorName("MCC 实体 UID")] public string entityUid;
        [InspectorName("阵营默认试放角色")] public bool defaultForPreview;
        [InspectorName("敌位预览模型")] public GameObject enemyPreviewModel;
    }
}
