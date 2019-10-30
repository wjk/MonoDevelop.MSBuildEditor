// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	[Export]
	class MSBuildRefactoringService
	{
		readonly MSBuildRefactoringProvider[] refactoringProviders;

		[ImportingConstructor]
		public MSBuildRefactoringService (
			[ImportMany(typeof(MSBuildRefactoringProvider))] MSBuildRefactoringProvider[] refactoringProviders
			)
		{
			this.refactoringProviders = refactoringProviders;
		}

		public async Task<bool> HasRefactorings (MSBuildParseResult result, SnapshotSpan range, CancellationToken cancellationToken)
		{
			bool foundRefactoring = false;
			void ReportRefactoring (MSBuildAction a) => foundRefactoring = true;

			var context = new MSBuildRefactoringContext (
				result.MSBuildDocument,
				new Xml.Dom.TextSpan (range.Start, range.Length),
				ReportRefactoring,
				cancellationToken);

			foreach (var provider in refactoringProviders) {
				if (cancellationToken.IsCancellationRequested) {
					return false;
				}
				await provider.RegisterRefactoringsAsync (context);
				if (foundRefactoring) {
					return true;
				}
			}

			return false;
		}

		public async Task<List<MSBuildCodeFix>> GetRefactorings (
			MSBuildParseResult result,
			SnapshotSpan range,
			CancellationToken cancellationToken)
		{
			var refactorings = new List<MSBuildCodeFix> ();
			void ReportRefactoring (MSBuildAction a)
			{
				lock (refactorings) {
					refactorings.Add (new MSBuildCodeFix (a, ImmutableArray<MSBuildDiagnostic>.Empty));
				}
			}

			var context = new MSBuildRefactoringContext (
				result.MSBuildDocument,
				new Xml.Dom.TextSpan (range.Start, range.Length),
				ReportRefactoring,
				cancellationToken);

			foreach (var provider in refactoringProviders) {
				if (cancellationToken.IsCancellationRequested) {
					return null;
				}
				await provider.RegisterRefactoringsAsync (context);
			}

			return refactorings;
		}
	}
}
