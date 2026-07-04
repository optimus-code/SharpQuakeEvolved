using SharpQuake.Framework;
using System;

namespace SharpQuake.Renderer.Models
{
	public abstract class BaseAliasModelDesc : BaseModelDesc
	{
		public virtual aliashdr_t AliasHeader
		{
			get;
			set;
		}

		public virtual Int32 AliasFrame
		{
			get;
			set;
		}

		// model animation interpolation
		public virtual Int32 LastPoseNumber0
		{
			get;
			set;
		}

		public virtual Int32 LastPoseNumber
		{
			get;
			set;
		}
	}
}
