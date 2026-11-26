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
        private bool showConditions;
        private string conditionId;
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

        private void SelectGraph(GraphObject asset)
        {
            if (asset is SkillFormula formula)
            {
                MasterGraphWindow.CloseIfEditing(formula);
                OpenFormulas(formula, false);
                return;
            }
            if (page != PreviewPage) page = 0;
            formulaGraph = asset;
            formulaUid = null;
            formulaFromBuff = false;
            sampleNumbers.Clear();
            MasterGraphWindow.CloseIfEditing(asset);
            graph = asset;
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

        private void OnProjectChange() { library = null; Repaint(); }
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.update += UpdateDemoPreview;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnPreviewSceneLoaded;
        }
        private void OnDisable()
        {
            ReleasePreviewCamera();
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.update -= UpdateDemoPreview;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnPreviewSceneLoaded;
            if (componentEditor) DestroyImmediate(componentEditor);
        }

        private void OnGUI()
        {
            page = GUILayout.Toolbar(page, Pages);
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
            if (graph && GUILayout.Button("打开节点图", EditorStyles.toolbarButton)) MasterGraphWindow.OpenWindow(graph);
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
            if (graph)
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
                        bool opponent = EditorGUILayout.Popup("默认施放方",
                            graph.previewAsOpponent ? 1 : 0, new[] { "主角", "怪物" }) == 1;
                        if (opponent != graph.previewAsOpponent) Change(() => graph.previewAsOpponent = opponent);
                        showConditions = EditorGUILayout.Foldout(showConditions, "触发配置   " + root.Triggers.Count + " 个触发器 · " + root.Handlers.Count + " 个条件 · " + root.Branchs.Count + " 个分支", true);
                        if (showConditions) DrawConditions(root);
                        DrawTimeline(root);
                        var action = root.Actions.Find(x => x.Uid == selection);
                        if (action != null) DrawAction(action);
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

        private void DrawLibrary()
        {
            filter = EditorGUILayout.TextField("查找技能", filter);
            if (library == null)
                library = AssetDatabase.FindAssets("t:GraphObject").Select(x => AssetDatabase.LoadAssetAtPath<GraphObject>(AssetDatabase.GUIDToAssetPath(x))).Where(x => x && !(x is SkillFormula)).OrderBy(x => x.name).ToArray();
            foreach (var group in library.Where(s => s.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(s => s is OriginalSkillGraph ? "宗师之上 · 原作表现" : AssetDatabase.GetAssetPath(s).StartsWith("Assets/WhiteFog/Skills/", StringComparison.Ordinal)
                    ? (s.previewAsOpponent ? "怪物技能" : "主角技能") : "其他技能图")
                .OrderBy(g => g.Key == "宗师之上 · 原作表现" ? -1 : g.Key == "主角技能" ? 0 : g.Key == "怪物技能" ? 1 : 2))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                foreach (var skill in group)
                {
                    string path = AssetDatabase.GetAssetPath(skill);
                    if (GUILayout.Button(new GUIContent(group.Key == "其他技能图" ? path : skill.name, path), EditorStyles.miniButton)) SelectGraph(skill);
                }
            }
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
            asset.RootSet = requiredStatus == null ? CreateSkillRoot(count, healing) : CreateConditionalHeal(requiredStatus);
            AssetDatabase.CreateAsset(asset, path);
            SelectGraph(asset);
            library = null;
        }

        internal static Root CreateSkillRoot(int count, bool healing)
        {
            var root = EmptyRoot();
            if (count == 0) return root;
            AddCondition(root, "EntityExist");
            var action = AddAction(root, "AttributeChange", .1f);
            action.StringValues[0] = "chp";
            action.IntValues[0] = (int)(healing ? AttributeChangeAction.operations.Add : AttributeChangeAction.operations.Substract);
            action.Type = count == 1 ? ActionType.Only_Once : ActionType.Multiple_Time;
            action.Count = count;
            action.Interval = .2f;
            root.Variables.Last().Constant = count > 1 ? 7f : 30f;
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

        private void DrawTimeline(Root root)
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
                var folder = Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/Action";
                foreach (string file in Directory.GetFiles(folder, "*Action_interface.txt").OrderBy(x => x))
                {
                    var key = Path.GetFileName(file).Replace("Action_interface.txt", "");
                    menu.AddItem(new GUIContent(GraphEditorText.Localize(key)), false, () =>
                        Change(() => selection = AddAction(root, key, cursor).Uid));
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            float horizon = Mathf.Max(2, root.Actions.Select(x => x.WaitTime + Span(x) + .4f).DefaultIfEmpty(2).Max());
            cursor = EditorGUILayout.Slider("时间游标（秒）", cursor, 0, horizon);
            foreach (var action in root.Actions.OrderBy(x => x.WaitTime))
            {
                var row = EditorGUILayout.GetControlRect(false, 24);
                var name = new Rect(row.x, row.y, Mathf.Min(row.width * .49f, 285), row.height);
                var lane = Rect.MinMaxRect(name.xMax + 4, row.y + 4, row.xMax - 5, row.yMax - 4);
                var binding = action.inputPort[0].uid.FirstOrDefault();
                string route = binding == null ? "未连接" : binding == "Node0#0" ? "直接" : "分支";
                string label = action.WaitTime.ToString("0.00") + "s  " + ActionName(action) + "  ·  " + route;
                if (GUI.Toggle(name, action.Uid == selection, new GUIContent(label, action.Uid), EditorStyles.miniButton) && selection != action.Uid)
                { selection = action.Uid; cursor = action.WaitTime; }
                EditorGUI.DrawRect(lane, new Color(.13f,.19f,.21f));
                var bar = new Rect(lane.x + lane.width * action.WaitTime / horizon, lane.y + 2,
                    Mathf.Max(5, lane.width * Span(action) / horizon), lane.height - 4);
                EditorGUI.DrawRect(bar, selection == action.Uid ? new Color(.32f,.83f,.66f) : new Color(.35f,.55f,.63f));
                EditorGUI.DrawRect(new Rect(lane.x + cursor / horizon * lane.width, lane.y, 1, lane.height), Color.white);
                if (Event.current.type == EventType.MouseDown && lane.Contains(Event.current.mousePosition))
                { selection = action.Uid; cursor = Mathf.Clamp01((Event.current.mousePosition.x - lane.x) / lane.width) * horizon; Event.current.Use(); Repaint(); }
            }
            if (root.Actions.Count == 0) EditorGUILayout.HelpBox("将游标移到起始时间，点击「添加动作」。", MessageType.Info);
            else if (root.Actions.All(x => x.Uid != selection)) selection = root.Actions.OrderBy(x => x.WaitTime).First().Uid;
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
            var handler = new Handler(root) { ClassName = key, Position = trigger.Position + Vector2.left * 350 };
            handler.ResetInputPortSetting();
            root.Handlers.Add(handler);
            Wire(handler, trigger, 0);
            ConfigureInputs(root, handler, 0);
            return handler;
        }

        private void DrawConditions(Root root)
        {
            if (GUILayout.Button("添加必须满足的条件"))
            {
                var menu = new GenericMenu();
                foreach (var file in Directory.GetFiles(Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/Handler", "*Handler_interface.txt").OrderBy(x => x))
                {
                    string key = Path.GetFileName(file).Replace("Handler_interface.txt", "");
                    menu.AddItem(new GUIContent(GraphEditorText.Localize(key)), false, () => Change(() => conditionId = AddCondition(root, key).Uid));
                }
                menu.ShowAsContext();
            }
            if (root.Handlers.Count == 0) return;
            int idx = Mathf.Max(0, root.Handlers.FindIndex(x => x.Uid == conditionId));
            idx = EditorGUILayout.Popup("选择条件", idx, root.Handlers.Select(h => GraphEditorText.Localize(h.ClassName) + " · " + h.Uid).ToArray());
            conditionId = root.Handlers[idx].Uid;
            DrawFields(root.Handlers[idx], 0);
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
                        var entity = new EntityClass(root) { Type = EntityType.Target, Position = action.Position + new Vector2(-245, i * 130) };
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

        private static string ActionName(Action action) =>
            action.ClassName == "Event" && action.StringValues.Count > 0 && action.StringValues[0] == Entity.InterruptCastEvent
                ? "打断施法" : GraphEditorText.Localize(action.ClassName);

        private void DrawAction(Action action)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(ActionName(action) + " · " + action.Uid, EditorStyles.boldLabel);
            Edit("开始时间（秒）", action.WaitTime, x => action.WaitTime = x);
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
            if (action.ClassName == "OverTimeEffect") DrawReferencedBuff(action);
            if (action.ClassName == "FormulaImpact") DrawAttackCalculation(action);
            if (GUILayout.Button("移除当前动作"))
                Change(() => { RemoveAction(graph.RootSet, action); selection = null; });
        }

        internal static void RemoveAction(Root root, Action action)
        {
            IEnumerable<NodeBase> nodes = new NodeBase[] { root }.Concat(root.Actions).Concat(root.Entities)
                .Concat(root.Variables).Concat(root.Maths).Concat(root.Triggers).Concat(root.Branchs)
                .Concat(root.ConditionGroups).Concat(root.Handlers);
            string prefix = action.Uid + "#";
            foreach (var n in nodes)
                foreach (var port in n.inputPort.Concat(n.outputPort))
                    port.uid.RemoveAll(link => link.StartsWith(prefix, StringComparison.Ordinal));
            foreach (var group in root.Groups) group.Children.RemoveAll(id => id == action.Uid);
            root.Actions.Remove(action);
        }

        private void DrawFields(CustomNode action, int offset)
        {
            foreach (var field in action.CustomInterface.Elements)
            {
                if (!Visible(action, field.VisibleBind)) continue;
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
                        EditValue(action.StringValues, i, "", v => EditorGUILayout.DelayedTextField(label, v)); break;
                    case NodeInterfaceElementType.Float_Variable:
                        EditValue(action.FloatValues, i, 0f, v => EditorGUILayout.DelayedFloatField(label, v)); break;
                    case NodeInterfaceElementType.Int_Variable:
                        EditValue(action.IntValues, i, 0, v => EditorGUILayout.DelayedIntField(label, v)); break;
                    case NodeInterfaceElementType.Bool_Variable:
                        EditValue(action.BoolValues, i, false, v => EditorGUILayout.Toggle(label, v)); break;
                    case NodeInterfaceElementType.Enum:
                        EditValue(action.IntValues, i, 0, v => EditorGUILayout.Popup(label, v, field.EnumOptions.Select(GraphEditorText.Localize).ToArray())); break;
                }
            }
        }

        private static bool Visible(CustomNode node, string rule)
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
                var type = (EntityType)EditorGUILayout.Popup(label, (int)entity.Type, Enum.GetNames(typeof(EntityType)).Select(GraphEditorText.Localize).ToArray());
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
                    CastActors(graph, dealer, target, demoSkillLevel, out message, executor);
                }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        }

        private void Bind(CombatDamage damage)
        {
            executor = damage;
            if (!damage) { dealer = null; target = null; return; }
            dealer = damage.DealerEntity ? damage.DealerEntity : damage.GetComponentInParent<EntityComponent>();
            DrawInterruptButton(dealer, ref message);
            target = damage.TargetEntity;
            if (Application.isPlaying && dealer && target && dealer.mData != null && target.mData != null)
            {
                if (lastCaster == dealer && lastReceiver == target)
                    EditorGUILayout.LabelField("目标生命", target.name + "  " + healthBefore.ToString("0.##") + " → " + target.GetAttributeFloat("chp").ToString("0.##"));
                DrawStageCamera(dealer, target);
            }
            Repaint();
        }
    }
}
