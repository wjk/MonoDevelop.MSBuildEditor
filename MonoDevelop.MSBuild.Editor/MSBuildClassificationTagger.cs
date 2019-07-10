// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor
{
	class MSBuildClassificationTagger : ITagger<IClassificationTag>, IDisposable
	{
		readonly MSBuildBackgroundParser parser;
		readonly JoinableTaskContext joinableTaskContext;
		readonly IClassificationTypeRegistryService classificationRegistry;
		ParseCompletedEventArgs<MSBuildParseResult> lastArgs;
		ITextBuffer buffer;

		public MSBuildClassificationTagger (ITextBuffer buffer, IClassificationTypeRegistryService classificationRegistry, JoinableTaskContext joinableTaskContext)
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
				//FIXME: figure out which spans changed, if any, and only invalidate those
				TagsChanged?.Invoke (this, new SnapshotSpanEventArgs (new SnapshotSpan (args.Snapshot, 0, args.Snapshot.Length)));
			});
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void Dispose ()
		{
			parser.ParseCompleted -= ParseCompleted;
		}

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags (NormalizedSnapshotSpanCollection spans)
		{
			//this may be assigned from another thread so capture a consistent value
			var args = lastArgs;

			if (args == null || spans.Count == 0)
				return Enumerable.Empty<ITagSpan<IClassificationTag>> ();

			var tagSpans = new List<ITagSpan<IClassificationTag>> ();
			void AddClassificationIfRangeIntersects (SnapshotSpan sourceSpan, Span range, string tagName)
			{
				var tag = classificationRegistry.GetClassificationType (tagName);
				if (tag == null) throw new ArgumentException ($"Unknown classification tag '{tagName}'", nameof (tagName));

				var intersection = sourceSpan.Intersection (range);
				if (intersection.HasValue) tagSpans.Add (new TagSpan<ClassificationTag> (intersection.Value, new ClassificationTag (tag)));
			}

			var parse = args.ParseResult;
			var snapshot = args.Snapshot;
			var xdoc = parse.XDocument;

			foreach (var inputTaggingSpan in spans) {
				SnapshotSpan taggingSpan = inputTaggingSpan;
				if (taggingSpan.Snapshot != snapshot) {
					taggingSpan = taggingSpan.TranslateTo (snapshot, SpanTrackingMode.EdgeInclusive);
				}

				foreach (XNode matchedNode in GetNodes (taggingSpan, xdoc)) {
					if (matchedNode is XElement element) {
						AddClassificationIfRangeIntersects (taggingSpan, new Span (element.Span.Start, 1), MSBuildClassificationTypes.XmlDelimiter);
						if (element.Name.FullName != null) AddClassificationIfRangeIntersects (taggingSpan, new Span (element.NameSpan.Start, element.NameSpan.Length), MSBuildClassificationTypes.XmlName);

						foreach (XAttribute attribute in element.Attributes) {
							AddClassificationIfRangeIntersects (taggingSpan, new Span (attribute.NameSpan.Start, attribute.NameSpan.Length), MSBuildClassificationTypes.XmlName);
							AddClassificationIfRangeIntersects (taggingSpan, new Span (attribute.NameSpan.End, 2), MSBuildClassificationTypes.XmlDelimiter);
							AddClassificationIfRangeIntersects (taggingSpan, new Span (attribute.ValueSpan.Start, attribute.ValueSpan.Length), MSBuildClassificationTypes.XmlText);

							if (attribute.Span.End > attribute.ValueSpan.End) {
								AddClassificationIfRangeIntersects (taggingSpan, new Span (attribute.NameSpan.End, 1), MSBuildClassificationTypes.XmlDelimiter);
							}
						}

						if (element.IsSelfClosing) {
							AddClassificationIfRangeIntersects (taggingSpan, new Span (element.Span.End - 2, 2), MSBuildClassificationTypes.XmlDelimiter);
						} else if (element.ClosingTag != null) {
							var tag = element.ClosingTag;
							AddClassificationIfRangeIntersects (taggingSpan, new Span (tag.Span.Start, 2), MSBuildClassificationTypes.XmlDelimiter);
							AddClassificationIfRangeIntersects (taggingSpan, new Span (tag.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
							AddClassificationIfRangeIntersects (taggingSpan, new Span (tag.Span.Start + 2, tag.Span.End - 1), MSBuildClassificationTypes.XmlName);
							AddClassificationIfRangeIntersects (taggingSpan, new Span (element.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
						} else {
							AddClassificationIfRangeIntersects (taggingSpan, new Span (element.Span.End - 1, 1), MSBuildClassificationTypes.XmlDelimiter);
						}
					}
				}
			}

			return tagSpans;
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
