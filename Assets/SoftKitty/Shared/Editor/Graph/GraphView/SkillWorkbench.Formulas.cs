using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        [SerializeField] private GraphObject formulaGraph;
        [SerializeField] private string formulaUid;
        [SerializeField] private bool formulaFromBuff;
        [SerializeField] private string formulaActionUid;
        private readonly Dictionary<string, float> sampleNumbers = new Dictionary<string, float>();

        internal static Root CreateConditionalHeal(string requiredStatus)
        {
            var root = EmptyRoot();
            root.Position = new Vector2(280, -410);
            var condition = AddCondition(root, "OverTimeEffect");
            condition.StringValues[0] = requiredStatus;
            root.Entities[0].Position = new Vector2(-1100, -720);
            condition.Position = new Vector2(-760, -720);
            root.Triggers[0].Position = new Vector2(-340, -670);
            AddCappedHeal(root, 0, 1.2f, true);
            return root;
        }

        // Author a normal MCC expression. All inputs and conditions remain editable graph nodes.
        private static Action AddCappedHeal(Root root, float time, float amount, bool scaleByAttack = false)
        {
            var heal = AddAction(root, "AttributeChange", time);
            heal.StringValues[0] = "chp";
            heal.IntValues[0] = (int)AttributeChangeAction.operations.Override;
            Vector2 origin = heal.Position;
            var recipient = root.Entities.Last();
            var value = root.Variables.Last();
            recipient.Position = origin + new Vector2(-1520, 140);
            value.Position = origin + new Vector2(-1180, 500);
            value.Constant = amount;
            value.outputPort[0].uid.Remove(heal.Uid + "#2");
            heal.inputPort[2].uid.Clear();

            var guard = AddActionCondition(root, heal, "EntityExist");
            guard.IntValues[0] = (int)EntityExistHandler.logics.Interactable;
            guard.Position = origin + new Vector2(-320, -370);
            var guardEntity = root.Entities.Last();
            DisconnectNode(root, guardEntity);
            root.Entities.Remove(guardEntity);
            Wire(recipient, guard, 0);

            VariableClass Attribute(string key, int y)
            {
                var node = new VariableClass(root) { Type = VariableType.Attribute, AttributeKey = key,
                    Position = origin + new Vector2(-1180, y) };
                root.Variables.Add(node);
                Wire(recipient, node, 0);
                return node;
            }
            MathClass Combine(MathType type, NodeBase a, NodeBase b, int x, int y)
            {
                var node = new MathClass(root) { Type = type, Position = origin + new Vector2(x, y) };
                root.Maths.Add(node);
                Wire(a, node, 0);
                Wire(b, node, 1);
                return node;
            }

            NodeBase gain = value;
            if (scaleByAttack)
            {
                recipient.Type = EntityType.Dealer;
                value.Type = VariableType.DynamicVariable;
                value.VariableUid = "healRatio";
                gain = Combine(MathType.Multiply, Attribute("atk", 270), value, -950, 350);
            }
            var zero = new VariableClass(root) { Position = origin + new Vector2(-1180, 740) };
            root.Variables.Add(zero);
            var positive = Combine(MathType.Max, gain, zero, -720, 220);
            var total = Combine(MathType.Add, Attribute("chp", 0), positive, -470, 110);
            Wire(Combine(MathType.Min, Attribute("hp", -240), total, -240, 0), heal, 2);
            return heal;
        }

        private void OpenFormulas(GraphObject source, bool fromBuff)
        {
            formulaGraph = source;
            formulaFromBuff = fromBuff;
            formulaUid = source is SkillFormula formula ? formula.outputUid : null;
            sampleNumbers.Clear();
            page = 2;
        }

        private void DrawFormulaPage()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (graph && GUILayout.Button("使用当前技能图")) OpenFormulas(graph, false);
                var db = GameManager.OverTimeEffectData;
                var effect = db ? db.overTimeEffects.Find(e => e.uid == buffUid) : null;
                if (effect != null && effect.DesignGraph is GraphObject asset && GUILayout.Button("使用选中 Buff 的效果图"))
                    OpenFormulas(asset, true);
            }
            var source = (GraphObject)EditorGUILayout.ObjectField("公式所在图", formulaGraph, typeof(GraphObject), false);
            if (source != formulaGraph) OpenFormulas(source, false);
            if (!source || source.RootSet == null)
            {
                if (source is SkillFormula && !EditorApplication.isPlaying && GUILayout.Button("初始化公式节点树"))
                {
                    Undo.RecordObject(source, "初始化公式图");
                    source.RootSet = EmptyRoot();
                    EditorUtility.SetDirty(source);
                }
                EditorGUILayout.HelpBox("选择一张原生技能图或状态效果图，查看数值来源并试算。", MessageType.Info);
                return;
            }
            if (MasterGraphWindow.IsEditing(source))
            {
                if (GUILayout.Button("保存节点树并返回试算")) MasterGraphWindow.CloseIfEditing(source);
                return;
            }
            var root = source.RootSet;
            if (source is OriginalSkillGraph original) DrawOriginalDamageReference(original);
            bool showsDamage = DrawDamageSources(source);
            var nodes = root.Maths.Cast<NodeBase>().Concat(root.Variables).ToList();
            if (nodes.Count == 0)
            {
                if (!showsDamage) EditorGUILayout.HelpBox("图中没有变量或数学节点。", MessageType.Info);
            }
            else
            {
                int index = Mathf.Max(0, nodes.FindIndex(n => n.Uid == formulaUid));
                index = EditorGUILayout.Popup("试算输出", index, nodes.Select(n => NumericLabel(root, n)).ToArray());
                var result = nodes[index];
                formulaUid = result.Uid;
                if (source is SkillFormula formula)
                {
                    EditorGUILayout.LabelField("运行时公式输出", formula.outputUid);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || formula.outputUid == result.Uid))
                        if (GUILayout.Button("把当前节点设为运行时输出"))
                        {
                            Undo.RecordObject(formula, "选择公式输出");
                            formula.outputUid = result.Uid;
                            formula.Invalidate();
                            EditorUtility.SetDirty(formula);
                        }
                }
                EditorGUILayout.LabelField("输入树", EditorStyles.boldLabel);
                var inputs = new Dictionary<string, float>();
                var db = GameManager.OverTimeEffectData;
                var effect = formulaFromBuff && db ? db.overTimeEffects.Find(e => e.uid == buffUid && e.DesignGraph == source) : null;
                try
                {
                    var drawn = new HashSet<NodeBase>();
                    DrawExpression(root, result, inputs, new HashSet<NodeBase>(), drawn, effect);
                    float value = EvaluateFormula(root, result, inputs);
                    EditorGUILayout.LabelField("当前试算结果", value.ToString("G7"), EditorStyles.boldLabel);
                }
                catch (InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Warning); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重置试算输入")) sampleNumbers.Clear();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("用原生节点树编辑连线")) MasterGraphWindow.OpenWindow(source);
                if (GUILayout.Button("保存图资产")) SaveSkillAndLevels(source);
            }
        }

        private bool DrawDamageSources(GraphObject source)
        {
            bool shown = false, usesPreviewDamage = false;
            var actions = source.RootSet.Actions;
            bool hasCallback = source is OriginalSkillGraph || actions.Any(SkillProjectile.IsLaunch);
            var configured = actions.Find(a => a.ClassName == "FormulaImpact" && a.Uid == formulaActionUid)
                ?? actions.Find(a => a.ClassName == "FormulaImpact");
            foreach (var action in actions)
            {
                string origin;
                bool canConnect = false;
                if (SkillProjectile.IsImpactLaunch(action))
                {
                    origin = "投射物 Snap · " + (action.projectileSnap ? action.projectileSnap.name : "未绑定") +
                        " → " + (action.ClassName == "FormulaImpact" ? CalculationSource(action) : "伤害输入");
                }
                else if (IsHitSource(action, source) && !action.EmitsSettledHit)
                {
                    var links = action.outputPort.FirstOrDefault()?.uid;
                    if (links != null && links.Count > 0)
                        origin = "命中后执行 " + string.Join("、", links.Select(link =>
                        {
                            string uid = link.Split('#')[0];
                            var target = actions.Find(a => a.Uid == uid);
                            return target == null ? uid : HitActionLabel(target);
                        }));
                    else
                    {
                        origin = "命中口未接线" + (hasCallback ? "，试放时使用每段预览来伤" : "");
                        usesPreviewDamage |= hasCallback;
                        canConnect = true;
                    }
                }
                else if (action.ClassName == "FormulaImpact")
                {
                    var c = action.calculation;
                    if (c != null && c.EffectiveMode == SkillCalculation.Mode.NativeGraph)
                        origin = "MCC 伤害图 " + (c.nativeGraph ? c.nativeGraph.name : "未绑定");
                    else if (c != null && c.ActiveFlow)
                        origin = "技能伤害流程 " + c.ActiveFlow.name +
                            (c.independentSegment ? " · 逻辑段 " + c.logicIndex + (c.segmentRatioVerified ? " · 倍率已核实" : " · 倍率待核") : "");
                    else if (c != null && c.UsesLinkedAssets)
                        origin = "技能伤害流程 · 沿用本招的公式引用";
                    else origin = "技能伤害流程未绑定";
                }
                else continue;

                if (!shown) EditorGUILayout.LabelField("命中与伤害来源", EditorStyles.boldLabel);
                shown = true;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(ActionName(action) + " · " + action.Uid + "　" + origin, EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    {
                        if (canConnect && GUILayout.Button("接入伤害流程", GUILayout.Width(100)))
                        {
                            if (graph != source) { SelectGraph(source); OpenFormulas(source, false); }
                            Change(() => formulaActionUid = AddConfiguredAction(source.RootSet, "FormulaImpact", 0, 0, action).Uid);
                            GUIUtility.ExitGUI();
                        }
                    }
                    if (action.ClassName == "FormulaImpact" && (configured != action || graph != source) &&
                        GUILayout.Button("编辑流程", GUILayout.Width(78)))
                    {
                        if (graph != source) { SelectGraph(source); OpenFormulas(source, false); }
                        formulaActionUid = action.Uid;
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("定位动作", GUILayout.Width(78))) OpenAction(source, action.Uid);
                }
            }
            if (configured != null && graph == source)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("伤害流程 · " + configured.Uid, EditorStyles.boldLabel);
                    DrawAttackCalculation(configured);
                    if (configured.calculation?.levels) DrawLevelTable(configured.calculation.levels);
                }
            }
            if (!shown && source == graph && !(source is SkillFormula) && !formulaFromBuff)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("添加直接结算的伤害动作"))
                    {
                        Change(() => formulaActionUid = AddConfiguredAction(source.RootSet, "FormulaImpact", 0, 0).Uid);
                        GUIUtility.ExitGUI();
                    }
            }
            if (usesPreviewDamage)
            {
                DrawPreviewDamageInput();
                EditorGUILayout.HelpBox("工作台用此值作为一次命中的来伤。有效易伤先放大并四舍五入，护盾吸收后再扣生命，最多扣到零。飘字显示实际生命损失。", MessageType.Info);
            }
            return shown;
        }

        private static string NumericLabel(Root root, NodeBase node)
        {
            var variable = node as VariableClass;
            string type = variable != null ? GraphEditorText.Localize(variable.Type.ToString())
                : GraphEditorText.Localize(((MathClass)node).Type.ToString());
            if (variable != null && variable.Type == VariableType.Attribute) type += " · " + InputKey(root, variable);
            if (variable != null && variable.Type == VariableType.DynamicVariable) type += " · " + variable.VariableUid;
            string name = node.Name != "Math" && node.Name != "Variable" && !string.IsNullOrEmpty(node.Name) ? node.Name + " · " : "";
            return node.Uid + "  " + name + type;
        }

        private static string InputKey(Root root, VariableClass variable)
        {
            if (variable.Type == VariableType.DynamicVariable) return "参数/" + variable.VariableUid;
            if (variable.Type != VariableType.Attribute) return variable.Uid;
            string id = variable.inputPort[0].uid.FirstOrDefault();
            var owner = root.Entities.Find(e => id == e.Uid + "#0");
            string entity = id == null ? "玩家" : owner == null ? id :
                owner.Type == EntityType.Specified ? owner.EntityUid : GraphEditorText.Localize(owner.Type.ToString());
            return entity + "/" + variable.AttributeKey;
        }

        private static NodeBase NumericInput(Root root, MathClass math, int port)
        {
            string link = math.inputPort.ElementAtOrDefault(port)?.uid.FirstOrDefault();
            NodeBase input = root.Variables.Find(v => link == v.Uid + "#0");
            if (input == null) input = root.Maths.Find(m => link == m.Uid + "#0");
            if (input == null) throw new InvalidOperationException(math.Uid + " 的输入 " + (char)('A' + port)
                + (link == null ? " 尚未接线。" : " 来自分支或其他节点，请在原生节点图中查看。"));
            return input;
        }

        private static int Arity(MathClass math) => math.ValueC_Visible() ? 3 : math.ValueB_Visible() ? 2 : 1;

        private void DrawExpression(Root root, NodeBase node, Dictionary<string, float> inputs,
            HashSet<NodeBase> active, HashSet<NodeBase> drawn, OverTimeEffect effect)
        {
            if (active.Contains(node)) throw new InvalidOperationException(node.Uid + " 的输入形成循环。");
            if (!drawn.Add(node)) { EditorGUILayout.LabelField("共用 " + node.Uid); return; }
            EditorGUILayout.LabelField(NumericLabel(root, node));
            if (node is MathClass math)
            {
                active.Add(node);
                using (new EditorGUI.IndentLevelScope())
                    for (int i = 0; i < Arity(math); i++)
                        DrawExpression(root, NumericInput(root, math, i), inputs, active, drawn, effect);
                active.Remove(node);
                return;
            }
            var variable = (VariableClass)node;
            if (variable.Type == VariableType.Constant || variable.Type == VariableType.DynamicVariable)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    float saved = EditorGUILayout.DelayedFloatField("图内默认值", variable.Constant);
                    if (saved != variable.Constant)
                    {
                        Undo.RegisterCompleteObjectUndo(formulaGraph, "修改公式数值");
                        variable.Constant = saved;
                        if (formulaGraph is SkillFormula editedFormula) editedFormula.Invalidate();
                        EditorUtility.SetDirty(formulaGraph);
                    }
                }
            }
            if (variable.Type == VariableType.Constant) return;
            string key = InputKey(root, variable);
            if (inputs.ContainsKey(key)) return;
            var parameter = effect?.DynamicVariables.Find(v => v.uid == variable.VariableUid);
            float fallback = variable.Type == VariableType.Attribute ? 0 : variable.Constant;
            if (variable.Type == VariableType.DynamicVariable && parameter != null) fallback = parameter.value;
            if (variable.Type == VariableType.DynamicVariable && variable.VariableUid == "Layer") fallback = 1;
            float sample = sampleNumbers.TryGetValue(key, out var existing) ? existing : fallback;
            float next = EditorGUILayout.DelayedFloatField("试算输入 · " + key, sample);
            if (next != sample) sampleNumbers[key] = next;
            inputs[key] = next;
        }

        // Only compile the selected pure expression. Never execute actions, change entity
        // attributes or advance the game's random stream while rendering the editor.
        internal static float EvaluateFormula(Root root, NodeBase result, IDictionary<string, float> samples)
        {
            var copies = new Dictionary<NodeBase, NodeBase>();
            var active = new HashSet<NodeBase>();
            NodeBase Build(NodeBase source)
            {
                if (copies.TryGetValue(source, out var existing)) return existing;
                if (!active.Add(source)) throw new InvalidOperationException(source.Uid + " 的输入形成循环。");
                NodeBase copy;
                if (source is VariableClass variable)
                {
                    if (variable.Type == VariableType.Attribute) AttributeObject.RequireDefinition(variable.AttributeKey, variable.Uid);
                    float value = variable.Constant;
                    if (variable.Type != VariableType.Constant && !samples.TryGetValue(InputKey(root, variable), out value))
                        throw new InvalidOperationException("缺少试算输入 " + InputKey(root, variable));
                    copy = new VariableClass { mType = NodeType.Variable, Type = VariableType.Constant, Constant = value };
                }
                else if (source is MathClass math)
                {
                    var op = new MathClass { mType = NodeType.Math, Type = math.Type };
                    for (int i = 0; i < Arity(math); i++)
                    {
                        var port = new PortSetting("输入", "MathIn", new[] { "Variable" }, false);
                        port.connectedNodes.Add(Build(NumericInput(root, math, i)));
                        op.inputPort.Add(port);
                    }
                    copy = op;
                }
                else throw new InvalidOperationException("此节点没有可试算的数值输出。");
                active.Remove(source);
                copy.NodeIndex = copies.Count;
                copies.Add(source, copy);
                return copy;
            }
            var output = Build(result);
            var state = new GraphRuntimeState { Hash = 1, NodeStates = new NodeRuntimeState[copies.Count] };
            for (int i = 0; i < copies.Count; i++) state.NodeStates[i] = new NodeRuntimeState();
            float answer = output.GetVariableValue(state);
            if (float.IsNaN(answer) || float.IsInfinity(answer)) throw new InvalidOperationException("试算结果不是有限数值。");
            return answer;
        }
    }
}
