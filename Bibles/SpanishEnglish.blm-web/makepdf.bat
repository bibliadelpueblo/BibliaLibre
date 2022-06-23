chcp 65001
cd ../Spanish.BibliaLibreParaElMundo
..\..\bin\bibmark.exe
cd ../English.WorldEnglishBibleUS
..\..\bin\bibmark.exe
cd ../SpanishEnglish.blm-web
..\..\bin\bibmark.exe

cd tex
@REM xelatex Biblia14ptB5
@REM del BibliaDelPinoleroLetraGrandeB5.pdf
@REM ren Biblia14ptB5.pdf BibliaDelPinoleroLetraGrandeB5.pdf
xelatex Biblia11ptB5
del BibliaParaAprenderInglesB5.pdf
ren Biblia11ptB5.pdf BibliaParaAprenderInglesB5.pdf


del ..\pdf\BibliaParaAprenderInglesB5.pdf
move BibliaParaAprenderInglesB5.pdf ..\pdf
@REM move BibliaDelPinoleroLetraGrandeB5.pdf ..\pdf

cd ..
