// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (ITaggerProvider))]
	[TagType (typeof (IClassificationTag))]
	[ContentType (MSBuildContentType.Name)]
	sealed class MSBuildClassificationTaggerProvider : ITaggerProvider
	{
		[Import]
		JoinableTaskContext joinableTaskContext;
		[Import]
		IStandardClassificationService classificationService;

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
		{
			return (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildClassificationTagger (buffer, classificationService, joinableTaskContext));
		}
	}
}
