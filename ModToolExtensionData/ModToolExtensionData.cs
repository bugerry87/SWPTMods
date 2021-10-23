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
		[Header("The icon shown in the Customization UI:")]
		[Header("Imports all SkinnedMeshRenderers in hierarchy as one body.")]
		[Header("Warning: Requires the ModToolExtension!")]
		public Texture2D icon;

		[Header("Hide and overlay the following parts:")]
		public bool hideBody = true;
		public bool hideEyelash = true;
	}
}
