using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(menuName = "Soft Kitty/技能等级表")]
    public sealed class SkillLevelTable : ScriptableObject
    {
        [Serializable] public class Field { public string key; public float value; }
        [Serializable] public class Row { public int level = 1; public List<Field> fields = new List<Field>(); }
        public List<Row> rows = new List<Row>();

        public void Read(int level, Dictionary<string, float> values)
        {
            Row found = null;
            foreach (var row in rows)
            {
                if (row.level != level) continue;
                if (found != null) throw new InvalidOperationException(name + " 等级重复：" + level);
                found = row;
            }
            if (found == null) throw new InvalidOperationException(name + " 缺少等级 " + level);
            var columns = new HashSet<string>();
            foreach (var field in found.fields)
            {
                if (string.IsNullOrWhiteSpace(field.key) || !columns.Add(field.key))
                    throw new InvalidOperationException(name + " 的列为空或重复：" + field.key);
                if (float.IsNaN(field.value) || float.IsInfinity(field.value))
                    throw new InvalidOperationException(name + " 的数值无效：" + field.key);
                values["level." + field.key] = field.value;
            }
        }
    }
}
