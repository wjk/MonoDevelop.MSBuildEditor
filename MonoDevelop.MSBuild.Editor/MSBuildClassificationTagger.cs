// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Dom;
using System.Linq;
using System.Threading;

namespace MonoDevelop.MSBuild.Editor
{
	class MSBuildClassificationTagger : ITagger<IClassificationTag>, IDisposable
	{
		readonly MSBuildBackgroundParser parser;
		readonly JoinableTaskContext joinableTaskContext;
		readonly IStandardClassificationService classificationService;
		ParseCompletedEventArgs<MSBuildParseResult> lastArgs;
		ITextBuffer buffer;

		public MSBuildClassificationTagger (ITextBuffer buffer, IStandardClassificationService classificationService, JoinableTaskContext joinableTaskContext)
		{
			ITextBuffer2 buffer2 = (ITextBuffer2)buffer;
			parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> (buffer2);
			parser.ParseCompleted += ParseCompleted;
			parser.GetOrParseAsync ((ITextSnapshot2)buffer2.CurrentSnapshot, CancellationToken.None); // drop the returned value on the floor
			this.classificationService = classificationService;
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
				yield break;

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
						var range = new Span (element.Span.Start, 1);
						if (taggingSpan.IntersectsWith (range)) {
							var overlap = taggingSpan.Intersection (range);
							if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
						}

						range = new Span (element.NameSpan.Start, element.NameSpan.Length);
						if (taggingSpan.IntersectsWith (range)) {
							var overlap = taggingSpan.Intersection (range);
							if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.SymbolDefinition));
						}

						foreach (XAttribute attribute in element.Attributes) {
							range = new Span (attribute.NameSpan.Start, attribute.NameSpan.Length);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.SymbolDefinition));
							}

							range = new Span (attribute.NameSpan.End, 2);
							if (range.End <= attribute.Span.End && taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}

							range = new Span (attribute.ValueSpan.Start, attribute.ValueSpan.Length);
							if (!range.IsEmpty && taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.StringLiteral));
							}

							if (attribute.Span.End > attribute.ValueSpan.End) {
								range = new Span (attribute.ValueSpan.End, 1);
								if (taggingSpan.IntersectsWith (range)) {
									var overlap = taggingSpan.Intersection (range);
									if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
								}
							}
						}

						if (element.IsSelfClosing) {
							range = new Span (element.Span.End - 2, 2);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}
						} else if (element.ClosingTag != null) {
							var tag = element.ClosingTag;

							range = new Span (tag.Span.Start, 2);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}

							range = new Span (tag.Span.End - 1, 1);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}

							range = new Span (tag.Span.Start + 2, tag.Span.End - 1);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.SymbolDefinition));
							}

							range = new Span (element.Span.End - 1, 1);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}
						} else {
							range = new Span (element.Span.End - 1, 1);
							if (taggingSpan.IntersectsWith (range)) {
								var overlap = taggingSpan.Intersection (range);
								if (overlap.HasValue) yield return new TagSpan<ClassificationTag> (overlap.Value, new ClassificationTag (classificationService.Operator));
							}
						}
					}
				}
			}
		}

		private static IEnumerable<XNode> GetNodes (Span characterSpan, XDocument sourceDocument)
		{
			if (characterSpan.IsEmpty) yield break;

			foreach (XNode node in sourceDocument.AllDescendentNodes) {
				if (characterSpan.Start >= node.Span.Start) {
					if (characterSpan.End <= node.Span.End) {
						yield return node;
					}
				}
			}
		}
	}
}
