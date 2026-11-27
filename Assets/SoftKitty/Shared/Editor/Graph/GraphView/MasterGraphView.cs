using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor;
using System;
using System.Linq;


namespace SoftKitty
{
    public class MasterGraphView : GraphView
    {

        public GraphObject graph;
        public Dictionary<string, NodeBaseEditor> NodeDic = new Dictionary<string, NodeBaseEditor>();
        public List<CustomGroupEditor> Groups = new List<CustomGroupEditor>();
        public float saving = 0F;
        public MasterGraphView() { }

        internal void FocusNode(string uid)
        {
            if (uid == null || !NodeDic.TryGetValue(uid, out var node)) return;
            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        internal void RefreshHitLinks()
        {
            var sources = SkillWorkbench.ExecutionSources(graph.RootSet);
            foreach (var node in NodeDic.Values.OfType<ActionNode>())
                node.RefreshHitLinks(this, sources[node.data.Uid + "#0"]);
        }

        internal void ArrangeNodes()
        {
            var grouped = new HashSet<string>(Groups.SelectMany(group => group.data.Children));
            var cards = NodeDic.Values.Where(card => !grouped.Contains(card.baseData.Uid)).ToArray();
            if (cards.Length == 0 || cards.Any(card => !(card.layout.width > 0 && card.layout.height > 0))) return;
            const float columnGap = 104;
            const float rowGap = 32;
            var links = edges.ToList().Where(edge => edge.output?.node is NodeBaseEditor && edge.input?.node is NodeBaseEditor)
                .Select(edge => (source: (NodeBaseEditor)edge.output.node, target: (NodeBaseEditor)edge.input.node))
                .Where(link => link.source != link.target && !grouped.Contains(link.source.baseData.Uid) &&
                    !grouped.Contains(link.target.baseData.Uid)).Distinct().ToArray();
            var outputs = links.ToLookup(link => link.source, link => link.target);
            var inputs = links.ToLookup(link => link.target, link => link.source);
            var incoming = cards.ToDictionary(card => card, card => inputs[card].Count());
            var columns = cards.ToDictionary(card => card, card => 0);
            var queue = new Queue<NodeBaseEditor>(cards.Where(card => incoming[card] == 0));
            var ordered = new List<NodeBaseEditor>();
            while (queue.Count != 0)
            {
                var card = queue.Dequeue();
                ordered.Add(card);
                foreach (var next in outputs[card])
                {
                    columns[next] = Mathf.Max(columns[next], columns[card] + 1);
                    if (--incoming[next] == 0) queue.Enqueue(next);
                }
            }

            // Keep helpers one column before their first consumer, including helpers on later hit branches.
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                var card = ordered[i];
                if (card.baseData is Action or Root || !outputs[card].Any()) continue;
                columns[card] = Mathf.Max(columns[card], outputs[card].Min(next => columns[next]) - 1);
            }

            Undo.RegisterCompleteObjectUndo(graph, "整理节点布局");
            float x = cards.Min(card => card.baseData.Position.x);
            float top = cards.Min(card => card.baseData.Position.y);
            var lanes = cards.GroupBy(card => columns[card]).OrderBy(group => group.Key).ToArray();
            var left = new Dictionary<int, float>();
            foreach (var column in lanes)
            {
                left[column.Key] = x;
                x += column.Max(card => card.layout.width) + columnGap;
            }
            var obstacles = graph.RootSet.Notes.Select(note => new Rect(note.Position, note.Size))
                .Concat(Groups.Select(group => group.GetPosition())).OrderBy(rect => rect.yMin).ToArray();
            var placed = new Dictionary<NodeBaseEditor, Rect>();
            // Lay out each independent cast action together with its inputs and all of its hit branches.
            var roots = cards.Where(card => card.baseData is Root).ToArray();
            var assigned = new HashSet<NodeBaseEditor>(roots);
            var seeds = roots.SelectMany(root => outputs[root]).OrderBy(card => (card.baseData as Action)?.WaitTime ?? 0)
                .Concat(cards.OrderBy(card => card.baseData.Uid, StringComparer.Ordinal));
            var bands = new List<List<NodeBaseEditor>>();
            foreach (var seed in seeds)
            {
                if (!assigned.Add(seed)) continue;
                var band = new List<NodeBaseEditor> { seed };
                for (int i = 0; i < band.Count; i++)
                    foreach (var neighbor in inputs[band[i]].Concat(outputs[band[i]]))
                        if (assigned.Add(neighbor)) band.Add(neighbor);
                bands.Add(band);
            }
            if (bands.Count == 0) bands.Add(new List<NodeBaseEditor>());
            bands[0].AddRange(roots);
            foreach (var band in bands)
            {
                float bottom = top;
                foreach (var column in band.GroupBy(card => columns[card]).OrderByDescending(group => group.Key))
                {
                    float y = top;
                    float ConsumerY(NodeBaseEditor card) => outputs[card].Where(placed.ContainsKey)
                        .Select(target => placed[target].y).DefaultIfEmpty(top).Min();
                    foreach (var card in column.OrderBy(ConsumerY).ThenBy(card => card.baseData is Root ? 0 : card.baseData is Action ? 1 : 2)
                        .ThenBy(card => card.baseData.Uid, StringComparer.Ordinal))
                    {
                        var rect = new Rect(new Vector2(left[column.Key], Mathf.Max(y, ConsumerY(card))), card.layout.size);
                        foreach (var obstacle in obstacles)
                            if (rect.Overlaps(obstacle)) rect.y = obstacle.yMax + rowGap;
                        card.SetPosition(rect);
                        card.UpdatePosition(rect);
                        placed[card] = rect;
                        y = rect.yMax + rowGap;
                    }
                    bottom = Mathf.Max(bottom, y);
                }
                top = bottom + rowGap;
            }
            EditorUtility.SetDirty(graph);
            if (graph.autoSave) graph.pendingSave = true;
            schedule.Execute(() => FrameAll());
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList();
        }

        private CustomGroupEditor BelongGroup(Node _node)
        {
            CustomGroupEditor _result = null;
            foreach (var obj in Groups) {
                if(obj.ContainsElement(_node)){
                    _result = obj;
                }
            }
            return _result;
        }


        public List<Node> GetSelectedNodes()
        {
            List<Node> selectedNodes = new List<Node>();
            foreach (VisualElement element in selection)
            {
                if (element is Node node)
                {
                    selectedNodes.Add(node);
                }
            }
            return selectedNodes;
        }

        public void CreateGroup(CustomGroup _data)
        {
            var newGroup = new CustomGroupEditor(_data);
            newGroup.SetPosition(_data.Position);
            foreach (var obj in GetSelectedNodes()) {
                newGroup.AddElement(obj);
            }
            graph.RootSet.Groups.Add(_data);
            newGroup.InitializeNode(_data,this);
            AddElement(newGroup);
            Groups.Add(newGroup);
        }

        public void UnGroup(CustomGroupEditor _group)
        {
            if (_group != null)
            {
                foreach (var obj in GetSelectedNodes())
                {
                    _group.RemoveElement((Node)obj);
                }
            }
        }

        public void RemoveGroup(CustomGroupEditor _group)
        {
            for (int i = graph.RootSet.Groups.Count - 1; i >= 0; i--)
            {
                if (graph.RootSet.Groups[i].Uid == _group.data.Uid) graph.RootSet.Groups.RemoveAt(i);
            }
            Groups.Remove(_group);
            RemoveElement(_group);
        }

        public void CreateAction(Action _data)
        {
            ActionNode _newNode = new ActionNode(_data) { name = "Action" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Actions.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            RefreshHitLinks();
            if(graph.autoSave) graph.pendingSave=true;
        }

        public void CreateNote(Note _data)
        {
            CustomStickyNote _newNode = new CustomStickyNote(_data) { name = "Note" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data);
            _newNode.Move(_data.Position, _data.Size);
            graph.RootSet.Notes.Add(_data);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateHandler(Handler _data)
        {
            HandlerNode _newNode = new HandlerNode(_data) { name = "Condition" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Handlers.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateConditionGroup(ConditionGroup _data)
        {
            ConditionGroupNode _newNode = new ConditionGroupNode(_data) { name = "Condition Group" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.ConditionGroups.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateTrigger(Trigger _data)
        {
            TriggerNode _newNode = new TriggerNode(_data) { name = "Trigger" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Triggers.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateMath(DropdownMenuAction e, MathType _type)
        {
            Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
            MathClass _data = new MathClass(graph.RootSet);
            _data.Type = _type;
            _data.Position = _pos;
            _data.Name = "Math (" + _data.Type.ToString() + ")";
            CreateMathByData(_data);
        }

        public void CreateMathByData(MathClass _data)
        {
            MathNode _newNode = new MathNode(_data) { name = "Math" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Maths.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateBranch(DropdownMenuAction e, BranchType _type)
        {
            Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
            Branch _data = new Branch(graph.RootSet);
            _data.Type = _type;
            _data.Position = _pos;
            _data.Name = "Branch (" + _data.Type.ToString() + ")";
            _data.FormatPort();
            CreateBranchByData(_data);
        }

        public void CreateBranchByData(Branch _data)
        {
            BranchNode _newNode = new BranchNode(_data) { name = "Branch" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Branchs.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }


        public void CreateVariable(DropdownMenuAction e, VariableType _type)
        {
            Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
            VariableClass _data = new VariableClass(graph.RootSet);
            _data.Type = _type;
            _data.Position = _pos;
            _data.Name = "Variable (" + _data.Type.ToString() + ")";
            CreateVariableByData(_data);
        }

        public void CreateVariableByData(VariableClass _data)
        {
            VariableNode _newNode = new VariableNode(_data) { name = "Variable" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Variables.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }

        public void CreateEntity(DropdownMenuAction e, EntityType _type)
        {
            Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
            EntityClass _data = new EntityClass(graph.RootSet);
            _data.Type = _type;
            _data.Position = _pos;
            _data.Name= "Entity (" + _data.Type.ToString() + ")";
            CreateEntityByData(_data);
        }

        public void CreateEntityByData(EntityClass _data)
        {
            EntityNode _newNode = new EntityNode(_data) { name = "Entity" };
            AddElement(_newNode);
            _newNode.InitializeNode(_data, graph);
            _newNode.Move(_data.Position);
            graph.RootSet.Entities.Add(_data);
            NodeDic.Add(_data.Uid, _newNode);
            if (graph.autoSave) graph.pendingSave = true;
        }


        public void Save()
        {
            saving = 100F;
            graph.pendingSave = false;
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        public void SetSummary()
        {
            foreach (VisualElement element in selection)
            {
                if (element is CustomStickyNote node)
                {
                    graph.summaryNoteId = node.data.Uid;
                    node.data.Name = "Summary";
                    node.title= "Summary";
                    return;
                }
            }
           
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            if (evt.target is GraphView || evt.target is Node || evt.target is CustomStickyNote)
            {
                if(!(evt.target is CustomStickyNote)) evt.menu.ClearItems();

                if (evt.target is NodeBaseEditor)
                {
                    if (((NodeBaseEditor)evt.target).IsGroupable())
                    {
                        CustomGroupEditor _group = BelongGroup((Node)evt.target);
                        if (_group != null)
                        {
                            evt.menu.AppendAction("取消分组", (e) =>
                            {
                                UnGroup(_group);
                            });
                        }
                        else
                        {
                            evt.menu.AppendAction("分组", (e) =>
                            {
                                Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                                CustomGroup _data = new CustomGroup();
                                _data.Title = "New Group";
                                _data.Position = new Rect(_pos.x, _pos.y, 500, 200);
                                _data.RandomeUid();
                                CreateGroup(_data);
                            });
                        }

                        evt.menu.AppendSeparator();
                    }
                }

                if ((evt.target is CustomStickyNote))
                {
                    evt.menu.AppendAction("设为图摘要", (e) =>
                    {
                        SetSummary();
                    });
                }

                evt.menu.AppendAction("新建便笺", (e) =>
                {
                    Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                    Note _data = new Note();
                    _data.Position = _pos;
                    _data.RandomeUid();
                    CreateNote(_data);
                });
                evt.menu.AppendSeparator();

                foreach (VariableType _type in Enum.GetValues(typeof(VariableType)))
                {
                    evt.menu.AppendAction("创建节点/变量/" + GraphEditorText.Localize(_type.ToString()), (e) =>
                    {
                        CreateVariable(e, _type);
                    });
                }

                foreach (MathType _type in  Enum.GetValues(typeof(MathType))){
                    evt.menu.AppendAction("创建节点/数学/" + GraphEditorText.Localize(_type.ToString()), (e) =>
                    {
                        CreateMath(e, _type);
                    });
                }

                foreach (EntityType _type in Enum.GetValues(typeof(EntityType)))
                {
                    evt.menu.AppendAction("创建节点/实体/" + GraphEditorText.Localize(_type.ToString()), (e) =>
                    {
                        CreateEntity(e, _type);
                    });
                }

                foreach (BranchType _type in Enum.GetValues(typeof(BranchType)))
                {
                    evt.menu.AppendAction("创建节点/分支/" + GraphEditorText.Localize(_type.ToString()), (e) =>
                    {
                        CreateBranch(e, _type);
                    });
                }


                evt.menu.AppendAction("创建节点/动作", (e) =>
                {
                    Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                    Action _data = new Action(graph.RootSet);
                    _data.Position = _pos;
                    CreateAction(_data);
                });
                evt.menu.AppendAction("创建节点/条件", (e) =>
                {
                    Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                    Handler _data = new Handler(graph.RootSet);
                    _data.Position = _pos;
                    CreateHandler(_data);

                });
                evt.menu.AppendAction("创建节点/条件组", (e) =>
                {
                    Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                    ConditionGroup _data = new ConditionGroup(graph.RootSet);
                    _data.Position = _pos;
                    CreateConditionGroup(_data);
                });
                evt.menu.AppendAction("创建节点/触发器", (e) =>
                {
                    Vector2 _pos = MouseToContent(e.eventInfo.mousePosition);
                    Trigger _data = new Trigger(graph.RootSet);
                    _data.Position = _pos;
                    CreateTrigger(_data);

                });

                if (evt.target is Node || evt.target is CustomStickyNote)
                {
                    evt.menu.AppendSeparator();
                    if (evt.target is Node)
                    {
                        evt.menu.AppendAction("复制节点", (e) =>
                        {
                            foreach (var obj in selection)
                            {
                                if (obj is ActionNode)
                                {
                                    Action _copy = ((ActionNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateAction(_copy);
                                }
                                else if (obj is TriggerNode)
                                {
                                    Trigger _copy = ((TriggerNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateTrigger(_copy);
                                }
                                else if (obj is ConditionGroupNode)
                                {
                                    ConditionGroup _copy = ((ConditionGroupNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateConditionGroup(_copy);
                                }
                                else if (obj is HandlerNode)
                                {
                                    Handler _copy = ((HandlerNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateHandler(_copy);
                                }
                                else if (obj is VariableNode)
                                {
                                    VariableClass _copy = ((VariableNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateVariableByData(_copy);
                                }
                                else if (obj is MathNode)
                                {
                                    MathClass _copy = ((MathNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateMathByData(_copy);
                                }
                                else if (obj is BranchNode)
                                {
                                    Branch _copy = ((BranchNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateBranchByData(_copy);
                                }
                                else if (obj is EntityNode)
                                {
                                    EntityClass _copy = ((EntityNode)obj).data.Copy();
                                    _copy.RegenerateUid(graph.RootSet);
                                    foreach (var _port in _copy.inputPort) _port.uid.Clear();
                                    foreach (var _port in _copy.outputPort) _port.uid.Clear();
                                    _copy.Position.x += 100;
                                    _copy.Position.y += 150;
                                    CreateEntityByData(_copy);
                                }
                            }

                        });
                    }
                    evt.menu.AppendAction("删除", (e) =>
                    {
                        DeleteSelection();
                    });
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("查看文档", (e) =>
                    {
                        if (selection.Count > 0)
                            OpenDocumentation(selection[0].GetType());
                        else
                            Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/overview");
                    });
                }
                else
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("查看文档", (e) =>
                    {
                        Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/overview");
                    });
                }
            }

        }

        

        public void OpenDocumentation(Type _type)
        {
            if (_type == typeof(ActionNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/action");
            }
            else if (_type == typeof(ConditionGroupNode) || _type == typeof(HandlerNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/condition");
            }
            else if (_type == typeof(VariableNode) || _type == typeof(MathNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/variable-math");
            }
            else if (_type == typeof(TriggerNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/trigger");
            }
            else if (_type == typeof(BranchNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/branch");
            }
            else if (_type == typeof(EntityNode))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/entity");
            }
            else
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-combat-core/nodes/overview");
            }
        }


        internal Vector2 MouseToContent(Vector2 position)
        {
            position.x = (position.x - contentViewContainer.worldBound.x) / scale;
            position.y = (position.y - contentViewContainer.worldBound.y) / scale;
            return position;
        }

    }
}
