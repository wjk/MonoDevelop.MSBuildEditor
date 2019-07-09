// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor
{
	/// <summary>
	/// Contains classification type names for XML files used by the built-in XML editor extension.
	/// </summary>
	/// <remarks>
	/// The names were obtained via use of ILSpy, as I don't think they are publicly documented anywhere.
	/// </remarks>
	internal static class XmlClassificationTypes
	{
		internal const string AttributeName = "xml - attribute name";
		internal const string AttributeQuotes = "xml - attribute quotes";
		internal const string AttributeValue = "xml - attribute value";
		internal const string CDataSection = "xml - cdata section";
		internal const string Comment = "xml - comment";
		internal const string Delimiter = "xml - delimiter";
		internal const string EntityReference = "xml - entity reference";
		internal const string Name = "xml - name";
		internal const string ProcessingInstruction = "xml - processing instruction";
		internal const string Text = "xml - text";
	}
}
