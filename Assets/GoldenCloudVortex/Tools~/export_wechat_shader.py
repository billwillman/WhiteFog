"""Export the ShaderLab cloud functions for the standalone WeChat renderer."""
import argparse
from pathlib import Path
from textwrap import dedent

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "GoldenCloudVortex.shader"
OUTPUT = ROOT / "WeChatMiniGame" / "rolling.txt"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if the exported code differs")
    args = parser.parse_args()
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    markers = [line.strip() for line in lines]
    start = markers.index("// BEGIN SHARED CLOUD CODE") + 1
    end = markers.index("// END SHARED CLOUD CODE", start)
    exported = dedent("\n".join(lines[start:end])) + "\n"
    if args.check:
        if OUTPUT.read_text(encoding="utf-8") != exported:
            parser.exit(1, "rolling.txt differs. Run export_wechat_shader.py to update it.\n")
    else:
        OUTPUT.write_text(exported, encoding="utf-8")


if __name__ == "__main__":
    main()
