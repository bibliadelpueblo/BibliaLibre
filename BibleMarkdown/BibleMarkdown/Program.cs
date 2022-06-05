using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Data.SqlTypes;
using System.Xml;
using System.Xml.Linq;
using Pandoc;
using System.Runtime.InteropServices;

namespace BibleMarkdown
{
	partial class Program
	{

		static DateTime bibmarktime;
		static bool LowercaseFirstWords = false;
		static bool FromSource = false;
		static bool Imported = false;
		static string Language = null;
		static string Replace = null;

		public struct Footnote
		{
			public int Index;
			public int FIndex;
			public string Value;

			public Footnote(int Index, int FIndex, string Value)
			{
				this.Index = Index;
				this.FIndex = FIndex;
				this.Value = Value;
			}
		}

		static void Log(string file)
		{
			var current = Directory.GetCurrentDirectory();
			if (file.StartsWith(current))
			{
				file = file.Substring(current.Length);
			}
			Console.WriteLine($"Created {file}.");
		}
		public static bool IsNewer(string file, string srcfile)
		{
			var srctime = DateTime.MaxValue;
			if (File.Exists(srcfile)) srctime = File.GetLastWriteTimeUtc(srcfile);
			var filetime = DateTime.MinValue;
			if (File.Exists(file)) filetime = File.GetLastWriteTimeUtc(file);
			return filetime > srctime && filetime > bibmarktime;
		}

		static string Label(int i)
		{
			if (i == 0) return "a";
			StringBuilder label = new StringBuilder();
			while (i > 0)
			{
				var ch = (char)(((int)'a') + i % 26);
				label.Append(ch);
				i = i / 26;
			}
			return label.ToString();
		}

		static Task ProcessFileAsync(string file)
		{
			var path = Path.GetDirectoryName(file);
			var md = Path.Combine(path, "out", "pandoc");
			var mdtex = Path.Combine(md, "tex");
			var mdepub = Path.Combine(md, "epub");
			var tex = Path.Combine(path, "out", "tex");
			var html = Path.Combine(path, "out", "html");
			if (!Directory.Exists(md)) Directory.CreateDirectory(md);
			if (!Directory.Exists(mdtex)) Directory.CreateDirectory(mdtex);
			if (!Directory.Exists(mdepub)) Directory.CreateDirectory(mdepub);
			if (!Directory.Exists(tex)) Directory.CreateDirectory(tex);
			if (!Directory.Exists(html)) Directory.CreateDirectory(html);
			var mdfile = Path.Combine(md, Path.GetFileName(file));
			var texfile = Path.Combine(tex, Path.GetFileNameWithoutExtension(file) + ".tex");
			var htmlfile = Path.Combine(html, Path.GetFileNameWithoutExtension(file) + ".html");
			var epubfile = Path.Combine(mdepub, Path.GetFileNameWithoutExtension(file) + ".md");

			var mdfiletime = DateTime.MinValue;
			var epubfiletime = DateTime.MinValue;
			var texfiletime = DateTime.MinValue;
			var htmlfiletime = DateTime.MinValue;
			var filetime = File.GetLastWriteTimeUtc(file);

			Task TeXTask = Task.CompletedTask, HtmlTask = Task.CompletedTask;

			CreatePandoc(file, mdfile);
			CreateEpub(mdfile, epubfile);
			return Task.WhenAll(CreateTeXAsync(mdfile, texfile), CreateHtmlAsync(mdfile, htmlfile));
		}


		static void ProcessPath(string path)
		{
			var srcpath = Path.Combine(path, "src");
			var outpath = Path.Combine(path, "out");
			if (!Directory.Exists(outpath)) Directory.CreateDirectory(outpath);
			if (Directory.Exists(srcpath))
			{
				Location.ImportMap(Path.Combine(srcpath, "versemap.md"));
				ImportFromUSFM(path, srcpath);
				ImportFromTXT(path, srcpath);
				ImportFromZefania(path, srcpath);
				ImportFramework(path);
			}
			CreateFramework(path);
			CreateVerseStats(path);
			var files = Directory.EnumerateFiles(path, "*.md");
			Task.WaitAll(files.Select(file => ProcessFileAsync(file)).ToArray());
		}

		static void InitPandoc()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) PandocInstance.SetPandocPath("pandoc.exe");
			else PandocInstance.SetPandocPath("pandoc");
		}
		static void Main(string[] args)
		{

			// Get the version of the current application.
			var asm = Assembly.GetExecutingAssembly();
			var aname = asm.GetName();
			Console.WriteLine($"{aname.Name}, v{aname.Version.Major}.{aname.Version.Minor}.{aname.Version.Build}.{aname.Version.Revision}");

			InitPandoc();
			var exe = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			bibmarktime = File.GetLastWriteTimeUtc(exe);

			LowercaseFirstWords = args.Contains("-plc");
			FromSource = args.Contains("-s") || args.Contains("-src") || args.Contains("-source");
			var lnpos = Array.IndexOf(args, "-ln");
			if (lnpos >= 0 && (lnpos + 1 < args.Length)) Language = args[lnpos + 1];

			var replacepos = Array.IndexOf(args, "-replace");
			if (replacepos == -1) replacepos = Array.IndexOf(args, "-r");
			if (replacepos >= 0 && replacepos + 1 < args.Length) Replace = args[replacepos + 1];

			var paths = args.ToList();
			for (int i = 0; i < paths.Count; i++)
			{
				if (paths[i] == "-ln" || paths[i] == "-replace" || paths[i] == "-r")
				{
					paths.RemoveAt(i); paths.RemoveAt(i); i--;
				} else if (paths[i].StartsWith('-'))
				{
					paths.RemoveAt(i); i--;
				}
			}
			string path;
			if (paths.Count == 0)
			{
				path = Directory.GetCurrentDirectory();
				ProcessPath(path);
			} else
			{
				path = paths[0];
				if (Directory.Exists(path))
				{
					ProcessPath(path);
				}
				else if (File.Exists(path)) ProcessFileAsync(path).Wait();
			}
		}
	}
}
