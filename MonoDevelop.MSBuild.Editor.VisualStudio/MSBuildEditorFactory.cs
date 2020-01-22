using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[ComVisible (true), Guid (Guid)]
	internal class MSBuildEditorFactory : EditorFactory
	{
		public const string Guid = "f801dba5-07b2-44dc-85bf-97cdbff58686";

		private readonly IProjectThreadingService threadingService;

		public MSBuildEditorFactory (Package package, IProjectThreadingService threadingService) : base (package)
		{
			this.threadingService = threadingService;
		}

		public override Guid GetLanguageServiceGuid ()
		{
			return new Guid(MSBuildLanguageService.Guid);
		}

		public override bool IsRegisteredExtension (string extension)
		{
			string noDotExtension = extension.StartsWith (".") ? extension.Substring (1) : extension;
			noDotExtension = noDotExtension.ToLowerInvariant ();

			if (noDotExtension == "props" || noDotExtension == "targets" || noDotExtension == "user") return true;
			else if (noDotExtension == "tasks" || noDotExtension == "overridetasks") return true;
			else if (noDotExtension.EndsWith ("proj")) return true;
			else return false;
		}
	}
}
