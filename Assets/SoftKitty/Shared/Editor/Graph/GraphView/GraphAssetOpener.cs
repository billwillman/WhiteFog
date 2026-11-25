using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;


public static class GraphAssetOpener
{

    [OnOpenAsset(0)]
    public static bool OnOpenGraphAsset(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);

        SoftKitty.GraphObject graphData = asset as SoftKitty.GraphObject;

        if (graphData != null)
        {
            SoftKitty.MasterGraphWindow.OpenWindow(graphData);
            return true; 
        }

        return false;
    }
}