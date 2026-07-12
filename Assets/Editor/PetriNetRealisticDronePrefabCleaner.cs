using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class PetriNetRealisticDronePrefabCleaner
{
	private const string DronePrefabPath = "Assets/Realistic Drone/drone/3dModel/Drone.prefab";
	private const string DroneMaterialsFolder = "Assets/Realistic Drone/drone/3dModel/Materials";
	private const string SessionKey = "PetriNetRealisticDronePrefabCleaner.Cleaned.v2";

	static PetriNetRealisticDronePrefabCleaner()
	{
		EditorApplication.delayCall += CleanOnce;
	}

	private static void CleanOnce()
	{
		if (SessionState.GetBool(SessionKey, false))
		{
			return;
		}

		SessionState.SetBool(SessionKey, true);
		FixDroneMaterials();

		GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
		if (prefabAsset == null)
		{
			return;
		}

		GameObject prefabRoot = null;
		try
		{
			prefabRoot = PrefabUtility.LoadPrefabContents(DronePrefabPath);
			int removedCount = 0;
			Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < transforms.Length; i++)
			{
				removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
			}

			if (removedCount > 0)
			{
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, DronePrefabPath);
				Debug.Log("Cleaned " + removedCount + " missing script components from " + DronePrefabPath + ".");
			}
		}
		finally
		{
			if (prefabRoot != null)
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}
	}

	private static void FixDroneMaterials()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null)
		{
			shader = Shader.Find("Universal Render Pipeline/Simple Lit");
		}

		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		if (shader == null)
		{
			return;
		}

		string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { DroneMaterialsFolder });
		bool changedAny = false;
		for (int i = 0; i < materialGuids.Length; i++)
		{
			string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
			Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			if (material == null)
			{
				continue;
			}

			Color color = GetMaterialColor(material);
			if (material.shader != shader)
			{
				material.shader = shader;
				changedAny = true;
			}

			SetMaterialColor(material, color);
			DisableEmission(material);
			SetOpaqueSurface(material);
			EditorUtility.SetDirty(material);
			changedAny = true;
		}

		if (changedAny)
		{
			AssetDatabase.SaveAssets();
			Debug.Log("Fixed Realistic Drone materials for the active render pipeline.");
		}
	}

	private static Color GetMaterialColor(Material material)
	{
		if (material.HasProperty("_BaseColor"))
		{
			return material.GetColor("_BaseColor");
		}

		if (material.HasProperty("_Color"))
		{
			return material.GetColor("_Color");
		}

		return Color.white;
	}

	private static void SetMaterialColor(Material material, Color color)
	{
		Color opaqueColor = new Color(color.r, color.g, color.b, 1f);
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", opaqueColor);
		}

		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", opaqueColor);
		}
	}

	private static void DisableEmission(Material material)
	{
		material.DisableKeyword("_EMISSION");
		if (material.HasProperty("_EmissionColor"))
		{
			material.SetColor("_EmissionColor", Color.black);
		}

		material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
	}

	private static void SetOpaqueSurface(Material material)
	{
		if (material.HasProperty("_Surface"))
		{
			material.SetFloat("_Surface", 0f);
		}

		if (material.HasProperty("_Blend"))
		{
			material.SetFloat("_Blend", 0f);
		}

		if (material.HasProperty("_AlphaClip"))
		{
			material.SetFloat("_AlphaClip", 0f);
		}

		if (material.HasProperty("_SrcBlend"))
		{
			material.SetFloat("_SrcBlend", 1f);
		}

		if (material.HasProperty("_DstBlend"))
		{
			material.SetFloat("_DstBlend", 0f);
		}

		if (material.HasProperty("_ZWrite"))
		{
			material.SetFloat("_ZWrite", 1f);
		}

		material.SetOverrideTag("RenderType", "Opaque");
		material.renderQueue = -1;
	}
}
