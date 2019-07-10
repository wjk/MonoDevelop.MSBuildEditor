// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	internal sealed class MSBuildClassificationTypes
	{
		public const string XmlCData = "msbuild - xml cdata";
		public const string XmlComment = "msbuild - xml comment";
		public const string XmlDelimiter = "msbuild - xml delimiter";
		public const string XmlEntityReference = "msbuild - xml entity reference";
		public const string XmlName = "msbuild - xml name";
		public const string XmlText = "msbuild - xml text";
		public const string XmlProcessingInstruction = "msbuild - xml processing instruction";
		private const string FormatLanguage = "formal language";

		#region Type Definitions

		[Export]
		[Name (XmlCData), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlCDataDefinition;

		[Export]
		[Name (XmlComment), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlCommentDefinition;

		[Export]
		[Name (XmlDelimiter), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlDelimiterDefinition;

		[Export]
		[Name (XmlEntityReference), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlEntityReferenceDefinition;

		[Export]
		[Name (XmlName), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlNameDefinition;

		[Export]
		[Name (XmlText), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlTextDefinition;

		[Export]
		[Name (XmlProcessingInstruction), BaseDefinition (FormatLanguage)]
		internal readonly ClassificationTypeDefinition XmlProcessingInstructionDefinition;

		#endregion

		#region Format Definitions

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlCData)]
		[Name (XmlCData)]
		[UserVisible (false)]
		private class XmlCDataFormat : ClassificationFormatDefinition
		{
			private XmlCDataFormat () : base ()
			{
				DisplayName = "MSBuild XML CDATA";
				ForegroundColor = Colors.Gray;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlComment)]
		[Name (XmlComment)]
		[UserVisible (false)]
		private class XmlCommentFormat : ClassificationFormatDefinition
		{
			private XmlCommentFormat () : base ()
			{
				DisplayName = "MSBuild XML Comment";
				ForegroundColor = Colors.Green;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlDelimiter)]
		[Name (XmlDelimiter)]
		[UserVisible (false)]
		private class XmlDelimiterFormat : ClassificationFormatDefinition
		{
			private XmlDelimiterFormat () : base ()
			{
				DisplayName = "MSBuild XML Delimiter";
				ForegroundColor = Colors.Blue;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlEntityReference)]
		[Name (XmlEntityReference)]
		[UserVisible (false)]
		private class XmlNameEntityReference : ClassificationFormatDefinition
		{
			private XmlNameEntityReference () : base ()
			{
				DisplayName = "MSBuild XML Entity Reference";
				ForegroundColor = Colors.Red;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlName)]
		[Name (XmlName)]
		[UserVisible (false)]
		private class XmlNameFormat : ClassificationFormatDefinition
		{
			private XmlNameFormat () : base ()
			{
				DisplayName = "MSBuild XML Name";
				ForegroundColor = Colors.Red;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlText)]
		[Name (XmlText)]
		[UserVisible (false)]
		private class XmlTextFormat : ClassificationFormatDefinition
		{
			private XmlTextFormat () : base ()
			{
				DisplayName = "MSBuild XML Text";
				ForegroundColor = Colors.Black;
			}
		}

		[Export (typeof (EditorFormatDefinition))]
		[ClassificationType (ClassificationTypeNames = XmlProcessingInstruction)]
		[Name (XmlProcessingInstruction)]
		[UserVisible (false)]
		private class XmlProcessingInstructionFormat : ClassificationFormatDefinition
		{
			private XmlProcessingInstructionFormat () : base ()
			{
				DisplayName = "MSBuild XML Processing Instruction";
				ForegroundColor = Colors.Gray;
			}
		}

		#endregion

		// This class is not meant to be instantiated.
		private MSBuildClassificationTypes () { }
	}
}
