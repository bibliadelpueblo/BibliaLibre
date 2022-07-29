chcp 65001
..\..\bin\bibmark.exe -twolanguage ..\Spanish.BibliaLibreParaElMundo ..\English.WorldEnglishBibleUS

cd tex
xelatex Biblia9ptB5
del BibliaParaAprenderIngles9ptB5.pdf
ren Biblia9ptB5.pdf BibliaParaAprenderIngles9ptB5.pdf

del ..\out\BibliaParaAprenderIngles9ptB5.pdf
move BibliaParaAprenderIngles9ptB5.pdf ..\out

cd ..
