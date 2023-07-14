using System;
using System.Text;
using System.Text.RegularExpressions;

Program.Language = "english";
Program.Replace = "/LORD/[Lord]{.smallcaps}";
// replace uppercase words with smallcaps
Program.Preprocess = txt => Regex.Replace(txt, @"(?<!(^|\n)#.*?)[A-Z][A-Z]+", m => {

        if (Regex.IsMatch(m.Value, "^[IVXCD]+$", RegexOptions.Singleline)) {
            // is roman number
            return m.Value;
        }
        
        var str = new StringBuilder("[");
        str.Append(m.Value[0]);
        for (int i = 1; i < m.Value.Length; i++) str.Append(Char.ToLower(m.Value[i]));
        str.Append("]{.smallcaps}");
        return str.ToString();
    }, RegexOptions.Singleline);