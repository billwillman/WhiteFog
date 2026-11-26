using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    // A pure numeric graph. The graph editor stores ordinary MCC entity, variable and math nodes.
    [CreateAssetMenu(menuName = "Soft Kitty/数值公式")]
    public sealed class SkillFormula : GraphObject
    {
        public string outputUid;
        private Root compiled;
        private NodeBase output;
        private readonly List<VariableClass> inputs = new List<VariableClass>();

        public void Invalidate() { compiled = null; }
        private void OnValidate() { Invalidate(); }

        private void Compile()
        {
            if (RootSet == null || string.IsNullOrEmpty(outputUid)) throw new InvalidOperationException("请选择结果节点。");
            if (RootSet.Actions.Count + RootSet.Triggers.Count + RootSet.Handlers.Count + RootSet.Branchs.Count + RootSet.ConditionGroups.Count != 0)
                throw new InvalidOperationException("公式图只接受实体、变量和数学节点。");
            var root = new Root(RootSet);
            if (!root.NodeDic.TryGetValue(outputUid, out var result)) throw new InvalidOperationException("结果节点已删除，请重新选择。");
            inputs.Clear();
            Visit(result, new HashSet<NodeBase>(), new HashSet<NodeBase>());
            output = result;
            compiled = root;
        }

        private void Visit(NodeBase node, HashSet<NodeBase> active, HashSet<NodeBase> done)
        {
            if (done.Contains(node)) return;
            if (!active.Add(node)) throw new InvalidOperationException("连线成环：" + node.Uid);
            if (node is MathClass math)
            {
                int arity = math.ValueC_Visible() ? 3 : math.ValueB_Visible() ? 2 : 1;
                for (int i = 0; i < arity; i++)
                {
                    var links = math.inputPort[i].connectedNodes;
                    if (links.Count != 1) throw new InvalidOperationException(math.Uid + " 缺少输入 " + (i + 1));
                    Visit(links[0], active, done);
                }
            }
            else if (node is VariableClass variable)
            {
                if (variable.Type == VariableType.Attribute && (variable.inputPort[0].connectedNodes.Count != 1 ||
                    !(variable.inputPort[0].connectedNodes[0] is EntityClass)))
                    throw new InvalidOperationException(variable.Uid + " 的属性必须连接一个实体。");
                if (variable.Type == VariableType.DynamicVariable && string.IsNullOrWhiteSpace(variable.VariableUid))
                    throw new InvalidOperationException(variable.Uid + " 的参数名为空。");
                inputs.Add(variable);
            }
            else throw new InvalidOperationException(node.Uid + " 不是数值节点。");
            active.Remove(node);
            done.Add(node);
        }

        // sampleInputs is optional and is used by the standalone calculator without spawning actors.
        public float Evaluate(RootRuntimeState context, IDictionary<string, float> sampleInputs = null)
        {
            if (compiled == null) Compile();
            var state = new GraphRuntimeState();
            compiled.InitRuntimeState(ref state);
            state.RootState = context;
            state.Hash = 1;
            foreach (var variable in inputs)
            {
                float value;
                if (variable.Type == VariableType.Attribute)
                {
                    var owner = (EntityClass)variable.inputPort[0].connectedNodes[0];
                    string key = AttributeInputKey(owner, variable.AttributeKey);
                    if (sampleInputs == null || !sampleInputs.TryGetValue(key, out value))
                    {
                        var entity = owner.GetEntity(state);
                        if (entity == null) throw new InvalidOperationException("无法读取实体属性 " + key);
                        value = entity.GetAttributeFloat(variable.AttributeKey);
                    }
                }
                else if (variable.Type == VariableType.DynamicVariable)
                {
                    string key = variable.VariableUid;
                    if (sampleInputs != null && sampleInputs.TryGetValue(key, out value)) { }
                    else if (context.DynamicVariableDic != null && context.DynamicVariableDic.TryGetValue(key, out value)) { }
                    else if (key.StartsWith("level.", StringComparison.Ordinal) || key.StartsWith("hit.", StringComparison.Ordinal))
                        throw new InvalidOperationException("缺少输入 " + key);
                    else value = variable.Constant;
                }
                else continue;
                state.NodeStates[variable.NodeIndex].SetHashCache(state.Hash, value);
            }
            float result = output.GetVariableValue(state);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidOperationException("公式结果不是有限数值。");
            return result;
        }

        public static string AttributeInputKey(EntityClass entity, string attribute)
            => (entity.Type == EntityType.Specified ? entity.EntityUid : entity.Type.ToString()) + "." + attribute;
    }
}
