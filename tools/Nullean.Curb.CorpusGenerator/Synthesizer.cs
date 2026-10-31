using System.Text;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>The kind of project a file is written for, which decides what it may use.</summary>
public enum ProjectKind
{
	/// <summary>Multi-targeted with netstandard2.0, so only what that framework can compile.</summary>
	Abstractions,
	Core,
	Services,
	Cli,
	Tests,
}

/// <summary>Writes one compilable C# file: header, usings, namespace, and a few types.</summary>
public static class Synthesizer
{
	public const string Header = "// Licensed to the Corpus authors under one or more agreements.\n" +
		"// The Corpus authors license this file to you under the MIT license.";

	private static readonly string[] UnusedUsings =
	[
		"System.Text",
		"System.Globalization",
		"System.Diagnostics",
		"System.Text.RegularExpressions",
		"System.Collections.Concurrent",
		"System.Buffers",
		"System.Numerics",
		"System.Reflection",
	];

	/// <summary>The project-relative namespace a file's types go in.</summary>
	public static string NamespaceFor(ProjectKind kind, string folder) => "Corpus." + kind + (folder.Length > 0 ? "." + folder : "");

	/// <summary>Writes a file holding roughly <paramref name="members"/> members.</summary>
	public static string File(Rng r, ProjectKind kind, string ns, int members, string tag = "")
	{
		var c = new Ctx(r, ns, tag);
		var netStandard = kind == ProjectKind.Abstractions;
		var body = new StringBuilder();

		if (kind == ProjectKind.Tests)
		{
			body.Append(TestTemplates.TestClass(c, Math.Max(2, members / 2)));
		}
		else
		{
			var serviceClasses = Math.Max(1, members / 18);
			var perClass = members / serviceClasses;
			for (var i = 0; i < serviceClasses; i++)
			{
				body.Append(Templates.ServiceClass(c, perClass, netStandard)).Append('\n');
			}
			for (var i = r.Next(0, 3); i > 0; i--)
			{
				body.Append(Templates.OtherType(c, netStandard)).Append('\n');
			}
		}

		var file = new StringBuilder();
		if (r.Chance(0.7))
		{
			file.Append(Header).Append("\n\n");
		}

		var usings = new SortedSet<string>(c.Usings, StringComparer.Ordinal);
		if (netStandard)
		{
			foreach (var u in new[] { "System", "System.Collections.Generic", "System.Linq", "System.IO", "System.Threading", "System.Threading.Tasks" })
			{
				usings.Add(u);
			}
		}
		else if (r.Chance(0.25))
		{
			usings.Add("System");
		}

		var unused = UnusedUsings.Where(u => !usings.Contains(u) && !(netStandard && u == "System.Buffers")).ToArray();
		if (unused.Length > 0 && r.Chance(0.3))
		{
			usings.Add(r.Pick(unused));
		}
		if (kind != ProjectKind.Abstractions)
		{
			usings.Add("Corpus.Abstractions");
		}

		var ordered = usings.ToList();
		if (r.Chance(0.2) && ordered.Count > 1)
		{
			var i = r.Next(ordered.Count);
			(ordered[0], ordered[i]) = (ordered[i], ordered[0]);
		}

		foreach (var u in ordered)
		{
			file.Append("using ").Append(u).Append(";\n");
		}
		file.Append('\n');

		if (r.Chance(0.08))
		{
			file.Append("#nullable enable\n\n");
		}

		if (r.Chance(0.7))
		{
			file.Append("namespace ").Append(ns).Append(";\n\n").Append(body);
		}
		else
		{
			file.Append("namespace ").Append(ns).Append("\n{\n").Append(body).Append("}\n");
		}

		return file.ToString();
	}
}
