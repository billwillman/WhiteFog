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
        private bool showSourceTimes = true;
        private GraphObject[] projectSkills;
        private bool showConditions;
        private static readonly string[] EntityRoleLabels = { "施法者（自己）", "本次目标", "按 UID 指定实体", "高级：全局玩家", "高级：技能执行实例" };
        private static readonly int[] EntityRoleValues = { (int)EntityType.Dealer, (int)EntityType.Target, (int)EntityType.Specified, (int)EntityType.Player, (int)EntityType.Instance };
        [SerializeField] private CombatDamage executor;
        private Editor componentEditor;
        private bool showExecutor;
        [SerializeField] private EntityComponent dealer;
        [SerializeField] private EntityComponent target;
        private float cursor;
        private string message = "";
        private static readonly string[] Modes = { "单次", "执行多次", "持续执行" };
        private const int PreviewPage = 3;
        private static readonly string[] Pages = { "技能编辑", "Buff 状态", "公式与数值", "Preview" };

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
                previewDamageReport = null;
                measuredHits = null;
                measuredCaster = null;
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
            page = GUILayout.Toolbar(page, Pages, GUILayout.Height(26));
            if (page == PreviewPage)
            {
                DrawPreviewPage();
                return;
            }
            if (page != 0)
            {
                pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
                if (page == 1) DrawBuffPage();
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
                    var root = graph.RootSet;
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField(graph.name, EditorStyles.largeLabel);
                        if (!string.IsNullOrWhiteSpace(graph.GetSummary)) EditorGUILayout.HelpBox(graph.GetSummary, MessageType.None);
                        foreach (var table in BoundLevelTables(graph)) DrawLevelTable(table);
                        var owner = (SkillLibraryCharacter)EditorGUILayout.ObjectField("所属角色", graph.libraryCharacter, typeof(SkillLibraryCharacter), false);
                        if (owner != graph.libraryCharacter)
                        {
                            Change(() => graph.libraryCharacter = owner);
                            FocusLibraryRole(owner);
                        }
                        bool opponent = EditorGUILayout.Popup("默认施放方",
                            graph.previewAsOpponent ? 1 : 0, new[] { "主角", "怪物" }) == 1;
                        if (opponent != graph.previewAsOpponent) Change(() => graph.previewAsOpponent = opponent);
                        bool timed = EditorGUILayout.Toggle("施法输入时序", graph.useInputTiming);
                        if (timed != graph.useInputTiming) Change(() => graph.useInputTiming = timed);
                        if (timed)
                        {
                            Edit("允许移动（秒）", graph.moveAfterSeconds, value => graph.moveAfterSeconds = value);
                            Edit("允许接招（秒）", graph.nextSkillAfterSeconds, value => graph.nextSkillAfterSeconds = value);
                            EditorGUILayout.HelpBox("从施法成功算起，分别填写何时接受移动和下一招。例：移动 0.4、接招 0.7，在两者之间可以走动。填 0 表示这次施法立即放行该输入。打断会解除本次施法占用，其他控制状态仍生效。", MessageType.Info);
                        }
                        var entries = root.Triggers.Where(t => root.inputPort[0].uid.Contains(t.Uid + "#0")).ToList();
                        showConditions = EditorGUILayout.Foldout(showConditions, "技能入口条件   " + entries.Sum(t => t.inputPort[0].uid.Count) + " 项", true);
                        if (showConditions) DrawEntryConditions(root, entries);
                        var sources = ExecutionSources(root);
                        DrawTimeline(root, sources);
                        var action = root.Actions.Find(x => x.Uid == selection);
                        if (action != null && !IsSourceTime(action)) DrawAction(action, sources[action.Uid + "#0"]);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSkillPicker()
        {
            if (GUILayout.Button("技能库", EditorStyles.toolbarButton, GUILayout.Width(62))) showLibrary = !showLibrary;
            var next = (GraphObject)EditorGUILayout.ObjectField(graph, typeof(GraphObject), false);
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

        private void DrawLibrary()
        {
            int camp = DrawSubTabs("阵营", libraryCamp, CampLabels);
            if (camp != libraryCamp)
            {
                libraryCamp = camp;
                selectedLibraryCharacter = null;
                characterFilter = "";
                filter = "";
            }
            if (camp < 2)
            {
                if (libraryCharacters == null)
                    libraryCharacters = AssetDatabase.FindAssets("t:SkillLibraryCharacter", new[] { "Assets" })
                        .Select(id => AssetDatabase.LoadAssetAtPath<SkillLibraryCharacter>(AssetDatabase.GUIDToAssetPath(id)))
                        .Where(c => c).OrderBy(c => c.name).ThenBy(c => AssetDatabase.GetAssetPath(c)).ToArray();

                characterFilter = EditorGUILayout.TextField("查找角色", characterFilter);
                var roles = libraryCharacters.Where(c => c && (int)c.camp == camp &&
                    c.name.IndexOf(characterFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (roles.Length == 0)
                {
                    selectedLibraryCharacter = null;
                    EditorGUILayout.HelpBox(string.IsNullOrEmpty(characterFilter) ?
                        "此阵营还没有角色。可在 Project 中创建「Soft Kitty/技能库角色」。" : "没有匹配的角色。", MessageType.Info);
                    return;
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

            filter = EditorGUILayout.TextField("查找技能", filter);
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
            asset.useInputTiming = true;
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
            (SkillProjectile.IsLaunch(action) || action.ClassName == "OriginalSkillVisual" && action.IntValues.FirstOrDefault() == 6 &&
             OriginalSkillVisualAction.EffectFor(action, skill as OriginalSkillGraph)?.triggerHit == true);

        internal static string HitSourceLabel(Action action) =>
            action.WaitTime.ToString("0.###") + "s " + (SkillProjectile.IsLaunch(action) ? "投射物" : "轨道特效") + " " + action.Uid;

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

        // A settlement with one contact source belongs underneath it. Shared settlements keep a single row.
        private static ILookup<Action, Action> GroupHitSettlements(Action[] actions, ILookup<string, NodeBase> sources, GraphObject skill)
        {
            return actions.ToLookup(action =>
            {
                if (action.ClassName != "FormulaImpact") return null;
                var parents = sources[action.Uid + "#0"].Distinct().ToArray();
                return parents.Length == 1 && IsHitSource(parents[0], skill) ? (Action)parents[0] : null;
            });
        }

        private void DrawTimeline(Root root, ILookup<string, NodeBase> sources)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("动作时间轴", EditorStyles.boldLabel);
            if (GUILayout.Button("添加动作", GUILayout.Width(95)))
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
            var actions = root.Actions.Where(a => !IsSourceTime(a)).OrderBy(a => a.WaitTime).ToArray();
            var motions = actions.ToDictionary(a => a, MotionTimeline);
            float horizon = Mathf.Max(2, actions.Select(a => a.WaitTime + Span(a) + (motions[a]?.seconds ?? 0) + .4f).DefaultIfEmpty(2).Max());
            cursor = EditorGUILayout.Slider("时间游标（秒）", cursor, 0, horizon);
            var groups = GroupHitSettlements(actions, sources, graph);
            foreach (var parentRow in groups[null])
            foreach (var action in new[] { parentRow }.Concat(groups[parentRow]))
            {
                var row = EditorGUILayout.GetControlRect(false, 24);
                var name = new Rect(row.x, row.y, Mathf.Min(row.width * .49f, 285), row.height);
                bool nested = action != parentRow;
                if (nested) name.xMin += 16;
                var lane = Rect.MinMaxRect(name.xMax + 4, row.y + 4, row.xMax - 5, row.yMax - 4);
                var parents = sources[action.Uid + "#0"];
                bool onArrival = parents.Any(p => IsHitSource(p, graph));
                string route = string.Join("、", parents.Select(parent => parent == root ? "直接" :
                    IsHitSource(parent, graph) ? HitSourceLabel((Action)parent) + " 命中后" : "分支 " + parent.Uid));
                if (route.Length == 0) route = "未连接";
                var timing = motions[action];
                string label = (onArrival ? "+" : "") + action.WaitTime.ToString("0.00") + "s  " + (timing == null ? ActionName(action) : "角色动画") + "  ·  " + (timing == null ? route : TimingLabel(timing));
                if (IsHitSource(action, graph)) label = HitSourceLabel(action);
                if (onArrival && action.ClassName == "FormulaImpact") label = (nested ? "↳ " : "") + HitActionLabel(action);
                if (GUI.Toggle(name, action.Uid == selection, new GUIContent(label, action.Uid + " · " + route), EditorStyles.miniButton) && selection != action.Uid)
                { selection = action.Uid; cursor = parentRow.WaitTime; }
                if (onArrival && action.ClassName == "FormulaImpact")
                {
                    string delay = action.WaitTime > 0 ? "延迟 " + action.WaitTime.ToString("0.###") + " 秒" : "立即";
                    string detail = (nested ? "命中后" : route + " · ") + delay + "  ·  " + CalculationSource(action);
                    GUI.Label(lane, new GUIContent(detail, detail), EditorStyles.miniLabel);
                    continue;
                }
                EditorGUI.DrawRect(lane, new Color(.13f,.19f,.21f));
                var bar = new Rect(lane.x + lane.width * action.WaitTime / horizon, lane.y + 2,
                    Mathf.Max(5, lane.width * Span(action) / horizon), lane.height - 4);
                if (timing != null) DrawMotionBands(lane, horizon, action, timing);
                else EditorGUI.DrawRect(bar, selection == action.Uid ? new Color(.32f,.83f,.66f) : new Color(.35f,.55f,.63f));
                EditorGUI.DrawRect(new Rect(lane.x + cursor / horizon * lane.width, lane.y, 1, lane.height), Color.white);
                if (Event.current.type == EventType.MouseDown && lane.Contains(Event.current.mousePosition))
                { selection = action.Uid; cursor = Mathf.Clamp01((Event.current.mousePosition.x - lane.x) / lane.width) * horizon; Event.current.Use(); Repaint(); }
            }
            if (actions.Length == 0) EditorGUILayout.HelpBox("将游标移到起始时间，点击「添加动作」。", MessageType.Info);
            if (!actions.Any(x => x.Uid == selection)) selection = actions.FirstOrDefault()?.Uid;

            var sourceTimes = root.Actions.Where(IsSourceTime).OrderBy(a => a.WaitTime).ToArray();
            if (sourceTimes.Length == 0) return;
            showSourceTimes = EditorGUILayout.Foldout(showSourceTimes, "原作时间参考", true);
            if (showSourceTimes)
            {
                foreach (var mark in sourceTimes)
                {
                    EditorGUILayout.LabelField(ActionName(mark) + " · " + mark.Uid, EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(SourceTimeHelp(mark), MessageType.Info);
                }
            }
        }

        internal static Action AddAction(Root root, string key, float time)
        {
            var action = new Action(root) { ClassName = key, WaitTime = time, Position = new Vector2(450, root.Actions.Count * 420) };
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
            var handler = new Handler(root) { ClassName = key, Position = owner.Position + new Vector2(-350, owner.inputPort[port].uid.Count * 260) };
            handler.ResetInputPortSetting();
            root.Handlers.Add(handler);
            Wire(handler, owner, port);
            ConfigureInputs(root, handler, 0);
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
                    else if (condition != null && GUILayout.Button("打开节点图编辑条件组")) MasterGraphWindow.OpenWindow(graph);
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
                switch (field.Type)
                {
                    case NodeInterfaceElementType.String_Variable: Fill(action.StringValues, i, ""); break;
                    case NodeInterfaceElementType.Float_Variable: Fill(action.FloatValues, i, 0f); break;
                    case NodeInterfaceElementType.Int_Variable:
                    case NodeInterfaceElementType.Enum: Fill(action.IntValues, i, 0); break;
                    case NodeInterfaceElementType.Bool_Variable: Fill(action.BoolValues, i, false); break;
                    case NodeInterfaceElementType.Input_Entity:
                        var entity = new EntityClass(root) { Type = (field.Uid == "entityA" || field.Uid == "caster") ? EntityType.Dealer : EntityType.Target, Position = action.Position + new Vector2(-245, i * 130) };
                        root.Entities.Add(entity);
                        Wire(entity, action, i + offset);
                        break;
                    case NodeInterfaceElementType.Input_Float:
                        var value = new VariableClass(root) { Position = action.Position + new Vector2(-245, i * 130) };
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
            bool afterHit = false;
            foreach (var source in sources.Where(p => IsHitSource(p, graph)).Cast<Action>())
            {
                afterHit = true;
                if (GUILayout.Button("查看命中来源   " + HitSourceLabel(source)))
                { selection = source.Uid; cursor = source.WaitTime; Repaint(); }
            }
            if (afterHit) EditorGUILayout.HelpBox("从上游的「命中目标」端口触发。延迟填 0，碰到受击体时立即对本次命中的对象执行。", MessageType.Info);
            if (IsHitSource(action, graph)) DrawHitActions(action);
            DrawActionConditions(action);
            Edit(afterHit ? "命中后延迟（秒）" : "开始时间（秒）", action.WaitTime, x => action.WaitTime = x);
            var mode = (ActionType)EditorGUILayout.Popup("执行方式", (int)action.Type, Modes);
            if (mode != action.Type) Change(() => action.Type = mode);
            if (mode == ActionType.Multiple_Time)
            {
                int count = Mathf.Max(1, EditorGUILayout.DelayedIntField("执行次数", action.Count));
                if (count != action.Count) Change(() => action.Count = count);
            }
            if (mode == ActionType.For_X_Seconds) Edit("持续时间（秒）", action.KeepTime, x => action.KeepTime = x);
            if (mode != ActionType.Only_Once) Edit("执行间隔（秒）", action.Interval, x => action.Interval = x);
            DrawFields(action, 1);
            if (SkillProjectile.IsLaunch(action)) DrawProjectile(action);
            else if (SkillMotion.UsesBinding(action)) DrawMotion(action, graph);
            else if (action.ClassName == "OriginalSkillVisual") DrawVisualAction(action);
            else if ((action.ClassName == "SkillImpact" || action.ClassName == "FormulaImpact") &&
                     action.IntValues.ElementAtOrDefault(0) != (int)SkillPlayback.Style.Direct) DrawStrikeHitBox(action);
            // Older graphs keep their original interface until opened in the node editor.
            if (!action.CustomInterface.Elements.Any(f => f.Uid == "interruptOnMove"))
            {
                if (action.ClassName == "Teleport")
                    EditValue(action.BoolValues, TeleportAction.InterruptIndex, false, v => EditorGUILayout.Toggle("位移时打断受术者施法", v));
                else if (action.ClassName == "Transform" && action.BoolValues.ElementAtOrDefault(0))
                    EditValue(action.BoolValues, TransformAction.InterruptIndex, false, v => EditorGUILayout.Toggle("位移时打断受术者施法", v));
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
                if (action.ClassName == "OriginalSkillVisual" && (field.Uid == "stage" || field.Uid == "cue")) continue;
                int i = field.Index;
                string label = GraphEditorText.Localize(field.Label);
                switch (field.Type)
                {
                    case NodeInterfaceElementType.Input_Float:
                        if (action is Action stateAction && stateAction.ClassName == "OverTimeEffect" &&
                            i + offset == 2 && DrawBuffChoice(stateAction)) break;
                        DrawBinding(action, i + offset, label);
                        break;
                    case NodeInterfaceElementType.Input_Entity:
                        DrawBinding(action, i + offset, label);
                        break;
                    case NodeInterfaceElementType.String_Variable:
                        EditValue(action.StringValues, i, "", v => EditorGUILayout.DelayedTextField(label, v));
                        if (field.Uid == "path")
                        {
                            if (action.ClassName == "Instantiate") DrawResourcesAsset<GameObject>(action, i, "Prefab");
                            if (action.ClassName == "PlaySound") DrawResourcesAsset<AudioClip>(action, i, "Audio Clip");
                        }
                        break;
                    case NodeInterfaceElementType.Float_Variable:
                        EditValue(action.FloatValues, i, 0f, v => EditorGUILayout.DelayedFloatField(label, v)); break;
                    case NodeInterfaceElementType.Int_Variable:
                        EditValue(action.IntValues, i, 0, v =>
                        {
                            int next = EditorGUILayout.DelayedIntField(label, v);
                            return action.ClassName == "TagCompare" && field.Uid == "index" ? Mathf.Max(0, next) : next;
                        }); break;
                    case NodeInterfaceElementType.Bool_Variable:
                        EditValue(action.BoolValues, i, false, v => EditorGUILayout.Toggle(label, v)); break;
                    case NodeInterfaceElementType.Enum:
                        EditValue(action.IntValues, i, 0, v => EditorGUILayout.Popup(label, v, field.EnumOptions.Select(GraphEditorText.Localize).ToArray()));
                        break;
                }
            }
        }

        private void DrawResourcesAsset<T>(CustomNode node, int index, string label) where T : UnityEngine.Object
        {
            string key = node.StringValues.ElementAtOrDefault(index);
            var current = string.IsNullOrEmpty(key) ? null : Resources.Load<T>(key);
            var picked = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
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

        private void Edit(string label, float old, System.Action<float> set)
        {
            float next = Mathf.Max(0, EditorGUILayout.DelayedFloatField(label, old));
            if (old != next) Change(() => set(next));
        }

        private void DrawBinding(CustomNode a, int index, string label)
        {
            string uid = a.inputPort.ElementAtOrDefault(index)?.uid.FirstOrDefault()?.Split('#')[0];
            var entity = graph.RootSet.Entities.Find(e => e.Uid == uid);
            var variable = graph.RootSet.Variables.Find(v => v.Uid == uid);
            var source = (NodeBase)entity ?? variable;
            if (source != null && source.outputPort[0].uid.Count > 1) label += "（共享）";
            if (entity != null)
            {
                var type = (EntityType)EditorGUILayout.IntPopup(label, (int)entity.Type, EntityRoleLabels, EntityRoleValues);
                if (type != entity.Type) Change(() => entity.Type = type);
                if (type == EntityType.Specified)
                {
                    string id = EditorGUILayout.DelayedTextField("实体 UID", entity.EntityUid);
                    if (id != entity.EntityUid) Change(() => entity.EntityUid = id);
                }
            }
            else if (variable != null && (variable.Type == VariableType.Constant || variable.Type == VariableType.DynamicVariable))
            {
                string sourceLabel = variable.Type == VariableType.DynamicVariable ? " · " + variable.VariableUid + " 默认值"
                    : a.ClassName == "SkillImpact" && index == 3 ? " · 常量" : "";
                float num = EditorGUILayout.DelayedFloatField(label + sourceLabel, variable.Constant);
                if (num != variable.Constant) Change(() => variable.Constant = num);
            }
            else if (graph.RootSet.Maths.Find(m => m.Uid == uid) is MathClass math)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, math.Name + " · " + math.Uid);
                    if (GUILayout.Button("查看公式", GUILayout.Width(80)))
                    {
                        OpenFormulas(graph, false);
                        formulaUid = math.Uid;
                    }
                }
            }
            else EditorGUILayout.LabelField(label, uid == null ? "未连接 · 可在节点图接线" : "来自节点 " + uid + " · 在节点图查看公式");
        }

        private void DrawPlayTest()
        {
            DrawPreviewHitDamage();
            var picked = (CombatDamage)EditorGUILayout.ObjectField("伤害执行组件", executor, typeof(CombatDamage), true);
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
            dealer = (EntityComponent)EditorGUILayout.ObjectField("施加者", dealer, typeof(EntityComponent), true);
            target = (EntityComponent)EditorGUILayout.ObjectField("目标", target, typeof(EntityComponent), true);
            if (UsesSkillLevel(graph)) demoSkillLevel = Mathf.Max(1, EditorGUILayout.DelayedIntField("试放技能等级", demoSkillLevel));
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
