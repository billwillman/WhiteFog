using UnityEditor;
using UnityEngine;
using System.Collections;

namespace SoftKitty
{
	public class CustomGraphInterfaceMenu : ScriptableWizard
	{

		[MenuItem("Window/Soft Kitty/Custom Graph Interface Tool")]

		public static void CreateWizard()
		{
			var _window = EditorWindow.GetWindow<CustomGraphInterfaceTool>(false, "Custom Graph Interface Tool", true);
			_window.minSize = new Vector2(580F,700F);
			_window.Show();
		}

	}

	
}
