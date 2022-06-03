chcp 65001
..\..\bin\bibmark.exe

cd out\pandoc
pandoc -o ..\..\epub\BibliaDelPinolero.epub 00.0-Metadata.epub.md 00.1-Impressum.epub.md 00.2-Prefacio.epub.md 02-Génesis.epub.md 03-Éxodo.epub.md 04-Levítico.epub.md 05-Números.epub.md 06-Deuteronomio.epub.md 07-Josué.epub.md 08-Jueces.epub.md 09-Rut.epub.md "10-1 Samuel.epub.md" "11-2 Samuel.epub.md" "12-1 Reyes.epub.md" "13-2 Reyes.epub.md" "14-1 Crónicas.epub.md" "15-2 Crónicas.epub.md" 16-Esdras.epub.md 17-Nehemías.epub.md 18-Ester.epub.md 19-Job.epub.md 20-Salmos.epub.md 21-Proverbios.epub.md 22-Eclesiastés.epub.md "23-Cantar de los Cantares.epub.md" 24-Isaías.epub.md 25-Jeremías.epub.md 26-Lamentaciones.epub.md

cd ..\..\tex
@REM xelatex Biblia14ptB5
@REM del BibliaDelPinoleroLetraGrandeB5.pdf
@REM ren Biblia14ptB5.pdf BibliaDelPinoleroLetraGrandeB5.pdf
xelatex Biblia11ptB5
del BibliaDelPinoleroB5.pdf
ren Biblia11ptB5.pdf BibliaDelPinoleroB5.pdf

del ..\pdf\BibliaDelPinoleroB5.pdf
move BibliaDelPinoleroB5.pdf ..\pdf
@REM move BibliaDelPinoleroLetraGrandeB5.pdf ..\pdf

cd ..
