﻿using System;
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

namespace BibleMarkdown
{
	class Program
	{

		static DateTime bibmarktime;
		static bool LowercaseFirstWords = false;
		static bool Force = false;
		static string Language = null;

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
						src = Regex.Replace(src, @"\\(?<type>[fx])\s*[+-?]\s*(.*?)\\\k<type>\*(.*?(?=\s*#|\\p))", $"^*^$2{Environment.NewLine}^[$1]"); // footnotes
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

		public static void ImportFromTXT(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".txt"));

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
					string book = null;
					string chapter = null;
					string md;

					var s = new StringBuilder();
					foreach (var source in sources)
					{
						var src = File.ReadAllText(source);
						var matches = Regex.Matches(src, @"(?:^|\n)(.*?)\s*([0-9]+):([0-9]+)(.*?)(?=$|\n[^\n]*?[0-9]+:[0-9]+)", RegexOptions.Singleline);

						foreach (Match m in matches)
						{
							var bk = m.Groups[1].Value;
							if (book != bk)
							{
								if (book != null)
								{
									md = Path.Combine(mdpath, $"{bookno++:D2}-{book}.md");
									File.WriteAllText(md, s.ToString());
									Console.WriteLine($"Created {md}.");
									s.Clear();
								}
								book = bk;
							}

							var chap = m.Groups[2].Value;
							if (chap != chapter)
							{
								chapter = chap;
								if (chapter != "1")
								{
									s.AppendLine();
									s.AppendLine();
								}
								s.AppendLine($"# {chapter}");
							}

							string verse = m.Groups[3].Value;
							string text = Regex.Replace(m.Groups[4].Value, @"\r?\n", " ").Trim();
							s.Append($"{(verse == "1" ? "" : " ")}^{verse}^ {text}");
						}
					}
					md = Path.Combine(mdpath, $"{bookno++:D2}-{book}.md");
					File.WriteAllText(md, s.ToString());
					Console.WriteLine($"Created {md}.");
				}
			}
		}

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
		static void CreatePandoc(string file, string panfile)
		{
			var text = File.ReadAllText(file);
			var exit = false;
			while (!exit) {
				var txt = Regex.Replace(text, @"(\^\^)(.*?)(\^\[.*?\])", "$3$2", RegexOptions.Singleline); // ^^ footnotes
				if (txt == text) exit = true;
				text = txt;
			} 


			if (text.Contains(@"%!verse-paragraphs%")) // each verse in a separate paragraph. For use in Psalms & Proverbs
			{
				text = Regex.Replace(text, @"(\^[0-9]+\^[^#]*?)(\s*?)(?=\^[0-9]+\^)", "$1\\\n", RegexOptions.Singleline);
			}

			text = Regex.Replace(text, @"\[\](.*?)(\^\[.*?\])", "$2$1", RegexOptions.Singleline); // footnotes with []
			text = Regex.Replace(text, @"\^([0-9]+)\^", @"\bibverse{$1}", RegexOptions.Singleline); // verses
			text = Regex.Replace(text, @"(?<!^)%.*?%", "", RegexOptions.Singleline); // comments
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
			var exe = Environment.GetEnvironmentVariable("PATH")
				 .Split(';')
				 .Select(s => Path.Combine(s, "pandoc.exe"))
				 .Where(s => File.Exists(s))
				 .FirstOrDefault();
			
			var pandoc = new ProcessStartInfo(exe, $"-f markdown -t latex -o \"{texfile}\" \"{mdfile}\"");
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

		static void CreateHtml(string mdfile, string htmlfile)
		{

			var mdhtmlfile = Path.ChangeExtension(mdfile, ".html.md");

			var src = File.ReadAllText(mdfile);
			src = Regex.Replace(src, @"\\bibverse\{([0-9]+)\}", "<sup>$1</sup>", RegexOptions.Singleline);
			File.WriteAllText(mdhtmlfile, src);
			Console.WriteLine($"Created {mdhtmlfile}.");

			var exe = Environment.GetEnvironmentVariable("PATH")
				 .Split(';')
				 .Select(s => Path.Combine(s, "pandoc.exe"))
				 .Where(s => File.Exists(s))
				 .FirstOrDefault();

			var pandoc = new ProcessStartInfo(exe, $"-f markdown -t html -o \"{htmlfile}\" \"{mdhtmlfile}\"");
			pandoc.CreateNoWindow = true;
			pandoc.WindowStyle = ProcessWindowStyle.Hidden;
			pandoc.RedirectStandardOutput = true;
			pandoc.RedirectStandardError = true;
			pandoc.UseShellExecute = false;
			var process = Process.Start(pandoc);
			process.WaitForExit();
			Console.WriteLine(process.StandardOutput.ReadToEnd());
			Console.WriteLine(process.StandardError.ReadToEnd());
			Console.WriteLine($"Created {htmlfile}.");
		}

		static void ProcessFile(string file)
		{
			var path = Path.GetDirectoryName(file);
			var md = Path.Combine(path, "out\\pandoc");
			var tex = Path.Combine(path, "out\\tex");
			var html = Path.Combine(path, "out\\html");
			if (!Directory.Exists(md)) Directory.CreateDirectory(md);
			if (!Directory.Exists(tex)) Directory.CreateDirectory(tex);
			if (!Directory.Exists(html)) Directory.CreateDirectory(html);
			var mdfile = Path.Combine(md, Path.GetFileName(file));
			var texfile = Path.Combine(tex, Path.GetFileNameWithoutExtension(file) + ".tex");
			var htmlfile = Path.Combine(html, Path.GetFileNameWithoutExtension(file) + ".html");

			var mdfiletime = DateTime.MinValue;
			var texfiletime = DateTime.MinValue;
			var htmlfiletime = DateTime.MinValue;
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

			if (File.Exists(htmlfile)) htmlfiletime = File.GetLastWriteTimeUtc(htmlfile);
			if (htmlfiletime < mdfiletime || htmlfiletime < bibmarktime)
			{
				CreateHtml(mdfile, htmlfile);
				htmlfiletime = DateTime.Now;
			}
		}

		static void CreateVerseFrame(string path)
		{
			var sources = Directory.EnumerateFiles(path, "*.md")
				.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));
			var verses = new StringBuilder();

			bool firstsrc = true;
			int btotal = 0;
			foreach (var source in sources) {

				if (!firstsrc) verses.AppendLine();
				firstsrc = false;
				verses.AppendLine($"# {Path.GetFileName(source)}");

				var txt = File.ReadAllText(source);

				int chapter = 0;
				int verse = 0;
				int nverses = 0;
				int totalverses = 0;
				var matches = Regex.Matches(txt, @"((^|\n)# ([0-9]+))|(\^([0-9]+)\^(?!\s*[#\^$]))");
				foreach (Match m in matches)
				{
					if (m.Groups[1].Success)
					{
						int.TryParse(m.Groups[3].Value, out chapter);
						if (verse != 0)
						{
							verses.Append(verse);
							verses.Append(' ');
						}
						verses.Append(chapter); verses.Append(':');
						totalverses += nverses;
						nverses = 0;
					} else if (m.Groups[4].Success)
					{
						int.TryParse(m.Groups[5].Value, out verse);
						nverses = Math.Max(nverses, verse);

					}
				}
				if (verse != 0) verses.Append(verse);
				totalverses += nverses;
				nverses = 0;
				verses.Append("; "); verses.Append(totalverses);
				btotal += totalverses;
				totalverses = 0;
				nverses = 0;
				verse = 0;
				chapter = 0;
			}

			verses.AppendLine(); verses.AppendLine(); verses.AppendLine(btotal.ToString());

			var frames = Path.Combine(path, @"out\verses.md");
			File.WriteAllText(frames, verses.ToString());
			Console.WriteLine($"Created {frames}");
		}

		static void CreateFrame(string path)
		{
			var sources = Directory.EnumerateFiles(path, "*.md")
				.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));
			var verses = new StringBuilder();

			var frames = Path.Combine(path, @"out\frames.md");

			var linklistfile = $@"{path}\src\linklist.xml";
			var namesfile = $@"{path}\src\bnames.xml";
			XElement[] refs;
			XElement[] bnames;
			int refi = 0;
			bool newrefs = false;
			if (File.Exists(linklistfile) && ((!File.Exists(frames)) || File.GetLastWriteTimeUtc(linklistfile) > File.GetLastWriteTimeUtc(frames)))
			{
				newrefs = true;
				var list = XElement.Load(File.OpenRead(linklistfile));
				var language = ((string)list.Element("collection").Attribute("id"));
				bnames = XElement.Load(File.OpenRead(namesfile))
					.Elements("ID")
					.Where(id => ((string)id.Attribute("descr")) == language)
					.FirstOrDefault()
					.Elements("BOOK")
					.ToArray();

				refs = list.Descendants("verse")
					.OrderBy(link => (int)link.Attribute("bn"))
					.ThenBy(link => (int)link.Attribute("cn"))
					.ThenBy(link => (int)link.Attribute("vn"))
					.ToArray();

			} else
			{
				refs = new XElement[0];
				bnames = new XElement[0];
			}

			bool firstsrc = true;
			foreach (var source in sources)
			{
				if (!firstsrc) verses.AppendLine();
				firstsrc = false;
				verses.AppendLine($"# {Path.GetFileName(source)}");
				int book;
				int.TryParse(Regex.Match(Path.GetFileName(source), "^[0-9][0-9]").Value, out book);


				var txt = File.ReadAllText(source);

				bool firstchapter = true;
				int nchapter = 0;
				var chapters = Regex.Matches(txt, @"(?<!#)#(?!#)(\s*([0-9]*).*?)\r?\n(.*?)(?=(?<!#)#(?!#)|$)", RegexOptions.Singleline);
				foreach (Match chapter in chapters)
				{
					nchapter++;
					int.TryParse(chapter.Groups[2].Value, out nchapter);

					if (!firstchapter) verses.AppendLine();
					firstchapter = false;
					verses.AppendLine($"## {nchapter}");

					string strip;
					if (newrefs) strip = @"(\^\^)|(.\[.*?\](\(.*?\))?[ \t]*\r?\n?)";
					else strip = @"[^\^]\[.*?\](\(.*?\))?[ \t]*\r?\n?";
					var rawch = Regex.Replace(chapter.Groups[3].Value, strip, ""); // remove markdown tags

					var ms = Regex.Matches(rawch, @"\^([0-9]+)\^|(\^\^)|(\^\[([^\]]*)\])|(?<=\r?\n)(\r?\n)(?!\s*?(\^\[|#|$))|(?<=\r?\n|^)(##.*?)(?=\r?\n|$)", RegexOptions.Singleline);
					string vers = "0";
					string lastvers = null;
					StringBuilder footnotes = new StringBuilder();
					foreach (Match m in ms)
					{	
						if (m.Groups[1].Success)
						{
							vers = m.Groups[1].Value;
							int nvers = 0;
							int.TryParse(vers, out nvers);
							if (refi < refs.Length)
							{
								XElement r = refs[refi];

								while ((refi+1 < refs.Length) && (((int)r.Attribute("bn") < book) || 
									((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") < nchapter) ||
									((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") == nchapter) && ((int)r.Attribute("vn") < nvers)))
								{
									r = refs[refi++];
								}
								if (((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") == nchapter) && ((int)r.Attribute("vn") == nvers)) {
									if (lastvers != vers) verses.Append($@"^{vers}^ ");
									lastvers = vers;
									verses.Append("^^ ");
									footnotes.Append($"^[**{nchapter}:{nvers}**");
									bool firstlink = true;
									foreach (var link in r.Elements("link"))
									{
										var bookname = bnames.FirstOrDefault(b => ((int)b.Attribute("bnumber")) == ((int)link.Attribute("bn")));
										if (bookname != null)
										{
											var bshort = (string)bookname.Attribute("bshort");
											if (!firstlink) footnotes.Append(';');
											else firstlink = false;
											footnotes.Append($" {bshort} {(string)link.Attribute("cn1")},{(string)link.Attribute("vn1")}");
											if (link.Attribute("vn2") != null) footnotes.Append($"-{(string)link.Attribute("vn2")}");
										}
									}
									footnotes.Append("] ");
								}
							}
						} else if (m.Groups[2].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							verses.Append("^^ ");
						}
						else if (m.Groups[3].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							verses.Append(m.Groups[3].Value); verses.Append(' ');
						}
						else if (m.Groups[5].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							if (footnotes.Length > 0)
							{
								verses.Append(footnotes);
								verses.Append(' ');
								footnotes.Clear();
							}
							verses.Append("\\ ");
						} else if (m.Groups[7].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							if (footnotes.Length > 0)
							{
								verses.Append(footnotes);
								verses.Append(' ');
								footnotes.Clear();
							}
							verses.AppendLine($"{Environment.NewLine}#{m.Groups[7].Value.Trim()}");
						}
					}
					if (footnotes.Length > 0)
					{
						verses.Append(footnotes);
						verses.Append(' ');
						footnotes.Clear();
					}
					}
				}

			File.WriteAllText(frames, verses.ToString());
			Console.WriteLine($"Created {frames}");
		}

		static void ImportFrame(string path)
		{
			var frmfile = Path.Combine(path, @"src\frames.md");

			if (File.Exists(frmfile))
			{

				var mdfiles = Directory.EnumerateFiles(path, "*.md")
					.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));

				var mdtimes = mdfiles.Select(file => File.GetLastWriteTimeUtc(file));
				var frmtime = File.GetLastWriteTimeUtc(frmfile);

				var frame = File.ReadAllText(frmfile);

				if (Force || mdtimes.Any(time => time < frmtime))
				{

					foreach (string srcfile in mdfiles)
					{

						File.SetLastWriteTimeUtc(srcfile, DateTime.Now);
						var src = File.ReadAllText(srcfile);
						var srcname = Path.GetFileName(srcfile);

						var frmpartmatch = Regex.Match(frame, $@"(?<=(^|\n)# {srcname}\r?\n).*?(?=\n# |$)", RegexOptions.Singleline);
						if (frmpartmatch.Success)
						{

							// remove current frame
							src = Regex.Replace(src, @"(?<=\r?\n|^)\r?\n(?!\s*#)", @""); // remove blank line
							src = Regex.Replace(src, @"(?<=^|\n)##+.*?\r?\n", ""); // remove titles
							src = Regex.Replace(src, @"(\s*\^\^)|(([ \t]*\^\[[^\]]*\])+([ \t]*\r?\n)?)", "", RegexOptions.Singleline); // remove footnotes

							var frmpart = frmpartmatch.Value;
							var frames = Regex.Matches(frmpart, @"(?<=(^|\n)## ([0-9]+)(\r?\n|$).*?)\^([0-9]+)\^((\s*(\^\^|\^\[[^\]]*\]))*\s*((\r?\n#((#+)\s*(.*?))(\r?\n|$))|\\|(?=\^[0-9]+\^)))", RegexOptions.Singleline).GetEnumerator();
							var hasFrame = frames.MoveNext();

							int chapter = 0;
							int verse = 0;
							src = Regex.Replace(src, @"(?<=^|\n)#\s+([0-9]+)(\s*\r?\n|$)|\^([0-9]+)\^.*?(?=\^[0-9]+\^|\s*#)", m =>
							{
								if (m.Groups[1].Success) // chapter
								{
									int.TryParse(m.Groups[1].Value, out chapter); verse = 0;
								}
								else if (m.Groups[3].Success) // verse
								{
									int.TryParse(m.Groups[3].Value, out verse);
								}

								if (hasFrame)
								{
									var f = (Match)frames.Current;
									int fchapter = 0;
									int fverse = 0;
									int.TryParse(f.Groups[2].Value, out fchapter);
									int.TryParse(f.Groups[4].Value, out fverse);

									if (fchapter <= chapter && fverse <= verse)
									{
										hasFrame = frames.MoveNext();
										var res = new StringBuilder(m.Value);
										if (f.Groups[5].Value.Contains("^^"))
										{
											if (!char.IsWhiteSpace(m.Value[m.Value.Length - 1])) res.Append(" ");
											res.Append("^^ ");
										}
										var foots = Regex.Matches(f.Groups[5].Value, @"\^\[[^\]]*\]");
										bool hasFoots = false;
										foreach (Match foot in foots) {
											if (hasFoots) res.Append(" ");
											else res.AppendLine();
											res.Append(foot.Value);
											hasFoots = true;
										}
										if (f.Groups[5].Value.Contains("\\")) { res.AppendLine(); res.AppendLine(); }
										else if (f.Groups[9].Success && f.Groups[11].Value != "#") 
											if (m.Groups[1].Success)
											{
												return $"{res.ToString()}{f.Groups[10].Value}{Environment.NewLine}";
											} else
											{
												return $"{res.ToString()}{Environment.NewLine}{Environment.NewLine}{f.Groups[10].Value}{Environment.NewLine}"; // add title
											}
										//if (hasFoots) res.AppendLine();
										return res.ToString();
									}
								}
								return m.Value;
							}, RegexOptions.Singleline);

							File.WriteAllText(srcfile, src);
							Console.WriteLine($"Created {srcfile}.");
						}
					}
				}
			}
		}

		static void ProcessPath(string path)
		{
			var srcpath = Path.Combine(path, "src");
			ImportFromUSFM(path, srcpath);
			ImportFromTXT(path, srcpath);
			ImportFrame(path);
			var files = Directory.EnumerateFiles(path, "*.md");
			foreach (var file in files) ProcessFile(file);
			CreateFrame(path);
			CreateVerseFrame(path);
		}
		static void Main(string[] args)
		{

			// Get the version of the current application.
			var asm = Assembly.GetExecutingAssembly();
			var aname = asm.GetName();
			Console.WriteLine($"{aname.Name}, v{aname.Version.Major}.{aname.Version.Minor}.{aname.Version.Build}.{aname.Version.Revision}");

			var exe = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			bibmarktime = File.GetLastWriteTimeUtc(exe);

			LowercaseFirstWords = args.Contains("-plc");
			Force = args.Contains("-f");
			var lnpos = Array.IndexOf(args, "-ln");
			if (lnpos >= 0 && (lnpos + 1 < args.Length)) Language = args[lnpos + 1];

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
