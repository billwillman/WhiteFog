"""Build file://-loadable H5 tables from the decoded 1.3.0 Lua keys/list exports.

Usage: python export_skill_progression.py DECODED_TABLE_DIRECTORY [ADDITIONAL_DIRECTORY ...]
Rows retain the source units, optional columns and resource triples.
"""
import argparse
import json
from pathlib import Path

TARGET = Path(__file__).resolve().parents[3] / 'LoginDemo/battle-art/skill-tables.js'


def build(folders):
    def read(name):
        for folder in folders:
            path = folder / (name + '.json')
            if path.exists():
                return json.loads(path.read_text(encoding='utf-8-sig'))
        raise FileNotFoundError(name + '.json')

    def table(name, fields=None, partitions=0):
        source = read(name)
        positions = source['keys']
        columns = fields or sorted(positions, key=positions.get)
        result = []
        for part in range(partitions + 1):
            rows = (read(f'{name}_{part}') if part else source)['list']
            for row in (rows.values() if isinstance(rows, dict) else rows):
                result.append([row[positions[key]-1] if positions[key] <= len(row) else None for key in columns])
        return {'columns': columns, 'rows': result}

    return {
        'version': '1.3.0',
        'kungfu_info': table('kungfu_info', ['id','name','levelup_id','quality','type','weapon','link_id']),
        'skill_info': table('skill_info', ['id','name','cooldown'], 3),
        'skill_lvlup_info': table('skill_lvlup_info',
            ['id','advance','level','number','node','resource','damage','attribute','skill'], 15),
        **{name: table(name) for name in ['kungfu_compose_info','kungfu_compose_special_rate_info',
                                         'kungfu_compose_special_drop_info','kungfu_link_info']}
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('folders', nargs='+', type=Path)
    args = parser.parse_args()
    data = build(args.folders)
    def compact(value):
        return json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False)

    # Keep one source row per line for review and for meaningful table diffs on re-export.
    entries = ['"version":' + compact(data['version'])]
    for key, table in data.items():
        if key == 'version':
            continue
        rows = ',\n'.join(compact(row) for row in table['rows'])
        entries.append(compact(key) + ':{"columns":' + compact(table['columns']) + ',"rows":[\n' + rows + '\n]}')
    TARGET.write_text('window.qingyunSkillTables = {\n' + ',\n'.join(entries) + '\n};\n',encoding='utf8')
    print(' '.join(f'{k}={len(t["rows"])}' for k,t in data.items() if isinstance(t,dict)))
