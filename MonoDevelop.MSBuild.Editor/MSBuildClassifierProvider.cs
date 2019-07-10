// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (IClassifierProvider))]
	[ContentType (MSBuildContentType.Name)]
	sealed class MSBuildClassifierProvider : IClassifierProvider
	{
		[Import]
		JoinableTaskContext joinableTaskContext;
		[Import]
		IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier (ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildClassifier (buffer, classificationRegistry, joinableTaskContext));
		}
	}
}
