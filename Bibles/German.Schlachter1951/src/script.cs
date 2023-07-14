using System;
using System.Text;
using System.Text.RegularExpressions;

// Epub.Links = false;
Epub.OmitParagraphs = false;
Epub.Page = book => {
    book = book+3;
    return $"ch{book:d3}.xhtml";
};

Program.Language = "german";
// replace uppercase words with smallcaps
Program.Preprocess = txt => Regex.Replace(txt, @"[A-ZÑÓÍÉÁÚÄÜÖ][A-ZÑÓÍÉÁÚÄÜÖ]+", m => {
        
        if (Regex.IsMatch(m.Value, "^[IVXCD]+$", RegexOptions.Singleline)) {
            // is roman number
            return m.Value;
        }

        var str = new StringBuilder("[");
        str.Append(m.Value[0]);
        for (int i = 1; i < m.Value.Length; i++) str.Append(Char.ToLower(m.Value[i]));
        str.Append("]{.smallcaps}");
        return str.ToString();
    });

Program.Log("Added epub rule.");
