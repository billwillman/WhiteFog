using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench : EditorWindow
    {
        [SerializeField] private GraphObject graph;
        [SerializeField] private string selection;
        private Vector2 scroll;
        private GraphObject[] library;
        private string filter = "";
        private bool showLibrary;
        private SkillLibraryCharacter[] libraryCharacters;
        [SerializeField] private SkillLibraryCharacter selectedLibraryCharacter;
        [SerializeField] private int libraryCamp;
        private string characterFilter = "";
        private GraphObject[] projectSkills;
        private bool showConditions;
        private static readonly string[] EntityRoleLabels = { "施法者（自己）", "本次目标", "按 UID 指定实体", "高级：全局玩家", "高级：技能执行实例", "投射物名单中的下一目标", "上游选出的目标" };
        private static readonly int[] EntityRoleValues = { (int)EntityType.Dealer, (int)EntityType.Target, (int)EntityType.Specified, (int)EntityType.Player, (int)EntityType.Instance, (int)EntityType.NextCastTarget, (int)EntityType.SelectedTarget };
        [SerializeField] private CombatDamage executor;
        private Editor componentEditor;
        private bool showExecutor;
        [SerializeField] private EntityComponent dealer;
        [SerializeField] private EntityComponent target;
        private float cursor;
        private string message = "";
        private static readonly string[] Modes = { "单次", "执行多次", "持续执行" };
        private const int PreviewPage = 3;
        private static readonly string[] Pages = { "技能编辑", "Buff 状态", "公式与数值", "Preview", "属性定义", "角色属性" };

        [MenuItem("Window/Soft Kitty/技能工作台")]
        private static void FromMenu() => Open(Selection.activeObject as GraphObject);

        public static void Open(GraphObject asset)
        {
            var window = GetWindow<SkillWorkbench>("技能工作台");
            window.minSize = new Vector2(590, 470);
            if (asset)
            {
                window.page = 0;
                window.SelectGraph(asset);
            }
            window.Show();
        }

        internal static void OpenAction(GraphObject asset, string uid)
        {
            Open(asset);
            var window = GetWindow<SkillWorkbench>();
            window.selection = uid;
            window.Repaint();
        }

        private void SelectGraph(GraphObject asset)
        {
            motionTimings.Clear();
            motionParameters.Clear();
            if (asset is SkillFormula formula)
            {
                MasterGraphWindow.CloseIfEditing(formula);
                OpenFormulas(formula, false);
                return;
            }
            if (page != PreviewPage) page = 0;
            if (asset != graph)
            {
                selectedBuffUid = null;
                friendlyTrial = new TrialRole();
                enemyTrial = new TrialRole();
                trialOpening = null;
                previewDamageReport = null;
                measuredHits = null;
                measuredCaster = null;
                selectedTrialMark = null;
            }
            formulaGraph = asset;
            formulaUid = null;
            formulaFromBuff = false;
            sampleNumbers.Clear();
            MasterGraphWindow.CloseIfEditing(asset);
            graph = asset;
            if (asset) FocusLibraryRole(asset.libraryCharacter);
            motionBrowserController = null;
            if (executor && executor.mGraph != graph) { executor = null; dealer = null; target = null; }
            selection = null;
            cursor = 0;
            scroll = Vector2.zero;
            previewScroll = Vector2.zero;
            previewDirection = 0;
            demoSkillLevel = 1;
            formulaSampleLevel = 1;
            showLibrary = false;
            message = "";
            demoQueued = false;
            demoResult = null;
            lastCaster = lastReceiver = null;
            GUI.FocusControl(null);
        }

        private void OnProjectChange() { projectSkills = null; library = null; libraryCharacters = null; RefreshMotionTimings(); }
        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshMotionTimings;
            Undo.postprocessModifications += OnMotionAssetEdited;
            EditorApplication.update += UpdateDemoPreview;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnPreviewSceneLoaded;
        }
        private void OnDisable()
        {
            ReleasePreviewCamera();
            Undo.undoRedoPerformed -= RefreshMotionTimings;
            Undo.postprocessModifications -= OnMotionAssetEdited;
            EditorApplication.update -= UpdateDemoPreview;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnPreviewSceneLoaded;
            if (componentEditor) DestroyImmediate(componentEditor);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int nextPage = GUILayout.Toolbar(page, Pages, GUILayout.Height(26));
                if (page == 0 && nextPage == 1) selectedBuffUid = null;
                page = nextPage;
                if (GUILayout.Button("制作流程", GUILayout.Width(80), GUILayout.Height(26)))
                    SkillProductionGuideWindow.Open();
            }
            if (page == PreviewPage)
            {
                DrawPreviewPage();
                return;
            }
            if (page != 0)
            {
                pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
                if (page == 5) DrawRolePage();
                else if (page == 4) DrawAttributePage();
                else if (page == 1) DrawBuffPage();
                else DrawFormulaPage();
                EditorGUILayout.EndScrollView();
                return;
            }
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawSkillPicker();
            if (GUILayout.Button("新建技能", EditorStyles.toolbarButton))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("空白技能"), false, () => NewSkill("NewSkill", 0, false));
                menu.AddItem(new GUIContent("单次伤害"), false, () => NewSkill("SingleHit", 1, false));
                menu.AddItem(new GUIContent("三段伤害"), false, () => NewSkill("TripleHit", 3, false));
                menu.AddItem(new GUIContent("单体治疗"), false, () => NewSkill("SingleHeal", 1, true));
                var states = GameManager.OverTimeEffectData;
                if (states && states.overTimeEffects.Count > 0)
                    foreach (var effect in states.overTimeEffects)
                    {
                        string uid = effect.uid;
                        menu.AddItem(new GUIContent("目标有状态时按攻击自疗/" + effect.name + " · " + uid),
                            false, () => NewSkill("条件自疗", 0, true, uid));
                    }
                else menu.AddDisabledItem(new GUIContent("目标有状态时按攻击自疗（请先配置状态库）"));
                menu.ShowAsContext();
            }
            if (graph && GUILayout.Button("复制技能", EditorStyles.toolbarButton))
            {
                string path = EditorUtility.SaveFilePanelInProject("复制技能", graph.name + "_Copy", "asset", "选择副本位置");
                if (!string.IsNullOrEmpty(path))
                {
                    MasterGraphWindow.CloseIfEditing(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(graph), path)) SelectGraph(AssetDatabase.LoadAssetAtPath<GraphObject>(path));
                }
            }
            if (graph && GUILayout.Button("打开节点图", EditorStyles.toolbarButton)) MasterGraphWindow.OpenWindow(graph, selection);
            if (graph && GUILayout.Button("保存", EditorStyles.toolbarButton)) SaveSkillAndLevels(graph);
            if (graph is OriginalSkillGraph local && GUILayout.Button("导出 H5 战斗包", EditorStyles.toolbarButton))
            {
                SaveSkillAndLevels(graph);
                try { LocalBattleSkillExport.Export(local); }
                catch (Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("导出失败", error.Message, "确定"); }
            }
            if (GUILayout.Button("使用手册", EditorStyles.toolbarButton))
            {
                string manual = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/MCC技能工作台操作手册.html"));
                if (File.Exists(manual)) Application.OpenURL(new Uri(manual).AbsoluteUri);
                else ShowNotification(new GUIContent("手册位置：docs/MCC技能工作台操作手册.html"));
            }
            EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (showLibrary || !graph) DrawLibrary();
            else
            {
                if (MasterGraphWindow.IsEditing(graph))
                {
                    EditorGUILayout.HelpBox("该资产正在节点图窗口编辑。切回工作台会保存当前节点图。", MessageType.Info);
                    if (GUILayout.Button("切回工作台编辑")) MasterGraphWindow.CloseIfEditing(graph);
                }
                else if (graph.RootSet == null || graph.RootSet.Uid != "Node0")
                {
                    if (GUILayout.Button("初始化技能时间轴")) Change(() => graph.RootSet = EmptyRoot());
                }
                else
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(graph.name, EditorStyles.largeLabel);
                    if (!string.IsNullOrWhiteSpace(graph.GetSummary)) EditorGUILayout.HelpBox(graph.GetSummary, MessageType.None);
                    DrawSkillWorkspace(graph.RootSet);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSkillSettings(Root root)
        {
            foreach (var table in BoundLevelTables(graph)) DrawLevelTable(table);
            var owner = (SkillLibraryCharacter)SkillFieldHelp.ObjectField("所属角色", graph.libraryCharacter, typeof(SkillLibraryCharacter), false, "将技能归入该角色的技能库。选择角色后，可在对应阵营和角色名下找到这项技能。");
            if (owner != graph.libraryCharacter)
            {
                Change(() => graph.libraryCharacter = owner);
                FocusLibraryRole(owner);
            }
            if (owner && GUILayout.Button("编辑「" + owner.name + "」角色属性"))
            {
                FocusLibraryRole(owner);
                page = 5;
            }
            bool opponent = SkillFieldHelp.Popup("默认施放方",
                graph.previewAsOpponent ? 1 : 0, new[] { "主角", "怪物" }, "决定快速试放时由主角还是怪物出招。动作中的「施法者」和「本次目标」会按这次施放方向解析。") == 1;
            var limit = Mathf.Max(1, EditorGUILayout.IntField("本次施法目标上限", Mathf.Max(1, graph.castTargetLimit)));
            if (limit != Mathf.Max(1, graph.castTargetLimit)) Change(() => graph.castTargetLimit = limit);
            if (opponent != graph.previewAsOpponent) Change(() => graph.previewAsOpponent = opponent);
            var entries = root.Triggers.Where(t => root.inputPort[0].uid.Contains(t.Uid + "#0")).ToList();
            showConditions = EditorGUILayout.Foldout(showConditions, "技能入口条件   " + entries.Sum(t => t.inputPort[0].uid.Count) + " 项", true);
            if (showConditions) DrawEntryConditions(root, entries);
        }

        private void DrawSkillWorkspace(Root root)
        {
            var sources = ExecutionSources(root);
            bool wide = position.width >= 1180;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawTimeline(root);
                    if (!wide) DrawWorkspaceInspector(root, sources);
                }
                if (wide)
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * .40f)))
                        DrawWorkspaceInspector(root, sources);
            }
        }

        private void DrawWorkspaceInspector(Root root, ILookup<string, NodeBase> sources)
        {
            var selected = root.Actions.Find(x => x.Uid == selection);
            if (selected == null || IsSourceTime(selected))
            {
                EditorGUILayout.LabelField("技能属性与入口条件", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) DrawSkillSettings(root);
                return;
            }
            EditorGUILayout.LabelField("节点检查器", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) DrawAction(selected, sources[selected.Uid + "#0"]);
        }

        private void DrawSkillPicker()
        {
            if (GUILayout.Button("技能库", EditorStyles.toolbarButton, GUILayout.Width(62))) showLibrary = !showLibrary;
            var next = (GraphObject)EditorGUILayout.ObjectField(graph, typeof(GraphObject), false);
            SkillFieldHelp.Button("技能图", "选择要编辑或试放的技能图。也可以从技能库按角色查找。");
            if (next == graph) return;
            if (page == PreviewPage && next is SkillFormula) ShowNotification(new GUIContent("请选择一张技能图"));
            else SelectGraph(next);
        }

        private void FocusLibraryRole(SkillLibraryCharacter character)
        {
            selectedLibraryCharacter = character;
            libraryCamp = character ? (int)character.camp : 2;
            characterFilter = "";
        }

        private static int DrawSubTabs(string label, int selected, string[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(48));
                return GUILayout.Toolbar(selected, options, EditorStyles.miniButton,
                    GUILayout.Width(options.Length * 84), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static readonly string[] CampLabels = { "友方", "敌方", "未归属" };

        private SkillLibraryCharacter[] LibraryRoles => libraryCharacters ??= AssetDatabase.FindAssets("t:SkillLibraryCharacter", new[] { "Assets" })
            .Select(id => AssetDatabase.LoadAssetAtPath<SkillLibraryCharacter>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(c => c).OrderBy(c => c.name).ThenBy(c => AssetDatabase.GetAssetPath(c)).ToArray();

        private static readonly string[] RoleCamps = { "友方", "敌方" };

        private bool DrawRoleSelector(bool includeUnassigned)
        {
            int camp = DrawSubTabs("阵营", includeUnassigned ? libraryCamp : Mathf.Min(libraryCamp, 1), includeUnassigned ? CampLabels : RoleCamps);
            if (camp != libraryCamp)
            {
                libraryCamp = camp;
                selectedLibraryCharacter = null;
                characterFilter = "";
                filter = "";
            }
            if (camp < 2)
            {
                characterFilter = SkillFieldHelp.TextField("查找角色", characterFilter, "按输入文字筛选当前阵营的角色。清空后显示全部角色。");
                var roles = LibraryRoles.Where(c => c && (int)c.camp == camp &&
                    c.name.IndexOf(characterFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (roles.Length == 0)
                {
                    selectedLibraryCharacter = null;
                    EditorGUILayout.HelpBox(string.IsNullOrEmpty(characterFilter) ?
                        "此阵营还没有角色。可在 Project 中创建「Soft Kitty/技能库角色」。" : "没有匹配的角色。", MessageType.Info);
                    return false;
                }
                int index = Mathf.Max(0, Array.IndexOf(roles, selectedLibraryCharacter));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(16);
                    GUILayout.Label("角色", EditorStyles.miniLabel, GUILayout.Width(32));
                    var labels = roles.Select(c => new GUIContent(c.name, AssetDatabase.GetAssetPath(c))).ToArray();
                    float available = Mathf.Max(80, EditorGUIUtility.currentViewWidth - 90);
                    float width = Mathf.Min(available, Mathf.Max(80, labels.Max(c => EditorStyles.radioButton.CalcSize(c).x) + 12));
                    int columns = Mathf.Clamp((int)(available / width), 1, roles.Length);
                    index = GUILayout.SelectionGrid(index, labels, columns, EditorStyles.radioButton, GUILayout.Width(columns * width));
                }
                if (selectedLibraryCharacter != roles[index]) filter = "";
                selectedLibraryCharacter = roles[index];
            }

            return true;
        }

        private void DrawLibrary()
        {
            if (!DrawRoleSelector(true)) return;
            int camp = libraryCamp;
            filter = SkillFieldHelp.TextField("查找技能", filter, "按名称或编号筛选技能列表。清空后恢复当前分类下的完整列表。");
            if (library == null)
                library = AssetDatabase.FindAssets("t:OriginalSkillGraph", new[] { "Assets/WhiteFog/Skills/宗师之上" })
                    .Select(id => AssetDatabase.LoadAssetAtPath<OriginalSkillGraph>(AssetDatabase.GUIDToAssetPath(id)))
                    .Where(skill => skill && skill.sourceId > 0 && !string.IsNullOrEmpty(skill.sourceVersion))
                    .OrderBy(skill => skill.sourceId).ToArray();
            if (projectSkills == null)
                projectSkills = AssetDatabase.FindAssets("t:GraphObject", new[] { "Assets" })
                    .Select(id => AssetDatabase.LoadAssetAtPath<GraphObject>(AssetDatabase.GUIDToAssetPath(id)))
                    .Where(g => g && g.GetType() == typeof(GraphObject) && g.RootSet?.ClassName == "Combat")
                    .OrderBy(g => g.name).ToArray();

            var owner = camp == 2 ? null : selectedLibraryCharacter;
            bool any = false;
            foreach (var skill in library.Concat(projectSkills))
            {
                if (!skill || skill.libraryCharacter != owner) continue;
                string label = skill is OriginalSkillGraph original ? original.sourceId + "  " + skill.name : skill.name;
                if (label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                any = true;
                if (GUILayout.Button(new GUIContent(label, AssetDatabase.GetAssetPath(skill)), EditorStyles.miniButton))
                    SelectGraph(skill);
            }
            if (!any) EditorGUILayout.HelpBox("当前列表没有匹配的技能。", MessageType.Info);
        }

        private static Root EmptyRoot()
        {
            var root = new Root();
            root.Reset();
            return root;
        }

        private void NewSkill(string nameHint, int count, bool healing, string requiredStatus = null)
        {
            string path = EditorUtility.SaveFilePanelInProject("新建技能", nameHint, "asset", "选择新技能的位置");
            if (string.IsNullOrEmpty(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var asset = CreateInstance<GraphObject>();
            asset.libraryCharacter = selectedLibraryCharacter;
            asset.previewAsOpponent = selectedLibraryCharacter && selectedLibraryCharacter.camp == SkillLibraryCharacter.Camp.Enemy;
            asset.RootSet = requiredStatus == null ? CreateSkillRoot(count, healing) : CreateConditionalHeal(requiredStatus);
            AssetDatabase.CreateAsset(asset, path);
            SelectGraph(asset);
            projectSkills = null;
        }

        internal static Root CreateSkillRoot(int count, bool healing)
        {
            var root = EmptyRoot();
            if (count == 0) return root;
            AddCondition(root, "EntityExist");
            float amount = count > 1 ? 7f : 30f;
            var action = healing ? AddCappedHeal(root, .1f, amount) : AddAction(root, "AttributeChange", .1f);
            if (!healing)
            {
                action.StringValues[0] = "chp";
                action.IntValues[0] = (int)AttributeChangeAction.operations.Substract;
                root.Variables.Last().Constant = amount;
            }
            action.Type = count == 1 ? ActionType.Only_Once : ActionType.Multiple_Time;
            action.Count = count;
            action.Interval = .2f;
            return root;
        }

        private void Change(System.Action write)
        {
            Undo.RegisterCompleteObjectUndo(graph, "编辑技能");
            write();
            EditorUtility.SetDirty(graph);
            Repaint();
        }

        private static float Span(Action a)
        {
            if (a.Type == ActionType.For_X_Seconds) return a.KeepTime;
            if (a.Type == ActionType.Multiple_Time) return Mathf.Max(0, a.Count - 1) * Mathf.Max(.01f, a.Interval);
            return 0;
        }

        internal static bool IsSourceTime(Action node) =>
            node.ClassName == "OriginalSkillVisual" && node.IntValues.Count > 0 &&
            (node.IntValues[0] == 4 || node.IntValues[0] == 5);

        internal static string SourceTimeHelp(Action node)
        {
            string time = node.WaitTime.ToString("0.###") + " 秒";
            return node.IntValues[0] == 4
                ? "原作在施法后 " + time + " 记录收招。此处仅供对照原作时序，不会切回待机、结束施法或触发伤害。手动新建技能无需添加。"
                : "原作在施法后 " + time + " 记录位移时点。此处仅供对照原作时序。角色移动由实际的位移动作控制。手动新建技能无需添加。";
        }

        // MCC dispatches actions through the upstream node's output port.
        internal static ILookup<string, NodeBase> ExecutionSources(Root root)
        {
            var owners = new NodeBase[] { root }.Concat(root.Branchs).Concat(root.Actions);
            return (from owner in owners
                    from port in owner.outputPort
                    where port.Type == "RootOut" || port.Type == "BranchOutAction"
                    from link in port.uid
                    select new { link, owner }).ToLookup(edge => edge.link, edge => edge.owner);
        }

        internal static bool IsHitSource(NodeBase source, GraphObject skill) => source is Action action &&
            (action.ClassName == "SelectTarget" || action.ClassName == "RedirectProjectile" || action.EmitsSettledHit || SkillProjectile.IsLaunch(action) || SkillProjectile.IsImpactLaunch(action) || action.ClassName == "OriginalSkillVisual" && action.IntValues.FirstOrDefault() == 6 &&
             OriginalSkillVisualAction.EffectFor(action, skill as OriginalSkillGraph)?.triggerHit == true);

        internal static string HitSourceLabel(Action action) =>
            action.WaitTime.ToString("0.###") + "s " + (action.ClassName == "SelectTarget" || action.ClassName == "RedirectProjectile" || action.EmitsSettledHit ? ActionName(action) :
                SkillProjectile.IsLaunch(action) || SkillProjectile.IsImpactLaunch(action) ? "投射物" : "轨道特效") + " " + action.Uid;

        internal static string HitEventName(Action source)
        {
            if (source.ClassName == "SelectTarget") return "找到目标时";
            if (source.EmitsSettledHit) return "每段伤害判定命中时";
            if (source.ClassName == "RedirectProjectile" || SkillProjectile.IsLaunch(source) || SkillProjectile.IsImpactLaunch(source)) return "命中";
            return "轨道特效判定接触时";
        }

        internal static string HitEventSource(Action source) => source.Uid + " · " + HitEventName(source);

        internal static string HitActionLabel(Action action)
        {
            if (action.ClassName != "FormulaImpact") return ActionName(action) + " " + action.Uid;
            var calculation = action.calculation;
            string segment = calculation?.independentSegment == true ? "第" + calculation.logicIndex + "段" : "";
            return segment + "命中结算 " + action.Uid;
        }

        internal static string CalculationSource(Action action)
        {
            var c = action.calculation;
            if (c == null) return "未配置结算";
            if (c.EffectiveMode == SkillCalculation.Mode.NativeGraph) return c.nativeGraph ? c.nativeGraph.name : "未绑定伤害图";
            return c.ActiveFlow ? c.ActiveFlow.name : c.UsesLinkedAssets ? "节点内的公式" : "未绑定伤害流程";
        }

        private void DrawTimeline(Root root)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("施法时间轴", EditorStyles.boldLabel);
            if (GUILayout.Toggle(selection == root.Uid, "技能属性与入口条件", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                selection = root.Uid;
            bool add;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                add = GUILayout.Button("添加动作", GUILayout.Width(95));
            if (add)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("打断施法"), false, () => Change(() =>
                {
                    var interrupt = AddAction(root, "Event", cursor);
                    interrupt.StringValues[0] = Entity.InterruptCastEvent;
                    selection = interrupt.Uid;
                }));
                AddActionChoices(menu, (key, stage) => Change(() =>
                    selection = AddConfiguredAction(root, key, stage, cursor).Uid));
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            var rows = TimelineRows(root).ToList();
            if (selection != root.Uid && !rows.Any(row => row.action.Uid == selection))
                selection = rows.FirstOrDefault(row => !IsSourceTime(row.action)).action?.Uid ?? root.Uid;
            var timed = rows.Where(row => row.start == root).Select(row => row.action).Distinct().ToArray();
            var motions = timed.ToDictionary(a => a, MotionTimeline);
            float end = timed.Select(a => a.WaitTime + Span(a) + (motions[a]?.seconds ?? 0)).DefaultIfEmpty(0).Max();
            var trial = timelineTrialSkill == graph ? timelineTrial : null;
            var marks = trial?.Marks.ToLookup(m => (m.node, m.trigger));
            var redirectedHits = trial?.Marks.Where(m => !string.IsNullOrEmpty(m.via)).ToLookup(m => (m.via, m.viaTrigger));
            if (trial != null)
            {
                if (trial.Marks.Count > 0) end = Mathf.Max(end, trial.Marks.Max(m => m.seconds));
                DrawTrialLegend(trial.Marks.Count);
                if (!string.IsNullOrEmpty(selectedTrialMark)) EditorGUILayout.LabelField(selectedTrialMark, EditorStyles.wordWrappedMiniLabel);
            }
            float horizon = Mathf.Max(2, end + .4f);
            cursor = SkillFieldHelp.Slider("时间游标（秒）", cursor, 0, horizon, "所有轨道共用施法时间刻度。拖动标尺移动游标，点击轨道选择动作并定位时间。");
            var ruler = TimelineLane(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight));
            float step = TimelineTickStep(horizon, ruler.width);
            DrawTimelineRuler(ruler, horizon, step);

            foreach (var (action, start, branch, depth) in rows)
            {
                var key = (action.Uid, (start as Action)?.Uid);
                var rowMarks = marks?[key];
                if (action.ClassName == "RedirectProjectile" && rowMarks != null) rowMarks = rowMarks.Concat(redirectedHits[key]);
                bool observed = rowMarks != null && rowMarks.Any();
                var row = EditorGUILayout.GetControlRect(false, observed ? 43 : 24);
                var name = TimelineLabel(row);
                var fullLane = TimelineLane(row);
                var lane = fullLane;
                if (observed) lane.height = 20;
                float indent = Mathf.Min(depth, 4) * 12;
                name.xMin += indent;
                bool absolute = start == root;
                bool reference = IsSourceTime(action);
                var parent = start as Action;
                string origin = parent == null ? (absolute ? "施法开始" : "未连接到施法入口") :
                    parent.Uid + " · " + (IsHitSource(parent, graph) ? HitEventName(parent) : "动作输出");
                if (!string.IsNullOrEmpty(branch)) origin += " · " + branch;
                string delay = action.WaitTime > 0 ? $"+{action.WaitTime:0.###} s" : "立即";
                string detail = absolute ? origin : origin + "　" + delay;
                string label = absolute ? $"{action.WaitTime:0.###} s　{ActionName(action)} · {action.Uid}" :
                    (parent == null ? "" : "↳ ") + HitActionLabel(action);
                var timing = absolute ? motions[action] : null;
                if (timing != null) label += " · " + TimingLabel(timing);
                if (reference)
                {
                    label = $"原作参考　{action.WaitTime:0.###} s　" + (action.IntValues[0] == 5 ? "位移时点" : "收招时点");
                    string help = action.Uid + "　" + SourceTimeHelp(action);
                    GUI.Label(new Rect(name.x, name.y, name.width - 22, name.height), new GUIContent(label, help), EditorStyles.miniLabel);
                    SkillFieldHelp.Button(new Rect(name.xMax - 20, name.y, 20, name.height), label, help);
                }
                else
                {
                    if (!absolute) detail += TimelineEventDetail(root, action);
                    var selected = selection == action.Uid;
                    if (GUI.Toggle(name, selected, new GUIContent(label, detail), EditorStyles.miniButton) != selected)
                    {
                        selection = action.Uid;
                        if (absolute) cursor = action.WaitTime;
                    }
                }
                DrawTimelineGrid(lane, horizon, step);
                if (absolute)
                {
                    if (timing != null) DrawMotionBands(lane, horizon, action, timing);
                    else
                    {
                        Color color = reference ? Color.gray : selection == action.Uid ? new Color(.32f,.83f,.66f) : new Color(.35f,.55f,.63f);
                        EditorGUI.DrawRect(new Rect(lane.x + lane.width * action.WaitTime / horizon, lane.y + 2,
                            Mathf.Max(3, lane.width * Span(action) / horizon), lane.height - 4), color);
                    }
                }
                else GUI.Label(new Rect(lane.x + 4, lane.y, lane.width - 4, lane.height), new GUIContent(detail, detail), EditorStyles.miniLabel);
                if (observed)
                {
                    var band = Rect.MinMaxRect(fullLane.x, lane.yMax, fullLane.xMax, fullLane.yMax);
                    DrawTrialMarks(band, horizon, step, rowMarks, action.Uid);
                }
                DrawTimelineCursor(fullLane, horizon);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && fullLane.Contains(Event.current.mousePosition))
                {
                    if (!reference) selection = action.Uid;
                    cursor = Mathf.Clamp01((Event.current.mousePosition.x - lane.x) / lane.width) * horizon;
                    Event.current.Use();
                    Repaint();
                }
            }
            if (rows.Count == 0) EditorGUILayout.HelpBox("将游标移到起始时间，点击「添加动作」。", MessageType.Info);
        }

        private static string TimelineEventDetail(Root root, Action action)
        {
            if (action.ClassName == "FormulaImpact") return "　·　" + CalculationSource(action);
            if (action.ClassName == "SelectTarget" && action.selectionRules != null)
            {
                var rules = action.selectionRules;
                return "　·　AND 条件 " + rules.filters.Count + " 项　" + string.Join(" → ", rules.priorities.Select(p => p?.Label));
            }
            if (action.ClassName == "RedirectProjectile")
            {
                var binding = action.inputPort.ElementAtOrDefault(1)?.uid.FirstOrDefault();
                var target = root.Entities.Find(e => e.Uid + "#0" == binding);
                int role = target == null ? -1 : Array.IndexOf(EntityRoleValues, (int)target.Type);
                if (role >= 0) return "　·　飞向" + EntityRoleLabels[role];
            }
            return "";
        }

        private static IEnumerable<(Action action, NodeBase start, string branch, int depth)> TimelineRows(Root root)
        {
            var nodes = root.Actions.Cast<NodeBase>().Concat(root.Branchs).ToDictionary(n => n.Uid);
            // Native true/false branches preserve their incoming clock. An action output starts a new event clock.
            IEnumerable<(Action action, string branch)> Outputs(NodeBase source)
            {
                var visited = new HashSet<NodeBase>();
                var pending = new Stack<(NodeBase node, string path)>();
                pending.Push((source, ""));
                while (pending.Count > 0)
                {
                    var (node, path) = pending.Pop();
                    if (node is Action child && (node != source || visited.Contains(node))) { yield return (child, path); continue; }
                    if (!visited.Add(node)) continue;
                    for (int p = node.outputPort.Count - 1; p >= 0; p--)
                    {
                        var port = node.outputPort[p];
                        if (port.Type != "RootOut" && port.Type != "BranchOutAction") continue;
                        string via = node is Branch ? (path.Length > 0 ? path + " → " : "") + node.Uid + (p == 0 ? " 成功" : " 失败") : path;
                        for (int i = port.uid.Count - 1; i >= 0; i--)
                            if (nodes.TryGetValue(port.uid[i].Split('#')[0], out var target))
                                pending.Push((target, via));
                    }
                }
            }
            var cast = Outputs(root).ToLookup(link => link.action, link => link.branch);
            var scheduled = root.Actions.Where(a => cast.Contains(a) || IsSourceTime(a)).OrderBy(a => a.WaitTime).ToArray();
            var expanded = new HashSet<Action>();
            var stack = new Stack<(Action action, NodeBase start, string branch, int depth)>();
            foreach (var entry in scheduled.Concat(root.Actions.Except(scheduled)))
            {
                bool absolute = cast.Contains(entry) || IsSourceTime(entry);
                if (!absolute && expanded.Contains(entry)) continue;
                stack.Push((entry, absolute ? root : null, string.Join(" / ", cast[entry].Distinct()), 0));
                while (stack.Count > 0)
                {
                    var row = stack.Pop();
                    yield return row;
                    if (!expanded.Add(row.action)) continue;
                    foreach (var child in Outputs(row.action).Reverse())
                        stack.Push((child.action, row.action, child.branch, row.depth + 1));
                }
            }
        }

        internal static Action AddAction(Root root, string key, float time)
        {
            var action = new Action(root) { ClassName = key, WaitTime = time,
                Position = new Vector2(root.Position.x + LayoutColumnStep, OccupiedNodeRects(root).Max(r => r.yMax) + LayoutRowGap) };
            action.ResetInputPortSetting();
            root.Actions.Add(action);
            Wire(root, action, 0);
            ConfigureInputs(root, action, 1);
            if (key == "FormulaImpact")
            {
                action.calculation = new SkillCalculation();
                var level = root.Variables.Last();
                level.Type = VariableType.DynamicVariable;
                level.VariableUid = "skill.level";
                level.Constant = 1;
            }
            return action;
        }

        internal static Handler AddCondition(Root root, string key)
        {
            var trigger = new Trigger(root) { Position = new Vector2(-400, root.Triggers.Count * 300) };
            root.Triggers.Add(trigger);
            Wire(trigger, root, 0);
            return AddHandler(root, trigger, key);
        }

        private static Handler AddHandler(Root root, NodeBase owner, string key, int port = 0)
        {
            var existing = new HashSet<NodeBase>(LayoutNodes(root));
            var occupied = OccupiedNodeRects(root).ToList();
            var handler = new Handler(root) { ClassName = key, Position = owner.Position - Vector2.right * LayoutColumnStep };
            handler.ResetInputPortSetting();
            root.Handlers.Add(handler);
            Wire(handler, owner, port);
            ConfigureInputs(root, handler, 0);
            PlaceBelowObstacles(LayoutNodes(root).Where(n => !existing.Contains(n)).ToList(), occupied);
            return handler;
        }

        internal static Handler AddActionCondition(Root root, Action action, string key) =>
            AddHandler(root, action, key, action.EnsureExecutionConditionPort());

        private void ShowConditionMenu(Func<string, Handler> add)
        {
            var menu = new GenericMenu();
            void Relation(string label, TagCompareHandler.logics comparison)
            {
                menu.AddItem(new GUIContent("目标阵营/" + label), false, () => Change(() =>
                {
                    var h = add("TagCompare");
                    h.IntValues[1] = (int)comparison;
                }));
            }
            Relation("友方 · 与施法者同阵营（比较标签）", TagCompareHandler.logics.Same);
            Relation("与施法者异阵营（比较标签）", TagCompareHandler.logics.Different);
            foreach (var file in Directory.GetFiles(Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/Handler", "*Handler_interface.txt").OrderBy(x => x))
            {
                string key = Path.GetFileName(file).Replace("Handler_interface.txt", "");
                menu.AddItem(new GUIContent(GraphEditorText.Localize(key)), false, () => Change(() => add(key)));
            }
            menu.ShowAsContext();
        }

        private void DrawEntryConditions(Root root, List<Trigger> triggers)
        {
            if (GUILayout.Button("添加技能入口条件")) ShowConditionMenu(key => AddCondition(root, key));
            foreach (var trigger in triggers) DrawConditionLinks(root, trigger, 0);
        }

        private void DrawActionConditions(Action action)
        {
            var root = graph.RootSet;
            int port = action.ExecutionConditionPort;
            int count = port >= 0 ? action.inputPort[port].uid.Count : 0;
            EditorGUILayout.LabelField("动作前置条件 · " + count + " 项", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("每次执行前检查；全部满足才执行这一次。", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("添加此动作的前置条件")) ShowConditionMenu(key => AddActionCondition(root, action, key));
            if (port >= 0) DrawConditionLinks(root, action, port);
        }

        private void DrawConditionLinks(Root root, NodeBase owner, int port)
        {
            foreach (var link in owner.inputPort[port].uid.ToArray())
            {
                var condition = (Condition)root.Handlers.Find(h => link == h.Uid + "#0")
                    ?? root.ConditionGroups.Find(g => link == g.Uid + "#0");
                var handler = condition as Handler;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string name = handler != null ? GraphEditorText.Localize(handler.ClassName)
                            : condition is ConditionGroup group ? "条件组 · " + GraphEditorText.Localize(group.Type.ToString()) : "缺少条件节点";
                        EditorGUILayout.LabelField(name + " · " + (condition?.Uid ?? link), EditorStyles.boldLabel);
                        if (GUILayout.Button("删除此条件", GUILayout.Width(95)))
                        {
                            Change(() => RemoveConditionLink(root, owner, port, link));
                            return;
                        }
                    }
                    if (condition != null && condition.outputPort[0].uid.Count > 1)
                        EditorGUILayout.HelpBox("此条件被多处引用。改参数会影响所有引用，删除只断开此处。", MessageType.Info);
                    if (handler != null)
                    {
                        if (handler.ClassName == "TagCompare")
                            EditorGUILayout.HelpBox("两名实体须在相同标签序号填写阵营值。同值表示同阵营，异值表示不同阵营。", MessageType.None);
                        DrawFields(handler, 0);
                    }
                    else if (condition != null && GUILayout.Button("打开节点图编辑条件组")) MasterGraphWindow.OpenWindow(graph, condition.Uid);
                }
            }
        }

        internal static void RemoveConditionLink(Root root, NodeBase owner, int port, string link)
        {
            if (!owner.inputPort[port].uid.Remove(link)) return;
            var handler = root.Handlers.Find(h => link == h.Uid + "#0");
            Condition condition = (Condition)handler ?? root.ConditionGroups.Find(g => link == g.Uid + "#0");
            if (condition != null)
            {
                condition.outputPort[0].uid.Remove(owner.Uid + "#" + port);
                // Complex groups remain available on the canvas when their last use is disconnected.
                if (handler != null && handler.outputPort[0].uid.Count == 0)
                {
                    var inputs = handler.inputPort.SelectMany(p => p.uid).ToArray();
                    DisconnectNode(root, handler);
                    root.Handlers.Remove(handler);
                    foreach (var source in inputs)
                    {
                        NodeBase leaf = (NodeBase)root.Entities.Find(e => source == e.Uid + "#0") ?? root.Variables.Find(v => source == v.Uid + "#0");
                        if (leaf == null || leaf.outputPort.Any(p => p.uid.Count > 0) || leaf.inputPort.Any(p => p.uid.Count > 0)) continue;
                        DisconnectNode(root, leaf);
                        if (leaf is EntityClass e) root.Entities.Remove(e);
                        else root.Variables.Remove((VariableClass)leaf);
                    }
                }
            }
            if (owner is Trigger trigger && trigger.inputPort.All(p => p.uid.Count == 0))
            {
                DisconnectNode(root, trigger);
                root.Triggers.Remove(trigger);
            }
        }

        private static void ConfigureInputs(Root root, CustomNode action, int offset)
        {
            foreach (var field in action.CustomInterface.Elements)
            {
                int i = field.Index;
                Vector2 inputPosition = action.Position + new Vector2(-LayoutColumnStep, 144 + i * 240);
                switch (field.Type)
                {
                    case NodeInterfaceElementType.String_Variable: Fill(action.StringValues, i, ""); break;
                    case NodeInterfaceElementType.Float_Variable: Fill(action.FloatValues, i, 0f); break;
                    case NodeInterfaceElementType.Int_Variable:
                    case NodeInterfaceElementType.Enum: Fill(action.IntValues, i, 0); break;
                    case NodeInterfaceElementType.Bool_Variable: Fill(action.BoolValues, i, false); break;
                    case NodeInterfaceElementType.Input_Entity:
                        var entity = new EntityClass(root) { Type = (field.Uid == "entityA" || field.Uid == "caster") ? EntityType.Dealer : EntityType.Target, Position = inputPosition };
                        if (field.Uid == "redirectTarget") entity.Type = EntityType.NextCastTarget;
                        root.Entities.Add(entity);
                        Wire(entity, action, i + offset);
                        break;
                    case NodeInterfaceElementType.Input_Float:
                        var value = new VariableClass(root) { Position = inputPosition };
                        root.Variables.Add(value);
                        Wire(value, action, i + offset);
                        break;
                }
            }
        }

        private static void Fill<T>(List<T> list, int index, T value)
        {
            while (list.Count <= index) list.Add(value);
        }

        private static void Wire(NodeBase from, NodeBase to, int input)
        {
            from.outputPort[0].uid.Add(to.Uid + "#" + input);
            to.inputPort[input].uid.Add(from.Uid + "#0");
        }

        internal static string ActionName(Action action) =>
            SkillProjectile.IsLaunch(action) ? "产生投射物" :
            action.ClassName == "OriginalSkillVisual" ? VisualLabel(action.IntValues.ElementAtOrDefault(0)) :
            action.ClassName == "Event" && action.StringValues.Count > 0 && action.StringValues[0] == Entity.InterruptCastEvent
                ? "打断施法" : GraphEditorText.Localize(action.ClassName);

        private void DrawAction(Action action, IEnumerable<NodeBase> sources)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(ActionName(action) + " · " + action.Uid, EditorStyles.boldLabel);
            bool launch = SkillProjectile.IsLaunch(action);
            bool afterHit = false;
            foreach (var source in sources.Where(p => IsHitSource(p, graph)).Cast<Action>())
            {
                afterHit = true;
                if (GUILayout.Button("触发事件   " + HitEventSource(source) + "   ·   查看来源"))
                { selection = source.Uid; Repaint(); }
            }
            bool afterSelection = sources.OfType<Action>().Any(p => p.ClassName == "SelectTarget");
            if (afterSelection) EditorGUILayout.HelpBox("上游已经选出实体。「上游选出的目标」读取本次选择结果。「本次目标」仍指向触发上游的目标。", MessageType.Info);
            else if (afterHit) EditorGUILayout.HelpBox("此动作由上方列出的事件触发。「本次目标」取这次事件的实际受击者；延迟从事件发生时开始计时。", MessageType.Info);
            if (action.ClassName == "SelectTarget") DrawTargetSelection(action);
            if (IsHitSource(action, graph)) DrawHitActions(action);
            else if (action.ClassName == "FormulaImpact" && action.calculation?.EffectiveMode == SkillCalculation.Mode.NativeGraph &&
                     action.calculation.nativeGraph && GUILayout.Button("在原生伤害图中配置命中 Buff"))
            {
                Open(action.calculation.nativeGraph);
                GUIUtility.ExitGUI();
            }
            if (launch) DrawProjectile(action);
            DrawActionConditions(action);
            Edit(afterSelection ? "选定后延迟（秒）" : afterHit ? "命中后延迟（秒）" : "开始时间（秒）", action.WaitTime, x => action.WaitTime = x,
                afterHit ? "从上游确认命中开始计时。填 0 会在命中后立即执行本动作；填 0.2 会在命中后再等 0.2 秒。" : "从上游触发这个动作起，等待多少秒开始执行。直接连到技能根节点时，从施法开始计时。例如填 1.003，轨道特效在施法约 1.003 秒后出现。");
            var mode = (ActionType)SkillFieldHelp.Popup("执行方式", (int)action.Type, Modes, "选择本动作只执行一次、按指定次数重复，或在指定时长内按间隔重复。重复动作每次执行前都会检查自己的前置条件。");
            if (mode != action.Type) Change(() => action.Type = mode);
            if (mode == ActionType.Multiple_Time)
            {
                int count = Mathf.Max(1, SkillFieldHelp.DelayedIntField("执行次数", action.Count, "选择「执行多次」时，这个动作总共执行几次，包含第一次。"));
                if (count != action.Count) Change(() => action.Count = count);
            }
            if (mode == ActionType.For_X_Seconds) Edit("持续时间（秒）", action.KeepTime, x => action.KeepTime = x, "选择「持续执行」时，从动作开始算起，在这段时间内按执行间隔重复。");
            if (mode != ActionType.Only_Once) Edit("执行间隔（秒）", action.Interval, x => action.Interval = x, "重复执行时，相邻两次动作之间相隔的秒数。第一次执行的时间由开始时间或命中后延迟决定。");
            DrawFields(action, 1);
            if (SkillMotion.UsesBinding(action)) DrawMotion(action, graph);
            else if (!launch && action.ClassName == "OriginalSkillVisual") DrawVisualAction(action);
            else if ((action.ClassName == "SkillImpact" || action.ClassName == "FormulaImpact") &&
                     action.IntValues.ElementAtOrDefault(0) != (int)SkillPlayback.Style.Direct) DrawProjectile(action, true);
            // Older graphs keep their original interface until opened in the node editor.
            if (!action.CustomInterface.Elements.Any(f => f.Uid == "interruptOnMove"))
            {
                if (action.ClassName == "Teleport")
                    EditValue(action.BoolValues, TeleportAction.InterruptIndex, false, v => SkillFieldHelp.Toggle("位移时打断受术者施法", v, "让这次位移打断被移动角色正在施放的技能。已离手的投射物仍可继续飞行。"));
                else if (action.ClassName == "Transform" && action.BoolValues.ElementAtOrDefault(0))
                    EditValue(action.BoolValues, TransformAction.InterruptIndex, false, v => SkillFieldHelp.Toggle("位移时打断受术者施法", v, "让这次位移打断被移动角色正在施放的技能。已离手的投射物仍可继续飞行。"));
            }
            if (action.ClassName == "OverTimeEffect") DrawReferencedBuff(action);
            if (action.ClassName == "FormulaImpact") DrawAttackCalculation(action);
            if (GUILayout.Button("移除当前动作"))
                Change(() => { RemoveAction(graph.RootSet, action); selection = null; });
        }

        internal static void RemoveAction(Root root, Action action)
        {
            int port = action.ExecutionConditionPort;
            if (port >= 0)
                foreach (var link in action.inputPort[port].uid.ToArray()) RemoveConditionLink(root, action, port, link);
            DisconnectNode(root, action);
            root.Actions.Remove(action);
        }

        private static void DisconnectNode(Root root, NodeBase removed)
        {
            IEnumerable<NodeBase> nodes = new NodeBase[] { root }.Concat(root.Actions).Concat(root.Entities)
                .Concat(root.Variables).Concat(root.Maths).Concat(root.Triggers).Concat(root.Branchs)
                .Concat(root.ConditionGroups).Concat(root.Handlers);
            string prefix = removed.Uid + "#";
            foreach (var n in nodes)
                foreach (var port in n.inputPort.Concat(n.outputPort))
                    port.uid.RemoveAll(link => link.StartsWith(prefix, StringComparison.Ordinal));
            foreach (var group in root.Groups) group.Children.RemoveAll(id => id == removed.Uid);
        }

        private void DrawFields(CustomNode action, int offset)
        {
            foreach (var field in action.CustomInterface.Elements)
            {
                if (!Visible(action, field.VisibleBind)) continue;
                if (GraphEditorText.IsLegacyProjectileField(action, field)) continue;
                if (action.ClassName == "OriginalSkillVisual" && (field.Uid == "stage" || field.Uid == "cue")) continue;
                int i = field.Index;
                string label = GraphEditorText.Localize(field.Label);
                string help = GraphEditorText.Localize(field.ToolTips);
                if (action.ClassName == "OriginalSkillVisual" && field.Uid == "target")
                    help = "选择这次表现瞄准的实体。轨道特效挂在目标上或启用目标命中时，都会使用这里选择的角色。";
                if (action.ClassName == "Transform" && field.Uid == "worldSpace")
                    help = "开启后沿场景的固定方向移动。关闭后沿被移动角色自己的方向移动，例如 Z = -2、位移倍率为 1 时，角色会向自己身后退 2 米。";
                switch (field.Type)
                {
                    case NodeInterfaceElementType.Input_Float:
                        if (action is Action stateAction && stateAction.ClassName == "OverTimeEffect" &&
                            i + offset == 2 && DrawBuffChoice(stateAction)) break;
                        DrawBinding(action, i + offset, label, help);
                        break;
                    case NodeInterfaceElementType.Input_Entity:
                        DrawBinding(action, i + offset, label, help);
                        break;
                    case NodeInterfaceElementType.String_Variable:
                        EditValue(action.StringValues, i, "", v => SkillFieldHelp.DelayedTextField(label, v, help));
                        if (i == 0 && SkillAttributeEdits.UsesAttributeField(action) && GameManager.AttributeData &&
                            GameManager.AttributeData.IndexOfAttributes(action.StringValues.ElementAtOrDefault(i)) < 0)
                            EditorGUILayout.HelpBox("缺少属性 " + action.StringValues.ElementAtOrDefault(i), MessageType.Error);
                        if (field.Uid == "path")
                        {
                            if (action.ClassName == "Instantiate") DrawResourcesAsset<GameObject>(action, i, "Prefab", "选择 Resources 目录下要实例化的预制体。选择后会写入节点使用的资源路径。");
                            if (action.ClassName == "PlaySound") DrawResourcesAsset<AudioClip>(action, i, "Audio Clip", "选择 Resources 目录下要播放的音频片段。选择后会写入节点使用的资源路径。");
                        }
                        break;
                    case NodeInterfaceElementType.Float_Variable:
                        EditValue(action.FloatValues, i, 0f, v => SkillFieldHelp.DelayedFloatField(label, v, help)); break;
                    case NodeInterfaceElementType.Int_Variable:
                        EditValue(action.IntValues, i, 0, v =>
                        {
                            int next = SkillFieldHelp.DelayedIntField(label, v, help);
                            return action.ClassName == "TagCompare" && field.Uid == "index" ? Mathf.Max(0, next) : next;
                        }); break;
                    case NodeInterfaceElementType.Bool_Variable:
                        EditValue(action.BoolValues, i, false, v => SkillFieldHelp.Toggle(label, v, help)); break;
                    case NodeInterfaceElementType.Enum:
                        if (GraphEditorText.IsHitMode(action, field))
                            EditValue(action.IntValues, i, 0, value =>
                            {
                                int current = GraphEditorText.HitModeIndex(value);
                                int next = SkillFieldHelp.Popup(GraphEditorText.HitModeLabel, current,
                                    GraphEditorText.HitModeOptions, GraphEditorText.HitModeHelp);
                                return next == current ? value : GraphEditorText.HitModeValue(next);
                            });
                        else
                            EditValue(action.IntValues, i, 0, v => SkillFieldHelp.Popup(label, v, field.EnumOptions.Select(GraphEditorText.Localize).ToArray(), help));
                        break;
                }
            }
        }

        private void DrawResourcesAsset<T>(CustomNode node, int index, string label, string help) where T : UnityEngine.Object
        {
            string key = node.StringValues.ElementAtOrDefault(index);
            var current = string.IsNullOrEmpty(key) ? null : Resources.Load<T>(key);
            var picked = (T)SkillFieldHelp.ObjectField(label, current, typeof(T), false, help);
            if (picked == current) return;
            string path = AssetDatabase.GetAssetPath(picked);
            const string folder = "/Resources/";
            int start = path.LastIndexOf(folder, StringComparison.OrdinalIgnoreCase);
            if (picked && start < 0)
            {
                ShowNotification(new GUIContent("请选择 Resources 目录中的资源"));
                return;
            }
            string resourceKey = picked ? Path.ChangeExtension(path.Substring(start + folder.Length), null) : "";
            if (picked && Resources.Load<T>(resourceKey) != picked)
            {
                ShowNotification(new GUIContent("请检查 Resources 中的同名资源或子资源"));
                return;
            }
            Change(() => { Fill(node.StringValues, index, ""); node.StringValues[index] = resourceKey; });
        }

        internal static bool Visible(CustomNode node, string rule)
        {
            if (string.IsNullOrEmpty(rule)) return true;
            var parts = rule.Replace("!", "").Split('#');
            var control = node.CustomInterface.Elements.FirstOrDefault(x => x.Uid == parts[0]);
            if (control == null) return true;
            bool active = parts.Length > 1 && int.TryParse(parts[1], out var value)
                ? node.IntValues.ElementAtOrDefault(control.Index) == value
                : node.BoolValues.ElementAtOrDefault(control.Index);
            return rule.Contains("!") ? !active : active;
        }

        private void EditValue<T>(List<T> values, int index, T fallback, Func<T,T> draw)
        {
            T old = index < values.Count ? values[index] : fallback;
            T next = draw(old);
            if (!EqualityComparer<T>.Default.Equals(old, next)) Change(() => { Fill(values, index, fallback); values[index] = next; });
        }

        private void Edit(string label, float old, System.Action<float> set, string help)
        {
            float next = Mathf.Max(0, SkillFieldHelp.DelayedFloatField(label, old, help));
            if (old != next) Change(() => set(next));
        }

        private void DrawBinding(CustomNode a, int index, string label, string help)
        {
            string uid = a.inputPort.ElementAtOrDefault(index)?.uid.FirstOrDefault()?.Split('#')[0];
            var entity = graph.RootSet.Entities.Find(e => e.Uid == uid);
            var variable = graph.RootSet.Variables.Find(v => v.Uid == uid);
            var source = (NodeBase)entity ?? variable;
            if (source != null && source.outputPort[0].uid.Count > 1)
            {
                label += "（共享）";
                help += " 此输入节点被多处引用，修改后会影响所有引用它的动作或条件。";
            }
            if (entity != null)
            {
                var type = (EntityType)SkillFieldHelp.IntPopup(label, (int)entity.Type, EntityRoleLabels, EntityRoleValues, help);
                if (type != entity.Type) Change(() => entity.Type = type);
                if (type == EntityType.Specified)
                {
                    string id = SkillFieldHelp.DelayedTextField("实体 UID", entity.EntityUid, "填写 MCC 实体库中的唯一标识。选择「指定」实体时，运行时按这个标识查找对象。");
                    if (id != entity.EntityUid) Change(() => entity.EntityUid = id);
                }
            }
            else if (variable != null && (variable.Type == VariableType.Constant || variable.Type == VariableType.DynamicVariable))
            {
                string sourceLabel = variable.Type == VariableType.DynamicVariable ? " · " + variable.VariableUid + " 默认值"
                    : a.ClassName == "SkillImpact" && index == 3 ? " · 常量" : "";
                float num = SkillFieldHelp.DelayedFloatField(label + sourceLabel, variable.Constant, help);
                if (num != variable.Constant) Change(() => variable.Constant = num);
            }
            else if (graph.RootSet.Maths.Find(m => m.Uid == uid) is MathClass math)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, math.Name + " · " + math.Uid);
                    SkillFieldHelp.Button(label, help);
                    if (GUILayout.Button("查看公式", GUILayout.Width(80)))
                    {
                        OpenFormulas(graph, false);
                        formulaUid = math.Uid;
                    }
                }
            }
            else using (new SkillFieldHelp.Row(label, help)) EditorGUILayout.LabelField(label, uid == null ? "未连接 · 可在节点图接线" : "来自节点 " + uid + " · 在节点图查看公式");
        }

        private void DrawPlayTest()
        {
            DrawPreviewHitDamage();
            var picked = (CombatDamage)SkillFieldHelp.ObjectField("伤害执行组件", executor, typeof(CombatDamage), true, "选择当前场景里执行这张技能图的 CombatDamage 组件。留空时，试放会临时创建执行组件。");
            if (picked != executor) Bind(picked);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("查找使用此图的场景组件"))
            {
                var matches = FindObjectsOfType<CombatDamage>(true).Where(d => d.mGraph == graph).ToArray();
                var menu = new GenericMenu();
                foreach (var d in matches)
                    menu.AddItem(new GUIContent(d.gameObject.scene.name + "/" + AnimationUtility.CalculateTransformPath(d.transform, null) + " (" + d.GetInstanceID() + ")"),
                        d == executor, () => Bind(d));
                if (matches.Length == 0) menu.AddDisabledItem(new GUIContent("当前场景没有使用此图的组件"));
                menu.ShowAsContext();
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button("创建场景测试组件"))
                {
                    var obj = new GameObject(graph.name + " · 试放");
                    Undo.RegisterCreatedObjectUndo(obj, "创建技能测试组件");
                    var damage = Undo.AddComponent<CombatDamage>(obj);
                    damage.mGraph = graph;
                    damage.PhysicsDetection = false;
                    EditorUtility.SetDirty(damage);
                    Bind(damage);
                }
            EditorGUILayout.EndHorizontal();
            if (executor)
            {
                showExecutor = EditorGUILayout.Foldout(showExecutor, "命中检测、投射物与动态变量", true);
                if (showExecutor)
                {
                    Editor.CreateCachedEditor(executor, null, ref componentEditor);
                    using (new EditorGUI.IndentLevelScope()) componentEditor.OnInspectorGUI();
                }
            }
            EditorGUILayout.LabelField("试放对象", EditorStyles.boldLabel);
            dealer = (EntityComponent)SkillFieldHelp.ObjectField("施加者", dealer, typeof(EntityComponent), true, "把场景中实际出招的角色拖到这里。技能图中的施法者输入会引用这个实体。");
            target = (EntityComponent)SkillFieldHelp.ObjectField("目标", target, typeof(EntityComponent), true, "把场景中本次被瞄准的角色拖到这里。技能可根据自己的命中规则决定实际受击者。");
            if (UsesSkillLevel(graph)) demoSkillLevel = Mathf.Max(1, SkillFieldHelp.DelayedIntField("试放技能等级", demoSkillLevel, "选择这次试放传给技能图的等级。绑定等级表的技能会读取该等级对应的数值。"));
            for (int i = 0; i < Mathf.Max(1, graph.castTargetLimit) - 1; i++)
            {
                if (sceneExtraTargets.Count <= i) sceneExtraTargets.Add(null);
                sceneExtraTargets[i] = (EntityComponent)EditorGUILayout.ObjectField("后续目标 " + (i + 2), sceneExtraTargets[i], typeof(EntityComponent), true);
            }
            bool ready = Application.isPlaying && !EditorApplication.isPaused && dealer && target && (!executor || (executor.inited && executor.mGraph == graph));
            if (!ready) EditorGUILayout.HelpBox("进入 Play，把场景中具体的施放者和目标拖到对应槽。伤害执行组件可留空。", MessageType.None);
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("试放当前技能"))
                {
                    lastCaster = dealer;
                    lastReceiver = target;
                    healthBefore = target.mData != null ? target.GetAttributeFloat("chp") : 0;
                    CastPreviewActors(dealer, target, out message, executor);
                }
            DrawInterruptButton(dealer, ref message);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            if (Application.isPlaying && dealer && target && dealer.mData != null && target.mData != null)
            {
                if (lastCaster == dealer && lastReceiver == target)
                    EditorGUILayout.LabelField("目标生命", target.name + "  " + healthBefore.ToString("0.##") + " → " + target.GetAttributeFloat("chp").ToString("0.##"));
                DrawStageCamera(dealer, target);
            }
        }

        private void Bind(CombatDamage damage)
        {
            executor = damage;
            if (!damage) { dealer = null; target = null; return; }
            dealer = damage.DealerEntity ? damage.DealerEntity : damage.GetComponentInParent<EntityComponent>();
            target = damage.TargetEntity;
            Repaint();
        }
    }
}
