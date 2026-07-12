using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PetriNetAvatarDroneAnimator : MonoBehaviour
{
	private readonly List<Transform> rotors = new List<Transform>();
	private readonly List<Vector3> initialRotorPositions = new List<Vector3>();
	private readonly List<Quaternion> initialRotorRotations = new List<Quaternion>();
	private readonly List<float> rotorDirections = new List<float>();
	private readonly List<Vector3> rotorLocalAxes = new List<Vector3>();
	private AnimationClip[] clips;
	private bool hasPlayableClips;
	private bool playing;
	private float playbackTime;
	private float spinAngle;
	private float rotorDegreesPerSecond;
	private Vector3 rotorLocalAxis = Vector3.zero;

	public void Configure(
		AnimationClip[] sourceClips,
		bool useImportedAnimationClips,
		string clipNameContains,
		string clipNameExcludes,
		float rotorSpeed,
		Vector3 rotorAxis,
		bool shouldPlay)
	{
		clips = useImportedAnimationClips ? FilterClips(sourceClips, clipNameContains, clipNameExcludes) : null;
		hasPlayableClips = HasPlayableClip(clips);
		rotorDegreesPerSecond = Mathf.Max(0f, rotorSpeed);
		rotorLocalAxis = rotorAxis.sqrMagnitude > 0.0001f ? rotorAxis.normalized : Vector3.zero;
		CollectRotorsIfNeeded();
		playing = shouldPlay && (hasPlayableClips || (rotorDegreesPerSecond > 0f && rotors.Count > 0));
		enabled = playing;
	}

	private void Update()
	{
		if (!playing || !hasPlayableClips || clips == null)
		{
			return;
		}

		playbackTime += Time.deltaTime;
		Vector3 rootPosition = transform.localPosition;
		Quaternion rootRotation = transform.localRotation;
		Vector3 rootScale = transform.localScale;

		for (int i = 0; i < clips.Length; i++)
		{
			AnimationClip clip = clips[i];
			if (clip == null || clip.length <= 0.001f)
			{
				continue;
			}

			float clipTime = Mathf.Repeat(playbackTime, clip.length);
			clip.SampleAnimation(gameObject, clipTime);
		}

		transform.localPosition = rootPosition;
		transform.localRotation = rootRotation;
		transform.localScale = rootScale;
		RestoreRotorTransforms(false);
	}

	private void LateUpdate()
	{
		if (!playing || rotorDegreesPerSecond <= 0f || rotors.Count <= 0)
		{
			return;
		}

		spinAngle = Mathf.Repeat(spinAngle + rotorDegreesPerSecond * Time.deltaTime, 360f);
		RestoreRotorTransforms(true);
	}

	private void RestoreRotorTransforms(bool applySpin)
	{
		for (int i = 0; i < rotors.Count; i++)
		{
			Transform rotor = rotors[i];
			if (rotor == null)
			{
				continue;
			}

			float direction = i < rotorDirections.Count ? rotorDirections[i] : GetRotorDirection(rotor, i);
			Vector3 axis = i < rotorLocalAxes.Count ? rotorLocalAxes[i] : GetFallbackRotorLocalAxis();
			rotor.localPosition = initialRotorPositions[i];
			rotor.localRotation = applySpin
				? initialRotorRotations[i] * Quaternion.AngleAxis(spinAngle * direction, axis)
				: initialRotorRotations[i];
		}
	}

	private void CollectRotorsIfNeeded()
	{
		if (rotors.Count > 0)
		{
			return;
		}

		MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
		Bounds droneBounds = CalculateRendererBounds(renderers);
		float maxDroneSpan = GetMaxBoundsSpan(droneBounds);
		for (int i = 0; i < renderers.Length; i++)
		{
			MeshRenderer renderer = renderers[i];
			if (renderer == null)
			{
				continue;
			}

			Transform rotor = renderer.transform;
			if (rotor == null || rotor == transform)
			{
				continue;
			}

			if (!IsRotorTransform(rotor))
			{
				continue;
			}

			if (IsUnderCollectedRotor(rotor))
			{
				continue;
			}

			if (maxDroneSpan > 0.001f && GetMaxBoundsSpan(renderer.bounds) > maxDroneSpan * 0.58f)
			{
				continue;
			}

			Transform pivot = CreateRotorPivot(rotor, renderer.bounds.center);
			rotors.Add(pivot);
			initialRotorPositions.Add(pivot.localPosition);
			initialRotorRotations.Add(pivot.localRotation);
			rotorDirections.Add(GetRotorDirection(rotor, rotorDirections.Count));
			rotorLocalAxes.Add(GetRotorLocalAxis(renderer, rotorLocalAxis));
		}
	}

	private Vector3 GetFallbackRotorLocalAxis()
	{
		return rotorLocalAxis.sqrMagnitude > 0.0001f ? rotorLocalAxis : Vector3.forward;
	}

	private bool IsUnderCollectedRotor(Transform candidate)
	{
		for (int i = 0; i < rotors.Count; i++)
		{
			Transform collected = rotors[i];
			if (collected != null && candidate.IsChildOf(collected))
			{
				return true;
			}
		}

		return false;
	}

	private static Bounds CalculateRendererBounds(MeshRenderer[] renderers)
	{
		Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
		bool hasBounds = false;
		for (int i = 0; i < renderers.Length; i++)
		{
			MeshRenderer renderer = renderers[i];
			if (renderer == null)
			{
				continue;
			}

			if (!hasBounds)
			{
				bounds = renderer.bounds;
				hasBounds = true;
				continue;
			}

			bounds.Encapsulate(renderer.bounds);
		}

		return bounds;
	}

	private static float GetMaxBoundsSpan(Bounds bounds)
	{
		Vector3 size = bounds.size;
		return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
	}

	private Transform CreateRotorPivot(Transform rotor, Vector3 worldCenter)
	{
		Transform parent = rotor.parent;
		GameObject pivotObject = new GameObject("RotorSpinPivot_" + rotor.name);
		Transform pivot = pivotObject.transform;
		pivot.SetParent(parent, false);
		pivot.position = worldCenter;
		pivot.rotation = rotor.rotation;
		pivot.localScale = Vector3.one;
		rotor.SetParent(pivot, true);
		return pivot;
	}

	private static Vector3 GetRotorLocalAxis(MeshRenderer renderer, Vector3 configuredAxis)
	{
		if (configuredAxis.sqrMagnitude > 0.0001f)
		{
			return configuredAxis.normalized;
		}

		MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
		if (meshFilter == null || meshFilter.sharedMesh == null)
		{
			return Vector3.forward;
		}

		Vector3 size = meshFilter.sharedMesh.bounds.size;
		if (size.x <= size.y && size.x <= size.z)
		{
			return Vector3.right;
		}

		if (size.y <= size.x && size.y <= size.z)
		{
			return Vector3.up;
		}

		return Vector3.forward;
	}

	private static bool IsRotorTransform(Transform candidate)
	{
		for (Transform current = candidate; current != null; current = current.parent)
		{
			if (IsRotorName(current.name))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsRotorName(string objectName)
	{
		if (string.IsNullOrWhiteSpace(objectName))
		{
			return false;
		}

		return objectName.IndexOf("rotor", StringComparison.OrdinalIgnoreCase) >= 0
			|| objectName.IndexOf("elica", StringComparison.OrdinalIgnoreCase) >= 0
			|| objectName.IndexOf("helix", StringComparison.OrdinalIgnoreCase) >= 0
			|| objectName.IndexOf("prop", StringComparison.OrdinalIgnoreCase) >= 0
			|| objectName.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static float GetRotorDirection(Transform rotor, int index)
	{
		string path = GetTransformNamePath(rotor);
		if (path.IndexOf("_R", StringComparison.OrdinalIgnoreCase) >= 0
			|| path.IndexOf(".R", StringComparison.OrdinalIgnoreCase) >= 0
			|| path.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return -1f;
		}

		if (path.IndexOf("_L", StringComparison.OrdinalIgnoreCase) >= 0
			|| path.IndexOf(".L", StringComparison.OrdinalIgnoreCase) >= 0
			|| path.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 1f;
		}

		return index % 2 == 0 ? 1f : -1f;
	}

	private static string GetTransformNamePath(Transform transform)
	{
		if (transform == null)
		{
			return "";
		}

		string path = transform.name ?? "";
		for (Transform current = transform.parent; current != null; current = current.parent)
		{
			path = (current.name ?? "") + "/" + path;
		}

		return path;
	}

	private static AnimationClip[] FilterClips(AnimationClip[] sourceClips, string clipNameContains, string clipNameExcludes)
	{
		if (sourceClips == null)
		{
			return null;
		}

		List<AnimationClip> allowedClips = new List<AnimationClip>();
		for (int i = 0; i < sourceClips.Length; i++)
		{
			AnimationClip clip = sourceClips[i];
			if (!IsClipAllowed(clip, clipNameContains, clipNameExcludes))
			{
				continue;
			}

			allowedClips.Add(clip);
		}

		return allowedClips.ToArray();
	}

	private static bool IsClipAllowed(AnimationClip clip, string clipNameContains, string clipNameExcludes)
	{
		if (clip == null || clip.length <= 0.001f)
		{
			return false;
		}

		string clipName = clip.name ?? "";
		if (!string.IsNullOrWhiteSpace(clipNameContains)
			&& clipName.IndexOf(clipNameContains, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(clipNameExcludes)
			&& clipName.IndexOf(clipNameExcludes, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return false;
		}

		return true;
	}

	private static bool HasPlayableClip(AnimationClip[] sourceClips)
	{
		if (sourceClips == null)
		{
			return false;
		}

		for (int i = 0; i < sourceClips.Length; i++)
		{
			if (sourceClips[i] != null && sourceClips[i].length > 0.001f)
			{
				return true;
			}
		}

		return false;
	}
}
