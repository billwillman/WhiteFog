using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private const string DemoPath = "Assets/SoftKitty/MasterCombatCore/Demo/Scenes/DemoScene.unity";
        private const string StagePath = "Assets/WhiteFog/SkillPreview/角色预览.unity";
        private const float PortraitAspect = 9f / 16f;
        private static readonly Vector3 StageCenter = new Vector3(0, 10f, 20);
        private const string PreviousStart = "SoftKitty.Workbench.PreviousPlayStart";
        private const string OwnsStart = "SoftKitty.Workbench.OwnsPlayStart";
        private Camera monitorCamera;
        private RenderTexture monitor;
        [SerializeField] private bool showGamePreview;
        private Camera stageCamera;
        private Rect savedStageRect;
        private RenderTexture savedStageTarget;
        private bool savedStageEnabled;
        private static bool InPreviewStage => SceneManager.GetActiveScene().path == StagePath;

        [InitializeOnLoadMethod]
        private static void RegisterPreviewSceneRestore()
        {
            EditorApplication.playModeStateChanged -= RestorePreviewStart;
            EditorApplication.playModeStateChanged += RestorePreviewStart;
        }

        private static void RestorePreviewStart(PlayModeStateChange change)
        {
            if ((change != PlayModeStateChange.EnteredPlayMode && change != PlayModeStateChange.EnteredEditMode) ||
                !SessionState.GetBool(OwnsStart, false)) return;
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == StagePath)
            {
                string path = SessionState.GetString(PreviousStart, "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
            SessionState.EraseBool(OwnsStart);
            SessionState.EraseString(PreviousStart);
        }

        private void OnPreviewSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (demoQueued && scene.path == StagePath && Camera.main)
                SetStageOutput(Camera.main);
        }

        private void EnsureMonitorTexture()
        {
            if (!monitor)
                monitor = new RenderTexture(540, 960, 24) { name = "技能工作台 Preview", hideFlags = HideFlags.DontSave };
        }

        private void SetStageOutput(Camera cam)
        {
            if (!stageCamera)
            {
                stageCamera = cam;
                savedStageRect = cam.rect;
                savedStageTarget = cam.targetTexture;
                savedStageEnabled = cam.enabled;
            }
            EnsureMonitorTexture();
            cam.enabled = true;
            if (showGamePreview)
            {
                cam.targetTexture = savedStageTarget;
                float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                float w = Mathf.Min(1, PortraitAspect / screenAspect), h = Mathf.Min(1, screenAspect / PortraitAspect);
                cam.rect = new Rect((1-w)*.5f, (1-h)*.5f, w, h);
            }
            else
            {
                if (monitorCamera) monitorCamera.enabled = false;
                cam.targetTexture = monitor;
                cam.rect = new Rect(0, 0, 1, 1);
            }
        }

        private void RestoreStageOutput()
        {
            if (stageCamera)
            {
                stageCamera.targetTexture = savedStageTarget;
                stageCamera.rect = savedStageRect;
                stageCamera.enabled = savedStageEnabled;
            }
            stageCamera = null;
            savedStageTarget = null;
        }

        private void FrameStageCamera()
        {
            var cam = stageCamera ? stageCamera : Camera.main;
            if (!cam) return;
            SetStageOutput(cam);
            cam.transform.rotation = Quaternion.Euler(38, 0, 0);
            cam.transform.position = StageCenter + Vector3.up * 1.05f - cam.transform.forward * 16;
            cam.orthographicSize = Mathf.Max(3.75f, demoRange * .32f + 2.3f);
            if ((graph as OriginalSkillGraph)?.previewEnemy)
                cam.orthographicSize = Mathf.Max(cam.orthographicSize, 5.9f);
        }

        private void DrawStageCamera(EntityComponent from = null, EntityComponent to = null)
        {
            var cam = stageCamera ? stageCamera : Camera.main;
            if (!cam && !from) return;
            if (!from && InPreviewStage)
            {
                SetStageOutput(cam);
                if (!showGamePreview)
                {
                    DrawMonitorTexture();
                    return;
                }
            }
            EnsureMonitorTexture();
            if (!monitorCamera)
            {
                monitorCamera = new GameObject("工作台预览镜头").AddComponent<Camera>();
                if (cam) monitorCamera.CopyFrom(cam);
                if (cam && cam.TryGetComponent<OriginalSkillPreviewLight>(out var stageLight)) stageLight.monitorCamera = monitorCamera;
                monitorCamera.targetTexture = monitor;
                monitorCamera.rect = new Rect(0, 0, 1, 1);
            }
            monitorCamera.enabled = true;
            monitorCamera.orthographic = true;
            if (from && to)
            {
                Vector3 delta = to.transform.position - from.transform.position;
                Vector3 forward = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                Vector3 center = (from.transform.position + to.transform.position) * .5f + Vector3.up;
                monitorCamera.transform.position = center - forward * 12 + Vector3.up * 9.4f;
                monitorCamera.transform.LookAt(center);
                monitorCamera.orthographicSize = Mathf.Max(3.05f, delta.magnitude * .32f + 1.45f, Mathf.Abs(delta.y) * .5f + 2);
            }
            else
            {
                monitorCamera.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
                monitorCamera.orthographicSize = cam.orthographicSize;
            }
            DrawMonitorTexture();
        }

        private void DrawMonitorTexture()
        {
            float height = Mathf.Clamp(position.height - 315f, 225, 850);
            var row = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            float width = Mathf.Min(row.width, height * PortraitAspect);
            var rect = new Rect(row.center.x - width * .5f, row.y, width, width / PortraitAspect);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawPreviewTexture(rect, monitor, null, ScaleMode.ScaleToFit);
        }

        private void ReleasePreviewCamera()
        {
            RestoreStageOutput();
            if (monitorCamera) { monitorCamera.targetTexture = null; DestroyImmediate(monitorCamera.gameObject); }
            if (monitor) { monitor.Release(); DestroyImmediate(monitor); }
        }
        [SerializeField] private bool demoQueued;
        [SerializeField] private float demoRange = 8f;
        [SerializeField] private int demoSkillLevel = 1;
        [SerializeField] private float previewHitDamage = 10f;

        [SerializeField] private SkillDamageReport previewDamageReport;

        private List<(int index, string target, float loss, float shield)> measuredHits;
        private string measuredCaster;

        private static bool HasUnwiredHit(GraphObject skill) => skill.RootSet.Actions.Any(a =>
            (SkillProjectile.IsLaunch(a) || a.ClassName == "OriginalSkillVisual" && a.IntValues.FirstOrDefault() == 6 &&
             OriginalSkillVisualAction.EffectFor(a, skill as OriginalSkillGraph)?.triggerHit == true) &&
            (a.outputPort.Count == 0 || a.outputPort[0].uid.Count == 0));

        private void DrawPreviewHitDamage()
        {
            var segments = graph.RootSet.Actions.Where(a => a.ClassName == "FormulaImpact" && a.calculation?.independentSegment == true)
                .Select(a => a.calculation).OrderBy(c => c.logicIndex).ToArray();
            if (segments.Length > 0)
            {
                previewDamageReport = (SkillDamageReport)EditorGUILayout.ObjectField("对照战报（可选）", previewDamageReport,
                    typeof(SkillDamageReport), false);
                bool matches = previewDamageReport && graph is OriginalSkillGraph original &&
                    previewDamageReport.skillId == original.sourceId && (measuredCaster == null || measuredCaster == previewDamageReport.casterId);
                if (previewDamageReport && !matches) EditorGUILayout.HelpBox("所选战报与当前技能或施放者不符，试放继续使用技能公式。", MessageType.Warning);
                foreach (var c in segments)
                {
                    var seen = measuredHits?.Where(h => h.index == c.logicIndex);
                    var rows = matches ? previewDamageReport.entries?.Where(e => e.logicIndex == c.logicIndex) : null;
                    string label = "逻辑段 " + c.logicIndex + (c.segmentRatioVerified ? " · 已核实" : " · 倍率待核");
                    string actual = seen == null ? "" : string.Join("，", seen.Select(h => h.target + " 扣血 " + h.loss.ToString("G6") + " / 耗盾 " + h.shield.ToString("G6")));
                    EditorGUILayout.LabelField(label, actual.Length > 0 ? actual : "尚无实算命中", EditorStyles.wordWrappedMiniLabel);
                    if (matches)
                    {
                        string expected = rows == null ? "" : string.Join("，", rows.Select(e =>
                            e.targetId + " 扣血 " + (e.healthBefore - e.healthAfter) + " / 耗盾 " + e.shieldSpent));
                        EditorGUILayout.LabelField("战报对照", expected.Length > 0 ? expected : "本段无记录", EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
            if (HasUnwiredHit(graph)) DrawPreviewDamageInput();
        }

        private void DrawPreviewDamageInput()
        {
            previewHitDamage = Mathf.Max(0, EditorGUILayout.FloatField(new GUIContent("每段预览来伤",
                "仅用于工作台试放。飞剑命中口有连线时执行连线；设为 0 可关闭预览回调的扣血。"), previewHitDamage));
        }
        [SerializeField] private int previewDirection;
        [SerializeField] private int previewMode;
        private Vector2 previewScroll;
        private static readonly string[] PreviewModes = { "快速试放", "当前场景试放" };
        private float healthBefore;
        private const string EnemyModelSlot = "原作怪物模型";

        private static string PreviewActorName(EntityComponent actor)
        {
            var model = actor.transform.Find(EnemyModelSlot);
            return model && model.gameObject.activeSelf
                ? model.GetComponent<Animator>()?.runtimeAnimatorController?.name ?? actor.name
                : actor.name;
        }

        private bool PreviewAsOpponent => graph is OriginalSkillGraph ? graph.previewAsOpponent : (previewDirection == 0 ? graph.previewAsOpponent : previewDirection == 2);
        private double nextReadout;
        private EntityComponent lastCaster, lastReceiver;
        private string demoResult;

        private void DrawPreviewPage()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) DrawSkillPicker();
            using (var view = new EditorGUILayout.ScrollViewScope(previewScroll))
            {
                previewScroll = view.scrollPosition;
                if (showLibrary || !graph)
                {
                    DrawLibrary();
                    return;
                }
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
            if (!(graph is OriginalSkillGraph))
            {
                EditorGUILayout.LabelField("当前技能", "自定义技能图");

            }
            else EditorGUILayout.LabelField("来源", "《宗师之上》" + ((OriginalSkillGraph)graph).sourceVersion + " · " + ((OriginalSkillGraph)graph).sourceId);
            if (!(graph is OriginalSkillGraph)) previewDirection = EditorGUILayout.Popup("本次施放方向", previewDirection,
                new[] { graph.previewAsOpponent ? "按技能默认（怪物 → 主角）" : "按技能默认（主角 → 怪物）", "主角 → 怪物", "怪物 → 主角" });
            bool reverse = PreviewAsOpponent;
            EditorGUILayout.LabelField("施放者 → 目标", reverse ? ((graph as OriginalSkillGraph)?.previewEnemy?.name ?? "小雕") + " → 男角色 001" : "男角色 001 → 小雕");
            demoRange = EditorGUILayout.Slider("双方距离（米）", demoRange, 2, 20);
            if (UsesSkillLevel(graph)) demoSkillLevel = Mathf.Max(1, EditorGUILayout.DelayedIntField("试放技能等级", demoSkillLevel));
            DrawPreviewHitDamage();
            showGamePreview = EditorGUILayout.ToggleLeft(new GUIContent("同时在 Game 视图预览",
                "快速试放只运行独立角色预览场景。勾选后同步显示竖屏战斗画面。"), showGamePreview);
            bool inStage = InPreviewStage;
            if (Application.isPlaying && !inStage)
            {
                EditorGUILayout.HelpBox("当前在运行「" + SceneManager.GetActiveScene().name + "」。要试放场上角色，请选「当前场景试放」；要看男角色 001与小雕，请先退出 Play。", MessageType.Info);
                if (GUILayout.Button("退出当前 Play")) EditorApplication.isPlaying = false;
                return;
            }
            using (new EditorGUI.DisabledScope(demoQueued || EditorApplication.isPaused))
                if (GUILayout.Button(inStage && Application.isPlaying ? "再次试放所选技能" : "打开角色预览并试放"))
                    QueueDemoPreview();
            if (Application.isPlaying && EditorApplication.isPaused) EditorGUILayout.HelpBox("Play 已暂停，继续播放后可以试放。", MessageType.Info);
            if (!string.IsNullOrEmpty(demoResult)) EditorGUILayout.HelpBox(demoResult, MessageType.Info);
            if (inStage && Application.isPlaying)
            {
                DrawStageCamera();
                if (graph is OriginalSkillGraph)
                    EditorGUILayout.LabelField("表现回放", "原作动作、施放与命中特效");
                if (lastCaster && lastReceiver)
                {
                    EditorGUILayout.LabelField("施放者生命", PreviewActorName(lastCaster) + "  " + lastCaster.GetAttributeFloat("chp").ToString("0.##"));
                    EditorGUILayout.LabelField("目标生命", PreviewActorName(lastReceiver) + "  " + healthBefore.ToString("0.##") + " → " + lastReceiver.GetAttributeFloat("chp").ToString("0.##"));
                    EditorGUILayout.LabelField("施放者状态", BuffReadout(lastCaster), EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("目标状态", BuffReadout(lastReceiver), EditorStyles.wordWrappedLabel);
                }
                DrawInterruptButton(lastCaster, ref demoResult);
                if (GUILayout.Button("退出角色预览")) EditorApplication.isPlaying = false;
            }
        }

        private static void DrawInterruptButton(EntityComponent caster, ref string status)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || EditorApplication.isPaused || !caster))
                if (GUILayout.Button("打断施放者"))
                {
                    caster.InterruptCast();
                    status = "已打断 " + caster.name + " 的施法。已离手的投射物继续飞行。";
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
            if (Application.isPlaying && !InPreviewStage) return;
            if (!ValidatePreviewLevel(graph, demoSkillLevel, out demoResult)) return;
            if (!Application.isPlaying)
            {
                var start = AssetDatabase.LoadAssetAtPath<SceneAsset>(StagePath);
                if (!start) { demoResult = "找不到角色预览场景：" + StagePath; return; }
                var prior = EditorSceneManager.playModeStartScene;
                SessionState.SetString(PreviousStart, prior ? AssetDatabase.GetAssetPath(prior) : "");
                SessionState.SetBool(OwnsStart, true);
                EditorSceneManager.playModeStartScene = start;
            }
            demoQueued = true;
            demoResult = "正在打开角色预览场景";
            if (!Application.isPlaying) EditorApplication.isPlaying = true;
        }

        private void UpdateDemoPreview()
        {
            if (!Application.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    demoQueued = false;
                    if (monitor) ReleasePreviewCamera();
                }
                return;
            }
            if (stageCamera && (!InPreviewStage || previewMode != 0))
                RestoreStageOutput();
            if (stageCamera && !showGamePreview)
                stageCamera.enabled = page == PreviewPage;
            if (monitorCamera && (page != PreviewPage || (previewMode == 1 && (!dealer || !target)))) monitorCamera.enabled = false;
            if (demoQueued && InPreviewStage && !EditorApplication.isPaused && Time.timeSinceLevelLoad > 0)
            {
                demoQueued = false;
                var actors = SceneManager.GetActiveScene().GetRootGameObjects();
                var hero = actors.FirstOrDefault(go => go.name == "男角色 001")?.GetComponent<EntityComponent>();
                var monster = actors.FirstOrDefault(go => go.name == "小雕")?.GetComponent<EntityComponent>();
                if (!hero || !monster || hero.mData == null || monster.mData == null)
                    demoResult = "预览场缺少有效的男角色 001（player）或小雕（EnemyBot01）实体。";
                else
                {
                    hero.SetPosition(StageCenter + Vector3.back * demoRange * .5f, false);
                    monster.SetPosition(StageCenter + Vector3.forward * demoRange * .5f, false);
                    hero.transform.forward = Vector3.forward;
                    monster.transform.forward = Vector3.back;
                    // The extracted bird faces local -Z. Keep the entity forward available for skill targeting.
                    var birdModel = monster.transform.Find("小雕 · 飞行");
                    if (birdModel) birdModel.rotation = Quaternion.LookRotation(-monster.transform.forward, monster.transform.up);
                    var enemyPrefab = (graph as OriginalSkillGraph)?.previewEnemy;
                    if (birdModel) birdModel.gameObject.SetActive(!enemyPrefab);
                    const string enemySlot = EnemyModelSlot;
                    var previous = monster.transform.Find(enemySlot);
                    if (previous && (!enemyPrefab || previous.gameObject.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController !=
                        enemyPrefab.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController))
                    {
                        previous.gameObject.SetActive(false);
                        previous.name = enemySlot + "（退出）";
                        Destroy(previous.gameObject);
                        previous = null;
                    }
                    if (enemyPrefab && !previous)
                    {
                        var view = Instantiate(enemyPrefab, monster.transform, false);
                        view.name = enemySlot;
                    }
                    hero.UpdateData();
                    monster.UpdateData();
                    FrameStageCamera();
                    if (!showGamePreview) Focus();
                    lastCaster = PreviewAsOpponent ? monster : hero;
                    lastReceiver = PreviewAsOpponent ? hero : monster;
                    healthBefore = lastReceiver.GetAttributeFloat("chp");
                    CastPreviewActors(lastCaster, lastReceiver, out demoResult);
                }
                Repaint();
            }
            if (page == PreviewPage && EditorApplication.timeSinceStartup >= nextReadout)
            { nextReadout = EditorApplication.timeSinceStartup + .05; Repaint(); }
        }

        private void CastPreviewActors(EntityComponent caster, EntityComponent receiver, out string result, CombatDamage damage = null)
        {
            var hits = new List<(int, string, float, float)>();
            measuredHits = hits;
            measuredCaster = caster ? caster.mData?.CombatRandomId : null;
            CastActors(graph, caster, receiver, demoSkillLevel, out result, damage, previewHitDamage,
                (index, target, loss, shield) => hits.Add((index, target.CombatRandomId, loss, shield)));
        }

        private static bool ValidatePreviewLevel(GraphObject skill, int level, out string info)
        {
            foreach (var table in BoundLevelTables(skill))
                if (!table.rows.Any(row => row.level == level))
                { info = table.name + "没有 " + level + " 级，请选择已配置的等级。"; return false; }
            info = "";
            return true;
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
            return CastActors(skill, caster, receiver, skillLevel, out info);
        }

        private static bool CastActors(GraphObject skill, EntityComponent caster, EntityComponent receiver, int skillLevel, out string info, CombatDamage damage = null, float previewHitDamage = 10f, System.Action<int, Entity, float, float> onSettled = null)
        {
            if (!caster || !receiver || !caster.isActiveAndEnabled || !receiver.isActiveAndEnabled ||
                !caster.AvailableForInteraction || !receiver.AvailableForInteraction)
            { info = "施放者或目标不可交互，请检查实体数据和存活状态。"; return false; }
            if (SkillPlayback.FindActor(caster.mData) != caster || SkillPlayback.FindActor(receiver.mData) != receiver)
            { info = "场景中有多个角色共用同一份实体数据。请为所选角色使用独立实体或启用多实例。"; return false; }
            var original = skill as OriginalSkillGraph;
            foreach (var node in skill.RootSet.Actions.Where(SkillMotion.UsesBinding))
            {
                // Conditional and dynamic branches validate when the action actually runs.
                if (!skill.RootSet.outputPort[0].uid.Contains(node.Uid + "#0")) continue;
                var entity = MotionEntity(skill, node);
                var actor = entity == EntityType.Dealer ? caster : entity == EntityType.Target ? receiver : null;
                if (!actor) continue; // Dynamic entity bindings resolve when the graph reaches the action.
                string error = SkillMotion.For(node, original).Check(actor.SkillAnimator);
                if (error != null) { info = node.Uid + " · " + error; return false; }
            }
            if (!ValidatePreviewLevel(skill, skillLevel, out info)) return false;
            if (!damage)
            {
                var go = new GameObject("试放 · " + skill.name);
                SceneManager.MoveGameObjectToScene(go, caster.gameObject.scene);
                go.transform.position = caster.transform.position;
                damage = go.AddComponent<CombatDamage>();
                damage.PhysicsDetection = false;
                damage.mGraph = skill;
                damage.Init();
                if (!damage.inited) { Destroy(go); info = "技能图初始化失败，请检查 Console。"; return false; }
                SkillPlayback.On(damage).RetireAfter(1 + skill.RootSet.Actions.Select(a => a.WaitTime + Span(a)).DefaultIfEmpty(0).Max());
            }
            if (UsesSkillLevel(skill)) damage.SetDynamicValue("skill.level", Mathf.Max(1, skillLevel));
            SkillDamageNumbers.Observe(caster);
            SkillDamageNumbers.Observe(receiver);
            if (HasUnwiredHit(skill))
            {
                var playback = SkillPlayback.On(damage);
                var previousImpact = playback.OriginalProjectileImpact;
                playback.OriginalProjectileImpact = (source, victim) => SkillPlayback.ApplyIncomingDamage(source, victim, previewHitDamage);
                try { damage.DealDamageObserved(caster.mData, receiver.mData, onSettled); }
                finally { playback.OriginalProjectileImpact = previousImpact; }
            }
            else damage.DealDamageObserved(caster.mData, receiver.mData, onSettled);
            var description = original ? "原作表现 " + original.sourceId
                : UsesSkillLevel(skill) ? skillLevel + " 级" : "自定义技能图";
            info = skill.name + "  ·  " + description + "  ·  " + PreviewActorName(caster) + "（" + caster.uid + "） → " + PreviewActorName(receiver) + "（" + receiver.uid + "）  ·  起始距离 "
                + Vector3.Distance(caster.transform.position, receiver.transform.position).ToString("0.0") + " m";
            return true;
        }
    }
}
