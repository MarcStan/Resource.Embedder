﻿using Microsoft.Build.Framework;
using System.IO;

namespace ResourceEmbedder.MsBuild
{
	public abstract class MsBuildTask : Microsoft.Build.Utilities.Task
	{
		#region Properties

		[Required]
		public string AssemblyPath { set; get; }

		public string KeyFilePath { get; set; }

		[Required]
		public string ProjectDirectory { get; set; }

		public bool SignAssembly { get; set; }

		public string TargetPath { get; set; }

		#endregion Properties

		protected bool AssertSetup(Core.ILogger logger)
		{
			if (!Directory.Exists(ProjectDirectory))
			{
				logger.Error("Project directory '{0}' does not exist.", ProjectDirectory);
				return false;
			}
			var asm = Path.Combine(ProjectDirectory, AssemblyPath);
			if (!File.Exists(asm))
			{
				logger.Error("Assembly '{0}' not found", asm);
				return false;
			}
			return true;
		}
	}
}