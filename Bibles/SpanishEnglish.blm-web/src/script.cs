using System;
using System.Text;
using System.Text.RegularExpressions;

Program.LeftLanguage = "spanish";
Program.RightLanguage = "english";
Program.Replace = "/SEÑOR/[Señor]{.smallcaps}/LORD/[Lord]{.smallcaps}";
// replace uppercase words with smallcaps
Program.Preprocess = txt => Regex.Replace(txt, @"(?<!(^|\n)#.*?)[A-ZÑÓÍÉÁÚÄÜÖ][A-ZÑÓÍÉÁÚÄÜÖ]+", m => {
        if (m.Value == "II") return m.Value;
        
        var str = new StringBuilder("[");
        str.Append(m.Value[0]);
        for (int i = 1; i < m.Value.Length; i++) str.Append(Char.ToLower(m.Value[i]));
        str.Append("]{.smallcaps}");
        return str.ToString();
    }, RegexOptions.Singleline);
Program.PreprocessImportUSFM = txt =>
    Regex.Replace(txt, @"\\ms1.*?\r?\n", "", RegexOptions.Singleline);

Program.Log("Set languages.");
