// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor
{
	class MSBuildClassifier : IClassifier, IDisposable
	{
		readonly MSBuildBackgroundParser parser;
		readonly JoinableTaskContext joinableTaskContext;
		readonly IClassificationTypeRegistryService classificationRegistry;
		ParseCompletedEventArgs<MSBuildParseResult> lastArgs;
		ITextBuffer buffer;

		public MSBuildClassifier (ITextBuffer buffer, IClassificationTypeRegistryService classificationRegistry, JoinableTaskContext joinableTaskContext)
		{
			ITextBuffer2 buffer2 = (ITextBuffer2)buffer;
			parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> (buffer2);
			parser.ParseCompleted += ParseCompleted;
			parser.GetOrParseAsync ((ITextSnapshot2)buffer2.CurrentSnapshot, CancellationToken.None); // drop the returned value on the floor
			this.classificationRegistry = classificationRegistry;
			this.joinableTaskContext = joinableTaskContext;
			this.buffer = buffer;
		}

		void ParseCompleted (object sender, ParseCompletedEventArgs<MSBuildParseResult> args)
		{
			lastArgs = args;

			joinableTaskContext.Factory.Run (async delegate {
				await joinableTaskContext.Factory.SwitchToMainThreadAsync ();
				// FIXME: figure out which spans changed, if any, and only invalidate those
				ClassificationChanged?.Invoke (this, new ClassificationChangedEventArgs (new SnapshotSpan (args.Snapshot, 0, args.Snapshot.Length)));
			});
		}

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		public void Dispose ()
		{
			parser.ParseCompleted -= ParseCompleted;
		}

		public IList<ClassificationSpan> GetClassificationSpans (SnapshotSpan sourceSpan)
		{
			// This may be assigned from another thread so capture a consistent value
			var args = lastArgs;

			var results = new List<ClassificationSpan> ();
			void AddClassificationIfRangeIntersects (Span range, string typeName)
			{
				var type = classificationRegistry.GetClassificationType (typeName);
				if (type == null) throw new ArgumentException ($"Unknown classification type '{typeName}'", nameof (typeName));

				var intersection = sourceSpan.Intersection (range);
				if (intersection.HasValue) results.Add (new ClassificationSpan (intersection.Value, type));
			}

			var parse = args.ParseResult;
			var xdoc = parse.XDocument;

			foreach (XNode matchedNode in GetNodes (sourceSpan, xdoc)) {
				if (matchedNode is XElement element) {
					AddClassificationIfRangeIntersects (new Span (element.Span.Start, 1), MSBuildClassificationTypes.XmlDelimiter);
					if (element.Name.FullName != null) AddClassificationIfRangeIntersects (new Span (element.NameSpan.Start, element.NameSpan.Length), MSBuildClassificationTypes.XmlName);

					foreach (XAttribute attribute in element.Attributes) {
						AddClassificationIfRangeIntersects (new Span (attribute.NameSpan.Start, attribute.NameSpan.Length), MSBuildClassificationTypes.XmlName);
						AddClassificationIfRangeIntersects (new Span (attribute.NameSpan.End, 2), MSBuildClassificationTypes.XmlDelimiter);
						AddClassificationIfRangeIntersects (new Span (attribute.ValueSpan.Start, attribute.ValueSpan.Length), MSBuildClassificationTypes.XmlText);

						if (attribute.Span.End > attribute.ValueSpan.End) {
							AddClassificationIfRangeIntersects (new Span (attribute.NameSpan.End, 1), MSBuildClassificationTypes.XmlDelimiter);
						}
					}

					if (element.IsSelfClosing) {
						AddClassificationIfRangeIntersects (new Span (element.Span.End - 2, 2), MSBuildClassificationTypes.XmlDelimiter);
					} else if (element.ClosingTag != null) {
						var tag = element.ClosingTag;
						AddClassificationIfRangeIntersects (new Span (tag.Span.Start, 2), MSBuildClassificationTypes.XmlDelimiter);
						AddClassificationIfRangeIntersects (new Span (tag.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
						AddClassificationIfRangeIntersects (new Span (tag.Span.Start + 2, tag.Span.End - 1), MSBuildClassificationTypes.XmlName);
						AddClassificationIfRangeIntersects (new Span (element.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
					} else {
						AddClassificationIfRangeIntersects (new Span (element.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
					}
				}
			}

			return results;
		}

		private static IEnumerable<XNode> GetNodes (Span characterSpan, XDocument sourceDocument)
		{
			if (characterSpan.IsEmpty) yield break;

			foreach (XNode node in sourceDocument.AllDescendentNodes) {
				if (characterSpan.Start <= node.Span.Start && characterSpan.End >= node.Span.End) {
					yield return node;
				}
			}
		}
	}
}
