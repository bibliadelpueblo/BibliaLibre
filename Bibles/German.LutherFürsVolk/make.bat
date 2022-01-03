chcp 65001
..\..\bin\Release\net6.0\bibmark.exe -replace /HErrn/[Herrn]{.smallcaps}/HErr/[Herr]{.smallcaps}
cd tex
@REM xelatex Bibel14ptB5
@REM del BibelGrossdruckB5.pdf
@REM ren Bibel14ptB5.pdf BibelLutherGrossdruckB5.pdf
xelatex Bibel11ptB5
del BibelLutherGrossdruckB5.pdf
ren Bibel11ptB5.pdf BibelLutherGrossdruckB5.pdf

del ..\pdf\BibelLutherGrossdruckB5.pdf
move BibelLutherGrossdruckB5.pdf ..\pdf
@REM move BibliaDelPinoleroLetraGrandeB5.pdf ..\pdf

cd ..