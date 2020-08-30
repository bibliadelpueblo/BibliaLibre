using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions; 

namespace BibleMarkdown
{
	class Program
	{

		static DateTime bibmarktime;
		static bool LowercaseFirstWords = false;
		static bool Force = false;
	
		static void ImportFromUSFM(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".usfm"));

			if (sources.Any())
			{

				var mdtimes = Directory.EnumerateFiles(mdpath)
					.Select(file => File.GetLastWriteTimeUtc(file));
				var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

				var mdtime = DateTime.MinValue;
				var sourcetime = DateTime.MinValue;


				foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
				foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

				if (Force || mdtime < sourcetime)
				{

					int bookno = 1;

					foreach (var source in sources)
					{
						var src = File.ReadAllText(source);
						var bookm = Regex.Match(src, @"\\h\s+(.*?)$", RegexOptions.Multiline);
						var book = bookm.Groups[1].Value.Trim();

						src = Regex.Match(src, @"\\c\s+[0-9]+.*", RegexOptions.Singleline).Value; // remove header that is not content of a chapter
						src = src.Replace("\r", "").Replace("\n", ""); // remove newlines
						src = Regex.Replace(src, @"(?<=\\c\s+[0-9]+\s*(\\s[0-9]+\s+[^\\]*?)?)\\p", ""); // remove empty paragraph after chapter
						src = Regex.Replace(src, @"\\m?s([0-9]+)\s*([^\\]+)", m =>
						{
							int n = 1;
							int.TryParse(m.Groups[1].Value, out n);
							n++;
							return $"{new String('#', n)} {m.Groups[2].Value.Trim()}{Environment.NewLine}";
						}); // section titles
						bool firstchapter = true;
						src = Regex.Replace(src, @"\\c\s+([0-9]+\s*)", m =>
						{
							var res = firstchapter ? $"# {m.Groups[1].Value}{Environment.NewLine}" : $"{Environment.NewLine}{Environment.NewLine}# {m.Groups[1].Value}{Environment.NewLine}";
							firstchapter = false;
							return res;
						}); // chapters
						src = Regex.Replace(src, @"\\v\s+([0-9]+)", "^$1^"); // verse numbers
						src = Regex.Replace(src, @"\\(?<type>[fx])\s*[+-?]\s*(.*?)\\\k<type>\*((.*?(?=\\c))|(.*?\\p))", $"^*^$2^[$1]{Environment.NewLine}{Environment.NewLine}"); // footnotes
						src = Regex.Replace(src, @"\\p *", string.Concat(Enumerable.Repeat(Environment.NewLine, 2))); // replace new paragraph with empty line
						src = Regex.Replace(src, @"\|([a-z-]+=""[^""]*""\s*)+", ""); // remove word attributes
						src = Regex.Replace(src, @"\\\+?\w+(\*|\s*)?", ""); // remove usfm tags
						src = Regex.Replace(src, @" +", " "); // remove multiple spaces

						if (LowercaseFirstWords) // needed for ReinaValera1909, it has uppercase words on every beginning of a chapter
						{
							src = Regex.Replace(src, @"(\^1\^ \w)(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
							src = Regex.Replace(src, @"(\^1\^ \w )(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
						}

						var md = Path.Combine(mdpath, $"{bookno:D2}-{book}.md");
						bookno++;
						File.WriteAllText(md, src);
						Console.WriteLine($"Created {md}.");
					}

				}

			}
		}

		static void CreatePandoc(string file, string panfile)
		{
			var text = File.ReadAllText(file);
			text = Regex.Replace(text, @"\^\*\^(.*?)(\^\[.*?\])", "$2$1", RegexOptions.Singleline);
			if (text.Contains(@"%\verse-paragraphs")) // each verse in a separate paragraph. For use in Psalms & Proverbs
			{
				text = Regex.Replace(text, @"(\^[0-9]+\^[^#]*?)(\s*?)(?=\^[0-9]+\^)", "$1\\\n", RegexOptions.Singleline);
			}

			text = Regex.Replace(text, @"\[\](.*?)(\^\[.*?\])", "$2$1", RegexOptions.Singleline); // footnotes with []
			text = Regex.Replace(text, @"\^([0-9]+)\^", @"\bibverse{$1}", RegexOptions.Singleline); // verses
			text = Regex.Replace(text, @"(?<!^)%.*?%", "", RegexOptions.Singleline); // inline comments
			text = Regex.Replace(text, @"^%.*?$", "", RegexOptions.Multiline); // whole line comments
			text = Regex.Replace(text, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles

			/*
			text = Regex.Replace(text, @" ^# (.*?)$", @"\chapter{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^## (.*?)$", @"\section{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^### (.*?)$", @"\subsection{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^#### (.*)$", @"\subsubsection{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"\*\*(.*?)(?=\*\*)", @"\bfseries{$1}");
			text = Regex.Replace(text, @"\*([^*]*)\*", @"\emph{$1}", RegexOptions.Singleline); 
			text = Regex.Replace(text, @"\^\[([^\]]*)\]", @"\footnote{$1}", RegexOptions.Singleline);
			*/

			File.WriteAllText(panfile, text);
			Console.WriteLine($"Created {panfile}.");
		}

		static void CreateTeX(string mdfile, string texfile)
		{
			var pandoc = new ProcessStartInfo("pandoc.exe", $"-f markdown -t latex -o \"{texfile}\" \"{mdfile}\"");
			pandoc.CreateNoWindow = true;
			pandoc.WindowStyle = ProcessWindowStyle.Hidden;
			pandoc.RedirectStandardOutput = true;
			pandoc.RedirectStandardError = true;
			pandoc.UseShellExecute = false;
			var process = Process.Start(pandoc);
			process.WaitForExit();
			Console.WriteLine(process.StandardOutput.ReadToEnd());
			Console.WriteLine(process.StandardError.ReadToEnd());
			Console.WriteLine($"Created {texfile}.");
		}
		static void ProcessFile(string file)
		{
			var path = Path.GetDirectoryName(file);
			var md = Path.Combine(path, "out\\pandoc");
			var tex = Path.Combine(path, "out\\tex");
			if (!Directory.Exists(md)) Directory.CreateDirectory(md);
			if (!Directory.Exists(tex)) Directory.CreateDirectory(tex);
			var mdfile = Path.Combine(md, Path.GetFileName(file));
			var texfile = Path.Combine(tex, Path.GetFileNameWithoutExtension(file) + ".tex");

			var mdfiletime = DateTime.MinValue;
			var texfiletime = DateTime.MinValue;
			var filetime = File.GetLastWriteTimeUtc(file);

			if (File.Exists(mdfile)) mdfiletime = File.GetLastWriteTimeUtc(mdfile);
			if (mdfiletime < filetime || mdfiletime < bibmarktime)
			{
				CreatePandoc(file, mdfile);
				mdfiletime = DateTime.Now;
			}

			if (File.Exists(texfile)) texfiletime = File.GetLastWriteTimeUtc(texfile);
			if (texfiletime < mdfiletime || texfiletime < bibmarktime)
			{
				CreateTeX(mdfile, texfile);
			}
		}

		static void ProcessPath(string path)
		{
			var srcpath = Path.Combine(path, "src");
			ImportFromUSFM(path, srcpath);
			var files = Directory.EnumerateFiles(path, "*.md");
			foreach (var file in files) ProcessFile(file);
		}
		static void Main(string[] args)
		{

			// Get the version of the current application.
			var asm = Assembly.GetExecutingAssembly();
			var aname = asm.GetName();
			Console.WriteLine($"{aname.Name}, v{aname.Version.Major}.{aname.Version.Minor}.{aname.Version.Build}");

			var exe = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			bibmarktime = File.GetLastWriteTimeUtc(exe);

			LowercaseFirstWords = args.Contains("-plc");
			Force = args.Contains("-f");
			
			var paths = args.Where(a => !a.StartsWith("-")).ToArray();
			string path;
			if (paths.Length == 0)
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
				else if (File.Exists(path)) ProcessFile(path);
			}
		}
	}
}
