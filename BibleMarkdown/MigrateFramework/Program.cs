using System.Text.RegularExpressions;
using System.Text;

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

var path = Directory.GetCurrentDirectory();
var src = Path.Combine(path, "src", "framework.md");

var txt = File.ReadAllText(src);
int nmarker = 0;
int nfootnote = 0;

txt = Regex.Replace(txt, @"(?<title>#+\s+.*?\r?\n)|(?<verse>\^[0-9]+\^)|(?<marker>\^[a-zA-Z]*\^)|(?<footnote>\^(?<label>[a-zA-Z]*)\^?\[(?<note>.*?)\])|(?<paragraph>\\)", m => {
	if (m.Groups["marker"].Success && m.Groups["marker"].Value == "^^")
	{
		return $"^{Label(nmarker++)}^";
	}
	else if (m.Groups["footnote"].Success && m.Groups["label"].Value == "")
	{
		return $"^{Label(nfootnote++)}^[{m.Groups["note"].Value}]";
	}
	else if (m.Groups["title"].Success || m.Groups["paragraph"].Success)
	{
		nmarker = 0; nfootnote = 0;
	}
	return m.Value;
}, RegexOptions.Singleline);

File.WriteAllText(src, txt);
Console.WriteLine($"{src} migrated.");
