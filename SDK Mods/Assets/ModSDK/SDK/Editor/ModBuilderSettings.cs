using System;
using System.Collections.Generic;
using System.IO;
using PugMod;
using UnityEditor;
using UnityEngine;

public class ModBuilderSettings : ScriptableObject
{
	public ModMetadata metadata = new ModMetadata
	{
		guid = Guid.NewGuid().ToString("N"),
		name = "MyMod",
	};
	
	public string modPath = "Assets/Mod";
	public bool buildLinux = false;

	private void OnValidate()
	{
		if (string.IsNullOrEmpty(modPath))
		{
			var path = AssetDatabase.GetAssetPath(this);
			modPath = Path.GetDirectoryName(path);
		}
	}
}