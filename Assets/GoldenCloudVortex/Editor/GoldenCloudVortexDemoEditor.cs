using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GoldenCloudVortexDemo))]
public sealed class GoldenCloudVortexDemoEditor : Editor
{
    static readonly GUIContent FixedCameraLabel = new GUIContent(
        "固定相机", "勾选为固定机位，取消为穿行。编辑模式下保存为 PC 和微信小游戏的共同默认值。");

    public override void OnInspectorGUI()
    {
        var demo=(GoldenCloudVortexDemo)target;
        bool playing=Application.isPlaying;
        bool current=playing ? demo.FixedCamera : GoldenCloudVortexDemo.ReadCameraSettings().fixedCamera;
        EditorGUI.BeginChangeCheck();
        bool selected=EditorGUILayout.Toggle(FixedCameraLabel,current);
        if(EditorGUI.EndChangeCheck())
        {
            if(playing) demo.FixedCamera=selected;
            else
            {
                var settings=new GoldenCloudVortexDemo.CameraSettings { fixedCamera=selected };
                var asset=Resources.Load<TextAsset>(GoldenCloudVortexDemo.CameraSettingsResource);
                string path=AssetDatabase.GetAssetPath(asset);
                File.WriteAllText(path,JsonUtility.ToJson(settings,true)+"\n");
                AssetDatabase.ImportAsset(path);
            }
        }
        DrawDefaultInspector();
    }
}
