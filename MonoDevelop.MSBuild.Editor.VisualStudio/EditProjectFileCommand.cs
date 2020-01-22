using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MonoDevelop.MSBuild.Editor.Host;
using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Order (10)]
	[AppliesTo ("OpenProjectFile")]
	[ExportCommandGroup ("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}")]
	internal sealed class EditProjectFileCommand : IAsyncCommandGroupHandler
	{
		private readonly UnconfiguredProject unconfiguredProject;
		private readonly Lazy<IProjectThreadingService> threadingService;
		private readonly IServiceProvider serviceProvider;
		private readonly IProjectService projectService;

		[ImportMany (ExportContractNames.VsTypes.IVsHierarchy)]
		private readonly OrderPrecedenceImportCollection<IVsHierarchy> hierarchies;

		[ImportingConstructor]
		public EditProjectFileCommand (UnconfiguredProject unconfiguredProject, Lazy<IProjectThreadingService> threadingService, [Import (typeof (SVsServiceProvider))] IServiceProvider serviceProvider, IProjectService projectService)
		{
			this.unconfiguredProject = unconfiguredProject;
			this.threadingService = threadingService;
			this.serviceProvider = serviceProvider;
			this.projectService = projectService;

			hierarchies = new OrderPrecedenceImportCollection<IVsHierarchy> (projectCapabilityCheckProvider: unconfiguredProject);
		}

		public Task<CommandStatusResult> GetCommandStatusAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
		{
			if (commandId == 1632) {
				return Task.FromResult (new CommandStatusResult (true, commandText, CommandStatus.Enabled));
			} else {
				return Task.FromResult (CommandStatusResult.Unhandled);
			}
		}

		public async Task<bool> TryHandleCommandAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
		{
			if (commandId != 1632) return false;

			await threadingService.Value.SwitchToUIThread ();

			try {
				string path = unconfiguredProject.FullPath;
				var solutionService = (IVsSolution)serviceProvider.GetService (typeof (SVsSolution));
				if (solutionService == null) throw new InvalidOperationException ("Cannot get IVsSolution");
				solutionService.CloseSolutionElement ((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, hierarchies.First ().Value, 0);

				var frame = VsShellUtilities.OpenDocumentWithSpecificEditor (serviceProvider, path, new Guid (MSBuildEditorFactory.Guid), Guid.Empty);
				if (frame != null) {
					frame.Show ();
				} else {
					throw new Exception ("Could not show frame");
				}
			} catch (Exception ex) {
			}

			return true;
		}
	}
}
