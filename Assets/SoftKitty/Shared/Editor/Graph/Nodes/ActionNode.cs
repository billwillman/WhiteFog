using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SoftKitty
{
    public class ActionNode : CustomNodeEditor
    {
        private Action m_data;
        public Action data => m_data;
        public ActionNode(Action _action)
        {
            m_data = _action;
            if (SkillProjectile.IsLaunch(_action) || SkillProjectile.IsImpactLaunch(_action)) _action.EnsureArrivalPort();
        }
       

        void CountUp()
        {
            baseSettings.Q<IntegerField>("Count").value++;
        }

        void CountDown()
        {
            baseSettings.Q<IntegerField>("Count").value--;
        }



        public override void Init()
        {
            titleIcon.style.backgroundImage = new StyleBackground(mainDataObject.NodeIconSprites[1]);
            titleContainer.style.backgroundColor = new StyleColor(new Color(0.4F, 0.2F, 0.2F, 1F));

            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SoftKitty/Shared/Editor/Graph/UXML/ActionNode.uxml");
            baseSettings = InstantiateSettings(uiAsset);
            baseSettings.style.width = 250;
            baseSettings.Q<DropdownField>("Type").choices.Clear();
            baseSettings.Q<DropdownField>("Type").choices.AddRange(mainDataObject.ActionClassNames);
            baseSettings.Q<DropdownField>("Type").index = mainDataObject.ActionClassNames.IndexOf(m_data.ClassName);
            baseSettings.Q<DropdownField>("Type").value = m_data.ClassName;
            baseSettings.Q<DropdownField>("Type").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.ClassName = x.newValue;
                if (m_data.ClassName == "Projectile") m_data.requiresProjectileSnap = true;
                DeleteAllCustomPort(m_data,1);
                m_data.ResetInputPortSetting();
                RefreshProjectilePort();
                LoadClassInterface(m_data,"Action",1);
                DrawFormulaLinks();
                GetFirstAncestorOfType<MasterGraphView>()?.RefreshHitLinks();
            });

            baseSettings.Q<DropdownField>("ExcuteType").choices.Clear();
            baseSettings.Q<DropdownField>("ExcuteType").choices.AddRange(Enum.GetNames(typeof(ActionType)));
            baseSettings.Q<DropdownField>("ExcuteType").value = m_data.Type.ToString();
            baseSettings.Q<DropdownField>("ExcuteType").index = (int)m_data.Type;
            baseSettings.Q<DropdownField>("ExcuteType").RegisterValueChangedCallback(x =>
            {
                if (x.target is not DropdownField) return;
                m_data.Type =(ActionType)((DropdownField)x.target).index;
                FormatType();
            });

            baseSettings.Q<Button>("AdjustUp").clicked += CountUp;
            baseSettings.Q<Button>("AdjustDown").clicked += CountDown;

            baseSettings.Q<FloatField>("WaitTime").value = m_data.WaitTime;
            baseSettings.Q<FloatField>("WaitTime").RegisterValueChangedCallback(x =>
            {
                m_data.WaitTime = x.newValue;
                if (SkillWorkbench.IsSourceTime(m_data)) DrawFormulaLinks();
                if (SkillWorkbench.IsHitSource(m_data, mainDataObject)) GetFirstAncestorOfType<MasterGraphView>()?.RefreshHitLinks();
            });

            baseSettings.Q<IntegerField>("Count").value = m_data.Count;
            baseSettings.Q<IntegerField>("Count").RegisterValueChangedCallback(x =>
            {
                m_data.Count =x.newValue;
            });

            baseSettings.Q<FloatField>("KeepTime").value = m_data.KeepTime;
            baseSettings.Q<FloatField>("KeepTime").RegisterValueChangedCallback(x =>
            {
                m_data.KeepTime = x.newValue;
            });

            baseSettings.Q<FloatField>("IntervalTime").value = m_data.Interval;
            baseSettings.Q<FloatField>("IntervalTime").RegisterValueChangedCallback(x =>
            {
                m_data.Interval = x.newValue;
            });
            FormatType();
            extensionContainer.Add(baseSettings);
            m_data.ResetInputPortSetting();
            LoadClassInterface(m_data,"Action",1);
            if (SkillWorkbench.IsHitSource(m_data, mainDataObject) && output.Count == 0) RefreshProjectilePort();
            DrawFormulaLinks();
            RefreshExpandedState();
        }

        protected override void OnIntegerSettingChanged(string uid)
        {
            if (uid == "stage" && SkillProjectile.IsLaunch(m_data)) m_data.requiresProjectileSnap = true;
            if ((uid == "stage" || uid == "cue") && m_data.ClassName == "OriginalSkillVisual" ||
                uid == "style" && (m_data.ClassName == "SkillImpact" || m_data.ClassName == "FormulaImpact"))
            {
                RefreshProjectilePort();
                DrawFormulaLinks();
                GetFirstAncestorOfType<MasterGraphView>()?.RefreshHitLinks();
            }
        }

        private void RefreshProjectilePort()
        {
            if (SkillWorkbench.IsHitSource(m_data, mainDataObject))
            {
                m_data.EnsureArrivalPort();
                if (output.Count > 0) return;
                var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(NodeBase));
                port.portName = m_data.outputPort[0].Name;
                port.portColor = portColors[m_data.outputPort[0].Type];
                output.Add(port);
                outputContainer.Add(port);
            }
            // A different effect index can temporarily disable contact on the same track.
            else if (m_data.outputPort.Count > 0 && !(m_data.ClassName == "OriginalSkillVisual" && m_data.IntValues.FirstOrDefault() == 6) &&
                !((m_data.ClassName == "SkillImpact" || m_data.ClassName == "FormulaImpact") && m_data.outputPort[0].uid.Count > 0))
            {
                if (output.Count > 0)
                {
                    DisconnectOutput(0);
                    outputContainer.Remove(output[0]);
                    output.RemoveAt(0);
                }
                m_data.outputPort.Clear();
            }
            RefreshPorts();
        }

        internal void RefreshHitLinks(MasterGraphView view, IEnumerable<NodeBase> sources)
        {
            extensionContainer.Q<VisualElement>("hitLinks")?.RemoveFromHierarchy();
            bool emits = SkillWorkbench.IsHitSource(m_data, mainDataObject);
            var parents = sources.Where(n => SkillWorkbench.IsHitSource(n, mainDataObject)).OfType<Action>().Distinct().ToArray();
            var color = new Color(.97f, .69f, .28f);
            if (input.Count > 0)
            {
                bool onlyHits = parents.Length > 0 && sources.All(s => SkillWorkbench.IsHitSource(s, mainDataObject));
                input[0].portName = onlyHits ? "命中输入" : GraphEditorText.Localize(m_data.inputPort[0].Name);
                input[0].portColor = parents.Length > 0 ? color : portColors[m_data.inputPort[0].Type];
            }
            if (output.Count > 0)
            {
                output[0].portName = m_data.EmitsSettledHit ? "结算命中后执行" : emits ? "命中后执行" : "命中未启用";
                output[0].portColor = emits ? color : portColors[m_data.outputPort[0].Type];
            }
            if (parents.Length == 0 && (!emits || m_data.EmitsSettledHit && m_data.outputPort.FirstOrDefault()?.uid.Count == 0)) return;
            var panel = new VisualElement { name = "hitLinks" };
            var heading = new Label(emits ? SkillWorkbench.HitSourceLabel(m_data) : SkillWorkbench.HitActionLabel(m_data));
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(heading);
            void Link(string uid, string text)
            {
                var button = new Button(() => view.FocusNode(uid)) { text = text, tooltip = "定位 " + uid };
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.height = StyleKeyword.Auto;
                panel.Add(button);
            }
            foreach (var parent in parents)
                Link(parent.Uid, "来自 " + SkillWorkbench.HitSourceLabel(parent));
            if (m_data.ClassName == "FormulaImpact") panel.Add(new Label(SkillWorkbench.CalculationSource(m_data)));
            if (emits)
            {
                var links = m_data.outputPort.FirstOrDefault()?.uid;
                if (links == null || links.Count == 0)
                {
                    if (!m_data.EmitsSettledHit) panel.Add(new Label("未连接命中动作"));
                }
                else foreach (var link in links)
                {
                    string uid = link.Split('#')[0];
                    if (!view.NodeDic.TryGetValue(uid, out var child)) continue;
                    string target = child is ActionNode a ? SkillWorkbench.HitActionLabel(a.data) : child.title + " " + uid;
                    if (child is ActionNode impact && impact.data.ClassName == "FormulaImpact")
                        target += "\n" + SkillWorkbench.CalculationSource(impact.data);
                    Link(uid, "命中 → " + target);
                }
            }
            extensionContainer.Insert(0, panel);
        }

        private void DrawFormulaLinks()
        {
            extensionContainer.Q<VisualElement>("formulaLinks")?.RemoveFromHierarchy();
            if (SkillWorkbench.IsSourceTime(m_data))
            {
                var help = new Label(SkillWorkbench.SourceTimeHelp(m_data)) { name = "formulaLinks" };
                help.style.width = 250;
                help.style.whiteSpace = WhiteSpace.Normal;
                extensionContainer.Add(help);
                return;
            }
            if (m_data.ClassName == "Projectile" || m_data.ClassName == "OriginalSkillVisual" || m_data.ClassName == "Animation")
            {
                var edit = new Button(() => SkillWorkbench.OpenAction(mainDataObject, m_data.Uid))
                    { name = "formulaLinks", text = "编辑动作属性" };
                extensionContainer.Add(edit);
                return;
            }
            if (m_data.ClassName == "SkillImpact")
            {
                extensionContainer.Add(new Button(() => SkillWorkbench.OpenAction(mainDataObject, m_data.Uid))
                    { name = "formulaLinks", text = "编辑命中设置" });
                return;
            }
            if (m_data.ClassName != "FormulaImpact") return;
            var panel = new VisualElement { name = "formulaLinks" };
            var config = m_data.calculation;
            if (config == null) panel.Add(new Label("请在工作台配置攻击公式"));
            else
            {
                void Formula(string label, SkillFormula formula, string explanation)
                {
                    var button = new Button(() => MasterGraphWindow.OpenWindow(formula))
                        { text = label + " · " + (formula ? formula.name : "未绑定") };
                    button.SetEnabled(formula);
                    panel.Add(SkillDamageHelp.WithButton(button, label, explanation));
                }
                if (config.EffectiveMode == SkillCalculation.Mode.NativeGraph)
                {
                    var native = config.nativeGraph;
                    var open = new Button(() => MasterGraphWindow.OpenWindow(native))
                        { text = "MCC 原生伤害图 · " + (native ? native.name : "未绑定") };
                    open.SetEnabled(native);
                    panel.Add(open);
                    if (config.nativeParameters != null)
                        foreach (var field in config.nativeParameters)
                            if (field != null) panel.Add(new Label(field.key + " = " + field.value.ToString("G6")));
                }
                else if (config.ActiveFlow || config.UsesLinkedAssets)
                {
                    panel.Add(new Label("技能伤害流程 · " + (config.ActiveFlow ? config.ActiveFlow.name : "动作内置流程")));
                    if (config.independentSegment) panel.Add(new Label("逻辑段 " + config.logicIndex));
                    foreach (var stage in config.GetStageEntries())
                    {
                        if (stage == null || !config.IsStageEnabled(stage.id)) continue;
                        if (stage.kind == SkillDamageFlow.Kind.ModifyDamage && stage.conditionChance)
                            Formula(stage.label + "触发概率", stage.conditionChance, SkillDamageHelp.Condition);
                        if (stage.formula) Formula(stage.label, stage.formula, SkillDamageHelp.Formula(stage.kind));
                        else panel.Add(new Label(stage.label));
                    }
                }
                else panel.Add(new Label("伤害流程 · 未绑定"));
                panel.Add(new Button(() => Selection.activeObject = config.levels)
                    { text = "等级表 · " + (config.levels ? config.levels.name : "未绑定") });
                panel.Add(new Label("各段占比（%）  " + string.Join(" / ", config.segmentPercent)));
            }
            panel.Add(new Button(() => SkillWorkbench.OpenAction(mainDataObject, m_data.Uid)) { text = "配置伤害流程" });
            extensionContainer.Add(panel);
        }


        private void FormatType()
        {
            baseSettings.style.height = m_data.Type == ActionType.Only_Once ? 105 : 165;
            baseSettings.Q<Button>("AdjustUp").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<Button>("AdjustDown").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<IntegerField>("Count").style.display = m_data.Type == ActionType.Multiple_Time ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<FloatField>("KeepTime").style.display = m_data.Type == ActionType.For_X_Seconds ? DisplayStyle.Flex : DisplayStyle.None;
            baseSettings.Q<FloatField>("IntervalTime").style.display = m_data.Type != ActionType.Only_Once ? DisplayStyle.Flex : DisplayStyle.None;
        }


    }
}
