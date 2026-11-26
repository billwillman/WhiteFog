using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private const string DemoPath = "Assets/SoftKitty/MasterCombatCore/Demo/Scenes/DemoScene.unity";
        [SerializeField] private bool demoQueued;
        [SerializeField] private bool stageActors = true;
        [SerializeField] private float demoRange = 8f;
        [SerializeField] private int demoSkillLevel = 1;
        [SerializeField] private int previewDirection;
        [SerializeField] private int previewMode;
        private Vector2 previewScroll;
        private static readonly string[] PreviewModes = { "快速试放", "当前场景试放" };
        private double demoDeadline;
        private double nextReadout;
        private EntityComponent lastCaster, lastReceiver;
        private string demoResult;

        private void DrawPreviewPage()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) DrawSkillPicker();
            using (var view = new EditorGUILayout.ScrollViewScope(previewScroll))
            {
                previewScroll = view.scrollPosition;
                if (showLibrary || !graph) DrawLibrary();
                if (!graph) return;
                if (graph.RootSet == null || graph.RootSet.Uid != "Node0")
                {
                    EditorGUILayout.HelpBox("请先在「技能编辑」中初始化技能时间轴。", MessageType.Info);
                    return;
                }
                if (MasterGraphWindow.IsEditing(graph))
                {
                    EditorGUILayout.HelpBox("请先保存并关闭这张技能的节点图，再进行试放。", MessageType.Info);
                    return;
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(graph.name, EditorStyles.largeLabel);
                previewMode = GUILayout.Toolbar(previewMode, PreviewModes);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (previewMode == 0) DrawQuickPreview();
                    else DrawPlayTest();
                }
            }
        }

        private void DrawQuickPreview()
        {
            previewDirection = EditorGUILayout.Popup("本次施放方向", previewDirection,
                new[] { graph.previewAsOpponent ? "按技能默认（怪物 → 主角）" : "按技能默认（主角 → 怪物）", "主角 → 怪物", "怪物 → 主角" });
            stageActors = EditorGUILayout.Toggle("让怪物就位到主角前方", stageActors);
            if (stageActors) demoRange = EditorGUILayout.Slider("起始距离（米）", demoRange, 2, 20);
            if (UsesSkillLevel(graph)) demoSkillLevel = Mathf.Max(1, EditorGUILayout.DelayedIntField("试放技能等级", demoSkillLevel));
            bool inDemo = SceneManager.GetActiveScene().path == DemoPath;
            using (new EditorGUI.DisabledScope(demoQueued || (EditorApplication.isPlaying && !inDemo)))
                if (GUILayout.Button(EditorApplication.isPlaying ? "在 Preview 中试放所选技能" : "打开 Preview 场景并试放"))
                    QueueDemoPreview();
            if (!string.IsNullOrEmpty(demoResult)) EditorGUILayout.HelpBox(demoResult, MessageType.Info);
            if (Application.isPlaying && lastCaster && lastReceiver)
            {
                EditorGUILayout.LabelField("当前生命", lastCaster.uid + "  " + lastCaster.GetAttributeFloat("chp").ToString("0.#")
                    + "     →     " + lastReceiver.uid + "  " + lastReceiver.GetAttributeFloat("chp").ToString("0.#"));
                EditorGUILayout.LabelField("施放者状态", BuffReadout(lastCaster), EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("目标状态", BuffReadout(lastReceiver), EditorStyles.wordWrappedLabel);
            }
        }

        private static string BuffReadout(EntityComponent actor)
        {
            var effects = actor.mData.GetOverTimeEffectList();
            return effects.Count == 0 ? "无" : string.Join("、", effects.Select(e => e.uid + " ×" + e.layer + "（" + e.timer.ToString("0.#") + " 秒）"));
        }

        internal void QueueDemoPreview()
        {
            if (!graph || demoQueued) return;
            if (SceneManager.GetActiveScene().path != DemoPath)
            {
                if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(DemoPath);
            }
            demoQueued = true;
            demoDeadline = 0;
            demoResult = "正在等待预览场景中的主角与怪物";
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        private void UpdateDemoPreview()
        {
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) demoQueued = false;
                return;
            }
            if (demoQueued && graph && SceneManager.GetActiveScene().path == DemoPath)
            {
                if (demoDeadline == 0) demoDeadline = EditorApplication.timeSinceStartup + 20;
                if (TryDemoCast(graph, stageActors, demoRange, out lastCaster, out lastReceiver, out demoResult, demoSkillLevel,
                    previewDirection == 0 ? graph.previewAsOpponent : previewDirection == 2)
                    || EditorApplication.timeSinceStartup >= demoDeadline) demoQueued = false;
            }
            if (EditorApplication.timeSinceStartup >= nextReadout && (demoQueued || lastCaster))
            { nextReadout = EditorApplication.timeSinceStartup + .18; Repaint(); }
        }

        private static bool UsesSkillLevel(GraphObject skill) => skill && skill.RootSet != null &&
            skill.RootSet.Variables.Any(v => v.Type == VariableType.DynamicVariable && v.VariableUid == "skill.level");

        internal static bool TryDemoCast(GraphObject skill, bool arrange, float distance, out EntityComponent caster,
            out EntityComponent receiver, out string info, int skillLevel = 1, bool? asOpponent = null)
        {
            caster = receiver = null;
            info = "正在等待可交互的主角和怪物。若角色已死亡，请在预览场景中重新开始。";
            if (!Application.isPlaying || !skill || skill.RootSet == null || SceneManager.GetActiveScene().path != DemoPath) return false;
            var player = GameManager.GetPlayerInstance();
            if (!player || !player.AvailableForInteraction) return false;
            var enemy = GameManager.GetEntityInstanceList().Where(e => e && e != player && e.isActiveAndEnabled &&
                e.gameObject.scene == player.gameObject.scene && e.AvailableForInteraction && e.GetComponent("EnemyControl"))
                .OrderBy(e => (e.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (!enemy) return false;
            if (arrange)
            {
                var dir = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                enemy.SetPosition(player.transform.position + (dir == Vector3.zero ? Vector3.forward : dir) * distance, false);
                Physics.SyncTransforms();
            }
            bool opponent = asOpponent ?? skill.previewAsOpponent;
            caster = opponent ? enemy : player;
            receiver = opponent ? player : enemy;
            var go = new GameObject("试放 · " + skill.name);
            go.transform.position = caster.transform.position;
            var damage = go.AddComponent<CombatDamage>();
            damage.PhysicsDetection = false;
            damage.mGraph = skill;
            damage.Init();
            if (!damage.inited) { Destroy(go); info = "技能图初始化失败，请检查 Console。"; return false; }
            if (UsesSkillLevel(skill)) damage.SetDynamicValue("skill.level", Mathf.Max(1, skillLevel));
            // A separate executor owns each cast, including its delayed hits and visuals.
            var host = SkillPlayback.On(damage);
            host.RetireAfter(1 + skill.RootSet.Actions.Select(a => a.WaitTime + Span(a)).DefaultIfEmpty(0).Max());
            damage.DealDamage(caster.mData, receiver.mData);
            info = skill.name + "  ·  " + caster.uid + " → " + receiver.uid + "  ·  起始距离 "
                + Vector3.Distance(caster.transform.position, receiver.transform.position).ToString("0.0") + " m";
            return true;
        }
    }
}
