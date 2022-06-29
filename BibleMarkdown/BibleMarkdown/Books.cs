using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	public class Book
	{
		public string Language;
		public string Name;
		public string Abbreviation;
		public int Number;
	}

	public class BookList : SortedList<string, SortedList<string, Book>>
	{

		public static string? Language => Program.Language;

		public Book this[string language, int number]
		{
			get
			{
				return base[language]
					.FirstOrDefault(b => b.Value.Number == number)
					.Value;
			}
		}
		public Book this[string language, string bookname]
		{
			get
			{
				return base[language][bookname];
			}
		}

		public Book this[int number] => this[Language, number];

		public void Load(string path)
		{
			if (Count != 0) return;

			var namesfile = Path.Combine(path, "src", "bnames.xml");

			XElement xml;
			using (var file = File.Open(namesfile, FileMode.Open, FileAccess.Read))
			{
				xml = XElement.Load(file);
			}

			Program.Language = (string?)xml.Attribute("default") ?? Language;

			var languages = xml
				.Elements("ID")
				.Select(langset => new
				{
					Language = (string)langset.Attribute("descr"),
					Books = langset.Elements("BOOK")
						.Select(xml => new Book
						{
							Language = Language,
							Name = xml.Value,
							Abbreviation = (string)xml.Attribute("bshort"),
							Number = (int)xml.Attribute("bn")
						})
				});

			foreach (var lang in languages)
			{
				var books = new SortedList<string, Book>();
				Add(lang.Language, books);
				foreach (var book in lang.Books)
				{
					books.Add(book.Name, book);
				}
			}
		}

		public IEnumerable<Book> All => Values.SelectMany(lang => lang.Values);

		public static BookList Books = new BookList();
	}

	public struct ParallelVerse
	{
		public Location Verse;
		public Location[] ParallelVerses;
	}

	public class ParallelVerseList : List<ParallelVerse>
	{
		public void Load(string path)
		{
			if (Count != 0) return;

			var linklistfile = Path.Combine(path, "src", "linklist.xml");

			BookList.Books.Load(path);

			XElement xml;
			using (var file = File.Open(linklistfile, FileMode.Open, FileAccess.Read))
			{
				xml = XElement.Load(file);
			}
			var verses = xml
				.Descendants("verse")
				.Select(x =>
				{

					var book = BookList.Books[(int)x.Attribute("bn")];
					return new ParallelVerse
					{
						Verse = new Location
						{
							Book = book,
							Chapter = (int)x.Attribute("cn"),
							Verse = (int)x.Attribute("vn"),
							UpToVerse = 0
						},
						ParallelVerses = x.Elements("link")
							.Select(link => new Location
							{
								Book = book,
								Chapter = (int)link.Attribute("cn"),
								Verse = (int)link.Attribute("vn1"),
								UpToVerse = link.Attribute("vn2") != null ? (int)link.Attribute("vn2") : 0
							})
							.ToArray()
					};
				})
				.OrderBy(v => v.Verse.Book.Number)
				.ThenBy(v => v.Verse.Chapter)
				.ThenBy(v => v.Verse.Verse);

			foreach (var verse in verses) Add(verse);
		}

		public static ParallelVerseList ParallelVerses = new ParallelVerseList();
	}
	public class VerseMaps : Dictionary<string, SortedList<Location, Location>>
	{
		public static VerseMaps ParallelVerses = new VerseMaps();
		public static VerseMaps Paragraphs = new VerseMaps();
		public static VerseMaps Titles = new VerseMaps();
		public static VerseMaps Footnotes = new VerseMaps();
		public static VerseMaps DualLanguage = new VerseMaps();
		public static string Test = "";

		public Location Map(Location verse)
		{
			SortedList<Location, Location> book;
			if (!TryGetValue(verse.Book.Name, out book)) return verse;
			int i = 0, j = book.Count, m = 0;
			Location key = null, dest;
			while (i != j)
			{
				m = i + j / 2;
				key = book.Keys[m];
				if (key.Chapter > verse.Chapter || key.Chapter == verse.Chapter && key.Verse > verse.Verse)
				{
					j = m;
				}
				else
				{
					i = m;
				}
			}
			dest = book.Values[m];
			if (key == null) return verse;
			if ((key.Chapter <= verse.Chapter || key.Chapter == verse.Chapter && key.Verse <= verse.Verse))
			{
				var loc = new Location
				{
					Book = verse.Book,
					Chapter = verse.Chapter - key.Chapter + dest.Chapter,
					Verse = verse.Verse - key.Verse + dest.Verse,
				};
				if (loc.Chapter != verse.Chapter || loc.Verse != verse.Verse)
				{
					Console.WriteLine($"Verse mapped from {verse.Book} {verse.Chapter}:{verse.Verse} to {loc.Chapter}:{loc.Verse}.");
				}
				if (verse.UpToVerse != 0)
				{
					var upto = new Location
					{
						Book = verse.Book,
						Chapter = verse.Chapter,
						Verse = verse.UpToVerse,
						UpToVerse = 0
					};
					upto = Map(upto);
					if (upto.Chapter != loc.Chapter)
					{
						loc.UpToVerse = 0;
					}
					else
					{
						loc.UpToVerse = upto.Verse;
					}
				}
				return loc;
			}
			else
			{
				return verse;
			}
		}


		public void Import(string mapfile)
		{
			if (!File.Exists(mapfile)) return;

			Clear();

			var src = File.ReadAllText(mapfile);
			var books = Regex.Matches(mapfile, @"(?<=^|\n)#\s+(?<book>.*?\r?\n)(?<map>.*?)(?=\r?\n#\s)", RegexOptions.Singleline)
				.Select(m => new
				{
					Book = m.Groups["book"].Value,
					Map = m.Groups["map"].Value
				});
			foreach (var book in books)
			{
				var bookfromlist = BookList.Books[book.Book];

				var map = Regex.Matches(book.Map, @"([0-9]+):([0-9]+)=>([0-9]+):([0-9]+)", RegexOptions.Singleline)
					.Select(m => new
					{
						From = new Location()
						{
							Book = bookfromlist,
							Chapter = int.Parse(m.Groups[1].Value),
							Verse = int.Parse(m.Groups[2].Value)
						},
						To = new Location()
						{
							Book = bookfromlist,
							Chapter = int.Parse(m.Groups[3].Value),
							Verse = int.Parse(m.Groups[4].Value)
						}
					})
					.ToArray();

				if (map.Any())
				{
					var list = new SortedList<Location, Location>();
					Add(book.Book, list);
					foreach (var node in map)
					{
						list.Add(node.From, node.To);
					}
				}
			}
		}

		public static void Load(string path)
		{
			path = Path.Combine(path, "src");
			ParallelVerses.Import(Path.Combine(path, "parallelversesmap.md"));
			Paragraphs.Import(Path.Combine(path, "paragraphsmap.md"));
			Titles.Import(Path.Combine(path, "titlesmap.md"));
			Footnotes.Import(Path.Combine(path, "footnotesmap.md"));
			DualLanguage.Import(Path.Combine(path, "duallanguagemap.md"));

		}
	}

	public class Location
	{
		public Book Book;
		public int Chapter;
		public int Verse;
		public int UpToVerse;
	}

	partial class Program
	{
		public static BookList Books => BookList.Books;
		public static ParallelVerseList ParallelVerses => ParallelVerseList.ParallelVerses;
	}
}