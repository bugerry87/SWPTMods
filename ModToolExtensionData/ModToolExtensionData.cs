using System;
using UnityEngine;

namespace ModToolExtension
{
	public class AvatarData : MonoBehaviour
	{
		[Header("Override the Avatar for your Pose:")]
		[Header("Warning: Requires the ModToolExtension!")]
		public Avatar avatar;
	}

	public class BodyData : MonoBehaviour
	{
		[Serializable]
		public struct PositionOverride
		{
			public Transform bone;
			public Vector3 position;
		}

		[Header("The icon shown in the Customization UI:")]
		[Header("Imports all SkinnedMeshRenderers in hierarchy as one body.")]
		[Header("Warning: Requires the ModToolExtension!")]
		public Texture2D icon;

		[Header("Hide and overlay the following parts:")]
		public bool overlayBody = true;
		public bool overlayEyelash = true;

		[Header("Override the local position of certain bones if necessary:")]
		public PositionOverride[] positionOverrides;
	}

	public class InteractiveAnimation : StateMachineBehaviour
	{
		override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
		{
			animator.SetFloat("X", Input.mousePosition.x / Screen.width);
			animator.SetFloat("Y", Input.mousePosition.y / Screen.height);
		}
	}
}
