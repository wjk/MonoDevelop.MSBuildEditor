// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Util;

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Schema
{
	static class MSBuildCompletionExtensions
	{
		public static IEnumerable<BaseInfo> GetAttributeCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv)
		{
			bool isInTarget = false;
			if (rr.LanguageElement.SyntaxKind == MSBuildSyntaxKind.Item) {
				isInTarget = rr.LanguageElement.IsInTarget (rr.XElement);
			}

			foreach (var att in rr.LanguageElement.Attributes) {
				var spat = schemas.SpecializeAttribute (att, rr.ElementName);
				if (!spat.IsAbstract) {
					if (rr.LanguageElement.SyntaxKind == MSBuildSyntaxKind.Item) {
						if (isInTarget) {
							if (spat.Name == "Update") {
								continue;
							}
						} else {
							if (spat.Name == "KeepMetadata" || spat.Name == "RemoveMetadata" || spat.Name == "KeepDuplicates") {
								continue;
							}
						}
					}
					yield return spat;
				}
			}


			if (rr.LanguageElement.SyntaxKind == MSBuildSyntaxKind.Item && tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				foreach (var item in schemas.GetMetadata (rr.ElementName, false)) {
					yield return item;
				}
			}

			if (rr.LanguageElement.SyntaxKind == MSBuildSyntaxKind.Task) {
				foreach (var parameter in schemas.GetTaskParameters (rr.ElementName)) {
					yield return parameter;

				}
			}
		}

		public static bool IsInTarget (this MSBuildLanguageElement resolvedElement, Xml.Dom.XElement element)
		{
			switch (resolvedElement.SyntaxKind) {
			case MSBuildSyntaxKind.Metadata:
				element = element?.ParentElement;
				goto case MSBuildSyntaxKind.Item;
			case MSBuildSyntaxKind.Property:
			case MSBuildSyntaxKind.Item:
				element = element?.ParentElement;
				goto case MSBuildSyntaxKind.ItemGroup;
			case MSBuildSyntaxKind.ItemGroup:
			case MSBuildSyntaxKind.PropertyGroup:
				var name = element?.ParentElement?.Name.Name;
				return string.Equals (name, "Target", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		static IEnumerable<BaseInfo> GetAbstractAttributes (this IEnumerable<IMSBuildSchema> schemas, MSBuildSyntaxKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildSyntaxKind.Item:
				return schemas.GetItems ();
			case MSBuildSyntaxKind.Task:
				return schemas.GetTasks ();
			case MSBuildSyntaxKind.Property:
				return schemas.GetProperties (false);
			case MSBuildSyntaxKind.Metadata:
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IEnumerable<BaseInfo> GetElementCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr?.LanguageElement == null) {
				yield return MSBuildLanguageElement.Get ("Project");
				yield break;
			}

			if (rr.LanguageElement.Children == null) {
				yield break;
			}

			foreach (var c in rr.LanguageElement.Children) {
				if (c.IsAbstract) {
					var abstractChildren = GetAbstractChildren (schemas, rr.LanguageElement.AbstractChild.SyntaxKind, rr.ElementName);
					if (abstractChildren != null) {
						foreach (var child in abstractChildren) {
							yield return child;
						}
					}
				} else {
					yield return c;
				}
			}
		}

		static IEnumerable<BaseInfo> GetAbstractChildren (this IEnumerable<IMSBuildSchema> schemas, MSBuildSyntaxKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildSyntaxKind.Item:
				return schemas.GetItems ();
			case MSBuildSyntaxKind.Task:
				return schemas.GetTasks ();
			case MSBuildSyntaxKind.Property:
				return schemas.GetProperties (false);
			case MSBuildSyntaxKind.Metadata:
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IReadOnlyList<BaseInfo> GetValueCompletions (
			MSBuildValueKind kind,
			MSBuildRootDocument doc,
			MSBuildResolveResult rr = null,
			ExpressionNode triggerExpression = null)
		{
			var simple = kind.GetSimpleValues (true);
			if (simple != null) {
				return simple;
			}

			switch (kind) {
			case MSBuildValueKind.TaskOutputParameterName:
				return doc.GetTaskParameters (rr.ParentName).Where (p => p.IsOutput).ToList ();
			case MSBuildValueKind.TargetName:
				return doc.GetTargets ().ToList ();
			case MSBuildValueKind.PropertyName:
				return doc.GetProperties (true).ToList ();
			case MSBuildValueKind.ItemName:
				return doc.GetItems ().ToList ();
			case MSBuildValueKind.TargetFramework:
				return FrameworkInfoProvider.Instance.GetFrameworksWithShortNames ().ToList ();
			case MSBuildValueKind.TargetFrameworkIdentifier:
				return FrameworkInfoProvider.Instance.GetFrameworkIdentifiers ().ToList ();
			case MSBuildValueKind.TargetFrameworkVersion:
				return doc.Frameworks.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkVersions (tfm.Framework)
				).ToList ();
			case MSBuildValueKind.TargetFrameworkProfile:
				return doc.Frameworks.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Framework, tfm.Version)
				).ToList ();
			case MSBuildValueKind.Configuration:
				return doc.GetConfigurations ().Select (c => new ConstantInfo (c, "")).ToList ();
			case MSBuildValueKind.Platform:
				return doc.GetPlatforms ().Select (c => new ConstantInfo (c, "")).ToList ();
			}

			var fileCompletions = GetFilenameCompletions (kind, doc, triggerExpression, 0, rr);
			if (fileCompletions != null) {
				return fileCompletions;
			}

			return null;
		}

		public static IReadOnlyList<BaseInfo> GetFilenameCompletions (
			MSBuildValueKind kind, MSBuildRootDocument doc,
			ExpressionNode triggerExpression, int triggerLength, MSBuildResolveResult rr = null)
		{
			bool includeFiles = false;
			switch (kind) {
			case MSBuildValueKind.File:
			case MSBuildValueKind.ProjectFile:
				includeFiles = true;
				break;
			case MSBuildValueKind.FileOrFolder:
				includeFiles = true;
				break;
			case MSBuildValueKind.Folder:
			case MSBuildValueKind.FolderWithSlash:
				break;
			default:
				return null;
			}

			string baseDir = null;

			if (rr.LanguageAttribute != null && rr.LanguageAttribute.SyntaxKind == MSBuildSyntaxKind.Import_Project) {
				if (rr.XElement != null) {
					var sdkAtt = rr.XElement.Attributes.Get (new Xml.Dom.XName ("Sdk"), true)?.Value;
					if (!string.IsNullOrEmpty (sdkAtt) && Microsoft.Build.Framework.SdkReference.TryParse (sdkAtt, out var sdkRef)) {
						var sdkPath = doc.RuntimeInformation.GetSdkPath (sdkRef, doc.Filename, null);
						if (!string.IsNullOrEmpty (sdkPath)) {
							baseDir = sdkPath;
						}
					}
				}
			}

			var basePaths = EvaluateExpressionAsPaths (triggerExpression, doc, triggerLength + 1, baseDir).ToList ();
			return basePaths.Count == 0 ? null : GetPathCompletions (basePaths, includeFiles);
		}

		public static IEnumerable<string> EvaluateExpressionAsPaths (ExpressionNode expression, MSBuildRootDocument doc, int skipEndChars = 0, string baseDir = null)
		{
			baseDir = baseDir ?? Path.GetDirectoryName (doc.Filename);

			if (expression == null) {
				yield return baseDir;
				yield break;
			}

			if (expression is ListExpression list) {
				expression = list.Nodes[list.Nodes.Count - 1];
			}

			if (expression is ExpressionText lit) {
				if (lit.Length == 0) {
					yield return baseDir;
					yield break;
				}
				var path = TrimEndChars (lit.GetUnescapedValue ());
				if (string.IsNullOrEmpty (path)) {
					yield return baseDir;
					yield break;
				}
				//FIXME handle encoding
				if (MSBuildEscaping.FromMSBuildPath (path, baseDir, out var res)) {
					yield return res;
				}
				yield break;
			}

			if (!(expression is ConcatExpression expr)) {
				yield break;
			}

			//FIXME evaluate directly without the MSBuildEvaluationContext
			var sb = new StringBuilder ();
			for (int i = 0; i < expr.Nodes.Count; i++) {
				var node = expr.Nodes[i];
				if (node is ExpressionText l) {
					var val = l.GetUnescapedValue ();
					if (i == expr.Nodes.Count - 1) {
						val = TrimEndChars (val);
					}
					sb.Append (val);
				} else if (node is ExpressionProperty p) {
					sb.Append ($"$({p.Name})");
				} else {
					yield break;
				}
			}

			foreach (var variant in doc.FileEvaluationContext.EvaluatePathWithPermutation (sb.ToString (), baseDir)) {
				yield return variant;
			}

			string TrimEndChars (string s) => s.Substring (0, Math.Min (s.Length, s.Length - skipEndChars));
		}

		static IReadOnlyList<BaseInfo> GetPathCompletions (List<string> completionBasePaths, bool includeFiles)
		{
			var infos = new List<BaseInfo> ();

			foreach (var basePath in completionBasePaths) {
				try {
					if (!Directory.Exists (basePath)) {
						continue;
					}
					foreach (var e in Directory.GetDirectories (basePath)) {
						var name = Path.GetFileName (e);
						infos.Add (new FileOrFolderInfo (name, true, e));
					}

					if (includeFiles) {
						foreach (var e in Directory.GetFiles (basePath)) {
							var name = Path.GetFileName (e);
							infos.Add (new FileOrFolderInfo (name, false, e));
						}
					}
				} catch (Exception ex) {
					LoggingService.LogError ($"Error enumerating paths under '{basePath}'", ex);
				}
			}

			infos.Add (new FileOrFolderInfo ("..", true, "The parent directory"));

			return infos;
		}

		public static BaseInfo GetResolvedReference (this MSBuildResolveResult rr, MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				return doc.GetItem ((string)rr.Reference);
			case MSBuildReferenceKind.Metadata:
				var m = rr.ReferenceAsMetadata;
				if (Builtins.Metadata.TryGetValue (m.metaName, out var builtinMeta)) {
					return builtinMeta;
				}
				return doc.GetMetadata (m.itemName, m.metaName, true);
			case MSBuildReferenceKind.Property:
				var propName = (string)rr.Reference;
				if (Builtins.Properties.TryGetValue (propName, out var builtinProp)) {
					return builtinProp;
				}
				return doc.GetProperty (propName);
			case MSBuildReferenceKind.Task:
				return doc.GetTask ((string)rr.Reference);
			case MSBuildReferenceKind.Target:
				return doc.GetTarget ((string)rr.Reference);
			case MSBuildReferenceKind.Keyword:
				return (BaseInfo)rr.Reference;
			case MSBuildReferenceKind.KnownValue:
				return (BaseInfo)rr.Reference;
			case MSBuildReferenceKind.TargetFramework:
				return ResolveFramework ((string)rr.Reference);
			case MSBuildReferenceKind.TargetFrameworkIdentifier:
				return BestGuessResolveFrameworkIdentifier ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkVersion:
				return BestGuessResolveFrameworkVersion ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkProfile:
				return BestGuessResolveFrameworkProfile ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TaskParameter:
				var p = rr.ReferenceAsTaskParameter;
				return doc.GetTaskParameter (p.taskName, p.paramName);
			case MSBuildReferenceKind.ItemFunction:
				//FIXME: attempt overload resolution
				return functionTypeProvider.GetItemFunctionInfo ((string)rr.Reference);
			case MSBuildReferenceKind.StaticPropertyFunction:
				//FIXME: attempt overload resolution
				(string className, string name) = ((string, string))rr.Reference;
				return functionTypeProvider.GetStaticPropertyFunctionInfo (className, name);
			case MSBuildReferenceKind.PropertyFunction:
				//FIXME: attempt overload resolution
				(MSBuildValueKind kind, string funcName) = ((MSBuildValueKind, string))rr.Reference;
				return functionTypeProvider.GetPropertyFunctionInfo (kind, funcName);
			case MSBuildReferenceKind.ClassName:
				return functionTypeProvider.GetClassInfo ((string)rr.Reference);
			case MSBuildReferenceKind.Enum:
				return functionTypeProvider.GetEnumInfo ((string)rr.Reference);
			}
			return null;
		}

		static BaseInfo ResolveFramework (string shortname)
		{
			var fullref = NuGetFramework.ParseFolder (shortname);
			if (fullref.IsSpecificFramework) {
				return new FrameworkInfo (shortname, fullref);
			}
			return null;
		}

		static BaseInfo BestGuessResolveFrameworkIdentifier (string identifier, IReadOnlyList<NuGetFramework> docTfms)
		{
			//if any tfm in the doc matches, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Framework == identifier);
			if (existing != null) {
				return new FrameworkInfo (identifier, existing);
			}
			//else take the latest known version for this framework
			return FrameworkInfoProvider.Instance.GetFrameworkVersions (identifier).LastOrDefault ();
		}

		static BaseInfo BestGuessResolveFrameworkVersion (string version, IReadOnlyList<NuGetFramework> docTfms)
		{
			if (!Version.TryParse (version.TrimStart ('v', 'V'), out Version v)) {
				return null;
			}
			//if any tfm in the doc has this version, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Version == v);
			if (existing != null) {
				return new FrameworkInfo (version, existing);
			}
			//if this matches a known version for any tfm id in the doc, take that
			foreach (var tfm in docTfms) {
				foreach (var f in FrameworkInfoProvider.Instance.GetFrameworkVersions (tfm.Framework)) {
					if (f.Reference.Version == v) {
						return f;
					}
				}
			}
			return null;
		}

		static BaseInfo BestGuessResolveFrameworkProfile (string profile, IReadOnlyList<NuGetFramework> docTfms)
		{
			//if any tfm in the doc has this profile, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Profile == profile);
			if (existing != null) {
				return new FrameworkInfo (profile, existing);
			}
			foreach (var tfm in docTfms) {
				foreach (var f in FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Framework, tfm.Version)) {
					if (string.Equals (f.Name, profile, StringComparison.OrdinalIgnoreCase)) {
						return f;
					}
				}
			}
			return null;
		}

		public static ValueInfo GetElementOrAttributeValueInfo (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr.LanguageElement == null) {
				return null;
			}

			if (rr.AttributeName != null) {
				return schemas.GetAttributeInfo (rr.LanguageAttribute, rr.ElementName, rr.AttributeName);
			}

			return schemas.GetElementInfo (rr.LanguageElement, rr.ParentName, rr.ElementName);
		}

		public static MSBuildValueKind InferValueKindIfUnknown (this ValueInfo variable)
		{
			if (variable.ValueKind != MSBuildValueKind.Unknown) {
				return variable.ValueKind;
			}

			if (variable is MSBuildLanguageAttribute att) {
				switch (att.Name) {
				case "Include":
				case "Exclude":
				case "Remove":
				case "Update":
					return MSBuildValueKind.File.List ();
				}
			}

			//assume known items are files
			if (variable.ValueKind == MSBuildValueKind.UnknownItem) {
				return MSBuildValueKind.File;
			}

			if (variable.ValueKind == MSBuildValueKind.UnknownItem.List ()) {
				return MSBuildValueKind.File.List ();
			}

			bool isProperty = variable is PropertyInfo;
			if (isProperty || variable is MetadataInfo) {
				if (StartsWith ("Enable")
					|| StartsWith ("Disable")
					|| StartsWith ("Require")
					|| StartsWith ("Use")
					|| StartsWith ("Allow")
					|| EndsWith ("Enabled")
					|| EndsWith ("Disabled")
					|| EndsWith ("Required")) {
					return MSBuildValueKind.Bool;
				}
				if (EndsWith ("DependsOn")) {
					return MSBuildValueKind.TargetName.List ();
				}
				if (EndsWith ("Path")) {
					return MSBuildValueKind.FileOrFolder;
				}
				if (EndsWith ("Paths")) {
					return MSBuildValueKind.FileOrFolder.List ();
				}
				if (EndsWith ("Directory")
					|| EndsWith ("Dir")) {
					return MSBuildValueKind.Folder;
				}
				if (EndsWith ("File")) {
					return MSBuildValueKind.File;
				}
				if (EndsWith ("FileName")) {
					return MSBuildValueKind.Filename;
				}
				if (EndsWith ("Url")) {
					return MSBuildValueKind.Url;
				}
				if (EndsWith ("Ext")) {
					return MSBuildValueKind.Extension;
				}
				if (EndsWith ("Guid")) {
					return MSBuildValueKind.Guid;
				}
				if (EndsWith ("Directories") || EndsWith ("Dirs")) {
					return MSBuildValueKind.Folder.List ();
				}
				if (EndsWith ("Files")) {
					return MSBuildValueKind.File.List ();
				}
			}

			//make sure these work even if the common targets schema isn't loaded
			if (isProperty) {
				switch (variable.Name.ToLowerInvariant ()) {
				case "configuration":
					return MSBuildValueKind.Configuration;
				case "platform":
					return MSBuildValueKind.Platform;
				}
			}

			return MSBuildValueKind.Unknown;

			bool StartsWith (string prefix) => variable.Name.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
													   && variable.Name.Length > prefix.Length
													   && char.IsUpper (variable.Name[prefix.Length]);
			bool EndsWith (string suffix) => variable.Name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase);
		}
	}
}
