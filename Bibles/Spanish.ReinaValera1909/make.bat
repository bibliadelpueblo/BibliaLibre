chcp 65001
..\..\bin\bibmark.exe
cd tex
@REM xelatex Biblia14ptB5
@REM del BibliaDelOsoPinoleroLetraGrandeB5.pdf
@REM ren Biblia14ptB5.pdf BibliaDelOsoPinoleroLetraGrandeB5.pdf
xelatex Biblia11ptB5
del BibliaDelOsoPinoleroB5.pdf
ren Biblia11ptB5.pdf BibliaDelOsoPinoleroB5.pdf

del ..\pdf\BibliaDelOsoPinoleroB5.pdf
move BibliaDelOsoPinoleroB5.pdf ..\pdf
@REM move BibliaDelOsoPinoleroLetraGrandeB5.pdf ..\pdf

cd ..