// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Text;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildRefactoringProvider))]
	class UseAttributesForMetadataRefactoringProvider : MSBuildRefactoringProvider
	{
		public override Task RegisterRefactoringsAsync (MSBuildRefactoringContext context)
		{
			XElement itemElement = null;
			foreach (var el in context.ElementsinSpan) {
				switch (el.Value?.SyntaxKind) {
				case MSBuildSyntaxKind.Item:
					itemElement = el.Key;
					break;
				case MSBuildSyntaxKind.Metadata:
					itemElement = el.Key.Parent as XElement;
					break;
				}
				if (itemElement != null) {
					break;
				}
			}

			// check it isn't in an ItemDefinitionGroup
			if (!(itemElement?.Parent is XElement pe && pe.NameEquals ("ItemGroup", true))) {
				return Task.CompletedTask;
			}

			if (!IsTransformable (itemElement)) {
				return Task.CompletedTask;
			}

			context.RegisterRefactoring (new UseAttributeForMetadataAction (itemElement));

			return Task.CompletedTask;
		}

		static bool IsTransformable (XElement item)
		{
			if (!item.IsClosed || item.IsSelfClosing) {
				return false;
			}

			bool foundAny = false;

			// we can only tranform the item if its only children are metadata elements without attributes
			foreach (var node in item.Nodes) {
				if (!(node is XElement meta) || meta.Attributes.First != null) {
					return false;
				}

				// if the metadata element has a child, it must be a single text node
				if (meta.FirstChild != null && (!(meta.FirstChild is XText t) || t.NextSibling != null)) {
					return false;
				}

				//we also cannot transform if it would conflict with reserved attributes
				if (MSBuildElementSyntax.Item.GetAttribute (meta.Name.Name) is MSBuildAttributeSyntax att && !att.IsAbstract) {
					return false;
				}
				foundAny = true;
			}

			return foundAny;
		}

		class UseAttributeForMetadataAction : SimpleMSBuildAction
		{
			readonly XElement itemElement;

			public UseAttributeForMetadataAction (XElement itemElement)
			{
				this.itemElement = itemElement;
			}

			public override string Title => $"Use attributes for metadata";
			protected override MSBuildActionOperation CreateOperation ()
			{
				var insertionPoint = (itemElement.Attributes.Last?.Span ?? itemElement.NameSpan).End;

				var sb = new StringBuilder ();
				foreach (var node in itemElement.Nodes) {
					var meta = (XElement)node;
					sb.Append (" ");
					sb.Append (meta.Name);
					sb.Append ("=\"");
					var val = (meta.FirstChild as XText)?.Text ?? "";
					if (val.IndexOf ('\'') < 0) {
						val = val.Replace ("\"", "'");
					} else {
						val = val.Replace ("\"", "&quot;");
					}
					sb.Append (val);
					sb.Append ("\"");
				}

				sb.Append (" />");

				return new EditTextActionOperation ()
					.Replace (
						TextSpan.FromBounds (insertionPoint, itemElement.ClosingTag.Span.End),
						sb.ToString ()
					);
			}
		}
	}
}
