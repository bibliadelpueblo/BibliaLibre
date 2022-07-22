chcp 65001
..\..\bin\bibmark.exe

cd tex
@REM xelatex Biblia14ptB5
xelatex Biblia11ptB5

move Biblia11ptB5.pdf ..\out
@REM move BibliaDelPinoleroLetraGrandeB5.pdf ..\out

cd ..
