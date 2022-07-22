chcp 65001
cd ../Spanish.BibliaLibreParaElMundo
..\..\bin\bibmark.exe
cd ../English.WorldEnglishBibleUS
..\..\bin\bibmark.exe
cd ../SpanishEnglish.blm-web
..\..\bin\bibmark.exe

cd tex
xelatex Biblia11ptB5
del BibliaParaAprenderIngles11ptB5.pdf
ren Biblia11ptB5.pdf BibliaParaAprenderIngles11ptB5.pdf

del ..\out\BibliaParaAprenderIngles11ptB5.pdf
move BibliaParaAprenderIngles11ptB5.pdf ..\out

cd ..
