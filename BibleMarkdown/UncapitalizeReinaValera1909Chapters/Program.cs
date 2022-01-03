using System.Text;
using System.Text.RegularExpressions;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("uncap v1.0");

var path = Directory.GetCurrentDirectory();
path = Path.Combine(path, "src");

var files = Directory.EnumerateFiles(path)
	.Where(file => Path.GetExtension(file) == ".usfm");

foreach (var file in files)
{
	var text = File.ReadAllText(file);
	var plaintext = Regex.Replace(text, @"(\\\w+\*?|\|.*?=\"".*?\"")", "");

	text = Regex.Replace(text, @"\\v\s+1\s+(.*?)\\v", match => 
	{

		var verse = match.Value;
		var plainverse = Regex.Replace(match.Groups[1].Value, @"(\\\w+\*?|\|.*?=\"".*?\"")", "");
		var m = Regex.Match(plainverse, @"((^|[.;:!?¡¿])\s*)?([A-ZÁÉÚÍÓÑ][A-ZÁÉÚÍÓÑ]+)(?!\s+[A-ZÁÉÚÍÓÑ][A-ZÁÉÚÍÓÑ]+)", RegexOptions.Singleline);
		if (m.Success)
		{
			var capword = m.Groups[3].Value;
			var lowword = new StringBuilder();
			foreach (char ch in capword) lowword.Append(char.ToLower(ch));
			var name = new StringBuilder();
			var firstchar = true;
			foreach (char ch in capword) {
				if (firstchar) name.Append(ch);
				else name.Append(char.ToLower(ch));
				firstchar = false;
			}
			var pos = verse.IndexOf(capword);

			
			if (Regex.IsMatch(plaintext, $"[^a-zA-ZáéúíóñÁÉÚÍÓÑ]{lowword}[^a-zA-ZáéúíóñÁÉÚÍÓÑ]"))
			{
				if (m.Groups[1].Success) return $"{verse.Substring(0, pos)}{name}{verse.Substring(pos + name.Length)}";
				return $"{verse.Substring(0, pos)}{lowword}{verse.Substring(pos + capword.Length)}";
			}
			else
			{
				Console.WriteLine($"Is {name} not a Name? (Y/N)");
				if (Console.ReadKey().KeyChar == 'n') return $"{verse.Substring(0, pos)}{name}{verse.Substring(pos + name.Length)}";
				else if (m.Groups[1].Success) return $"{verse.Substring(0, pos)}{name}{verse.Substring(pos + name.Length)}";
				return $"{verse.Substring(0, pos)}{lowword}{verse.Substring(pos + capword.Length)}";
			}
		} return verse;
	}, RegexOptions.Singleline);

	File.WriteAllText(file, text);
	Console.WriteLine($"Created {file}");
}