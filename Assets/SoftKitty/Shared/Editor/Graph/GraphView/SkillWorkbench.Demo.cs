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
        private double demoDeadline;
        private double nextReadout;
        private EntityComponent lastCaster, lastReceiver;
        private string demoResult;

        private void DrawDemoPreview()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Demo 快速试放", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                bool opponent = EditorGUILayout.Popup("默认施放方向", graph.previewAsOpponent ? 1 : 0,
                    new[] { "主角 → 怪物", "怪物 → 主角" }) == 1;
                if (opponent != graph.previewAsOpponent) Change(() => graph.previewAsOpponent = opponent);
            }
            stageActors = EditorGUILayout.Toggle("让怪物就位到主角前方", stageActors);
            if (stageActors) demoRange = EditorGUILayout.Slider("起始距离（米）", demoRange, 2, 20);
            bool inDemo = SceneManager.GetActiveScene().path == DemoPath;
            using (new EditorGUI.DisabledScope(demoQueued || (EditorApplication.isPlaying && !inDemo)))
                if (GUILayout.Button(EditorApplication.isPlaying ? "在 Demo 试放所选技能" : "打开 Demo 并试放所选技能"))
                    QueueDemoPreview();
            if (!string.IsNullOrEmpty(demoResult)) EditorGUILayout.HelpBox(demoResult, MessageType.Info);
            if (Application.isPlaying && lastCaster && lastReceiver)
                EditorGUILayout.LabelField("当前生命", lastCaster.uid + "  " + lastCaster.GetAttributeFloat("chp").ToString("0.#")
                    + "     →     " + lastReceiver.uid + "  " + lastReceiver.GetAttributeFloat("chp").ToString("0.#"));
        }

        internal void QueueDemoPreview()
        {
            if (!graph || demoQueued) return;
            if (SceneManager.GetActiveScene().path != DemoPath)
            {
                if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(DemoPath);
            }
            AssetDatabase.SaveAssetIfDirty(graph);
            demoQueued = true;
            demoDeadline = 0;
            demoResult = "正在等待 Demo 中的主角与怪物";
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
                if (TryDemoCast(graph, stageActors, demoRange, out lastCaster, out lastReceiver, out demoResult)
                    || EditorApplication.timeSinceStartup >= demoDeadline) demoQueued = false;
            }
            if (EditorApplication.timeSinceStartup >= nextReadout && (demoQueued || lastCaster))
            { nextReadout = EditorApplication.timeSinceStartup + .18; Repaint(); }
        }

        internal static bool TryDemoCast(GraphObject skill, bool arrange, float distance, out EntityComponent caster,
            out EntityComponent receiver, out string info)
        {
            caster = receiver = null;
            info = "等待可交互的主角和怪物；若角色已死亡，请在 Demo 中重新开始。";
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
            caster = skill.previewAsOpponent ? enemy : player;
            receiver = skill.previewAsOpponent ? player : enemy;
            var go = new GameObject("试放 · " + skill.name);
            go.transform.position = caster.transform.position;
            var damage = go.AddComponent<CombatDamage>();
            damage.PhysicsDetection = false;
            damage.mGraph = skill;
            damage.Init();
            if (!damage.inited) { Destroy(go); info = "技能图初始化失败，请检查 Console。"; return false; }
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
