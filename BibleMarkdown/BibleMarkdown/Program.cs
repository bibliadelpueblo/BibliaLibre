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
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace BibleMarkdown
{
	partial class Program
	{

		public static DateTime bibmarktime;
		public static bool LowercaseFirstWords = false;
		public static bool EachVerseOnNewLine = true;
		public static bool FromSource = false;
		public static bool Imported = false;
		public static bool Help = false;
		public static Func<string, string> Preprocess = s => s;
		public static Func<string, string> PreprocessImportUSFM = s => s;
		static string language;
		public static string Language
		{
			get { return language; }
			set
			{
				if (value != language)
				{
					language = value;
					Log($"Language set to {language}");
				}
			}
		}
		public static string RightLanguage
		{
			get { return rightlanguage; }
			set
			{
				if (value != rightlanguage)
				{
					rightlanguage = value;
					Log($"RightLanguage set to {rightlanguage}");
				}
			}
		}
		public static string LeftLanguage
		{
			get { return leftlanguage; }
			set
			{
				if (value != leftlanguage)
				{
					leftlanguage = value;
					Log($"LeftLanguage set to {leftlanguage}");
				}
			}
		}

		static string leftlanguage;
		static string rightlanguage;
		public static bool MapVerses = false;
		public static string? Replace = null;
		public static bool TwoLanguage = false;

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

		static void LogFile(string file)
		{
			LogFile(file, "Created");
		}
		static void LogFile(string file, string label)
		{
			var current = Directory.GetCurrentDirectory();
			if (file.StartsWith(current))
			{
				file = file.Substring(current.Length);
			}
			Log($"{label} {file}");
		}

		static StringBuilder log = new StringBuilder();
		public static void Log(string text)
		{
			if (!string.IsNullOrWhiteSpace(text)) {
				lock (log) {
					log.AppendLine(text);
					Console.WriteLine(text);
				}
			}
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
			var usfm = Path.Combine(path, "out", "usfm");
			if (!Directory.Exists(md)) Directory.CreateDirectory(md);
			if (!Directory.Exists(mdtex)) Directory.CreateDirectory(mdtex);
			if (!Directory.Exists(mdepub)) Directory.CreateDirectory(mdepub);
			if (!Directory.Exists(tex)) Directory.CreateDirectory(tex);
			if (!Directory.Exists(html)) Directory.CreateDirectory(html);
			if (!Directory.Exists(usfm)) Directory.CreateDirectory(usfm);
			var mdfile = Path.Combine(md, Path.GetFileName(file));
			var texfile = Path.Combine(tex, Path.GetFileNameWithoutExtension(file) + ".tex");
			var htmlfile = Path.Combine(html, Path.GetFileNameWithoutExtension(file) + ".html");
			var epubfile = Path.Combine(mdepub, Path.GetFileNameWithoutExtension(file) + ".md");
			var usfmfile = Path.Combine(usfm, Path.GetFileNameWithoutExtension(file) + ".usfm");

			Task TeXTask = Task.CompletedTask, HtmlTask = Task.CompletedTask;

			CreatePandoc(file, mdfile);
			CreateEpub(path, mdfile, epubfile);
			CreateUSFM(mdfile, usfmfile);
			return Task.WhenAll(CreateTeXAsync(mdfile, texfile), CreateHtmlAsync(mdfile, htmlfile));
		}


		static void ProcessPath(string path)
		{
			RunScript(path);
			var srcpath = Path.Combine(path, "src");
			var outpath = Path.Combine(path, "out");
			if (!Directory.Exists(outpath)) Directory.CreateDirectory(outpath);
			if (Directory.Exists(srcpath))
			{
				VerseMaps.Load(path);
				ImportFromBibleEdit(srcpath);
				ImportFromUSFM(path, srcpath);
				ImportFromTXT(path, srcpath);
				ImportFromZefania(path, srcpath);
				ImportFramework(path);
			}
			CreateFramework(path);
			CreateVerseStats(path);
			Log("Convert to Pandoc...");
			var files = Directory.EnumerateFiles(path, "*.md");
			Task.WaitAll(files.AsParallel().Select(file => ProcessFileAsync(file)).ToArray());
			File.WriteAllText(Path.Combine(outpath, "bibmark.log"), log.ToString());
			log.Clear();
		}

		static void RunScript(string path)
		{
			var file = Path.Combine(path, "src", "script.cs");
			if (!File.Exists(file)) return;

			var txt = File.ReadAllText(file);
			LogFile(file, "Run script");

			try
			{
				var result = CSharpScript.RunAsync(txt, ScriptOptions.Default
				.WithReferences(typeof(Program).Assembly)
				.WithImports("BibleMarkdown"));
				result.Wait();
			} catch (Exception e)
			{
				Log(e.ToString());
			}
			
		}

		static void ProcessTwoLanguagesPath(string path, string path1, string path2)
		{
			TwoLanguage = true;
			var outpath = Path.Combine(path, "out");
			if (!Directory.Exists(outpath)) Directory.CreateDirectory(outpath);

			// ProcessPath(path1);
			// ProcessPath(path2);
			RunScript(path);
			Books.Load(path);
			CreateTwoLanguage(path, path1, path2);
			var files = Directory.EnumerateFiles(path, "*.md");
			Task.WaitAll(files.AsParallel().Select(file => ProcessFileAsync(file)).ToArray());
			File.WriteAllText(Path.Combine(outpath, "bibmark.log"), log.ToString());
			log.Clear();
		}
		static void InitPandoc()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Pandoc.SetPandocPath("pandoc.exe");
			else Pandoc.SetPandocPath("pandoc");
		}

		static void ShowHelp() {
			Log(@"Bibmark usage:

Bibmark converts Markdown files to various formats.
The Bibles use a specific version of Markdown, BibleMarkdown. BibleMarkdown is
normal pandoc Markdown, with the following extensions:

- You can put a marker ^letters^ at a place where you want to have a footnote, and
  put the footnote later in the text with regular Markdown ^letters^[The footnote] syntax.
  Within the footnote you can escape the [ and ] letters by [[ and ]].
- You can have comments, surrounded by /* and */ or // followed by a comment text up to the new line,
  like /*This is a comment*/ or
  // this is a comment.
  A /* */ comment can span multiple lines.
- Verse numbers are noted with superscript Markdown
  notation, like this ^1^ In the beginning was the Word and the Word was with God and
  the Word was God. ^2^ This was in the beginning...

To edit the Markdown of the Bibles, you can use a normal editor like Typora,
stackedit.io or VisualStudio Code.

Bibmark processes all the .md files in the current directory and converts them to
other formats in the ""out"" subdirectory. The md files in the current directory must
follow a naming schema, of two digits followed by a minus and the name of the
bible book, e.g. like 01-Genesis.md or 02-Exodus.md. Bibmark only processes files
with names adhering to that schema. The md files can be constructed from various
source formats. For this, the source files must be placed in the subdirectory ""src"".
In the ""src"" subdirectory you can place USFM files or zefania xml files, or a BibleEdit
folder. You can also place a script.cs file in the ""src"" folder that will be executed
when running bibmark, that can configure bibmark for certain tasks. Next you can
place a file booknames.xml in the ""src"" subdirectory that contains names of Bible
books in different languages. The names of the books should correspond to the titles
of the books in the USFM files. Then you can also import a Parallel Verses file,
linklist.xml, that contains parallel verses.

Options:
  - -s, -src or -source:
    If you want to import text from the src folder you need to specify this option when
    calling bibmark.
  - -cp:
    (Continuous Paragraph) 
    If you do not want to start each verse on a newline, but the whole paragraph on a
    single line, you can specify this option. Placing each verse on a newline is more git
    friendly, where as placing it on a single line is more readable.
  - -ln language:
    With this option you can specify a language. The language is used to determine
    Book names from booknames.xml.
  - -r or -replace replacementtext
    The first letter of repacementtext is used as token delimiter. replacementtext is
    then split into tokens. Every pair of tokens describes a global replacement directive,
    where the first token is a Regex expression, and the second token is a
    Regex replacement string.
  - -twolanguage path1 path2
    Produces a double column, two language bible, with the single language bibles
    located in path1 and path2.
  ");
		}
		
		static void Main(string[] args)
		{

			// Get the version of the current application.
			var asm = Assembly.GetExecutingAssembly();
			var aname = asm.GetName();
			Log($"{aname.Name}, v{aname.Version.Major}.{aname.Version.Minor}.{aname.Version.Build}.{aname.Version.Revision}");

			if (args.Contains("-h") || args.Contains("-help") || args.Contains("-?"))
			{
				ShowHelp();
				return;
			}

			Init();

			InitPandoc();
			//var process = Process.GetCurrentProcess();
			//var exe = process.MainModule.FileName;
			var exe = Assembly.GetExecutingAssembly().Location;
			bibmarktime = File.GetLastWriteTimeUtc(exe);

			LowercaseFirstWords = args.Contains("-plc");
			EachVerseOnNewLine = !args.Contains("-cp");
			FromSource = args.Contains("-s") || args.Contains("-src") || args.Contains("-source");
			var lnpos = Array.IndexOf(args, "-ln");
			if (lnpos >= 0 && (lnpos + 1 < args.Length)) Language = args[lnpos + 1];
			else Language = "default";

			var replacepos = Array.IndexOf(args, "-replace");
			if (replacepos == -1) replacepos = Array.IndexOf(args, "-r");
			if (replacepos >= 0 && replacepos + 1 < args.Length) Replace = args[replacepos + 1];

			var twolangpos = Array.IndexOf(args, "-twolanguage");
			if (twolangpos >= 0 && twolangpos + 2 < args.Length)
			{
				var left = args[twolangpos + 1];
				var right = args[twolangpos + 2];
				var p = Directory.GetCurrentDirectory();
				ProcessTwoLanguagesPath(p, left, right);
				return;
			}
			var paths = args.ToList();
			for (int i = 0; i < paths.Count; i++)
			{
				if (paths[i] == "-twolanguage")
				{
					paths.RemoveAt(i); paths.RemoveAt(i); paths.RemoveAt(i); i--;
				}
				else if (paths[i] == "-ln" || paths[i] == "-replace" || paths[i] == "-r")
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
