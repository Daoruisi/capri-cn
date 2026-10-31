@echo off
rem 用 Windows 自带的 .NET Framework 编译器直接出 exe（无需 SDK）
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /out:CapriPet.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Pet.cs
if errorlevel 1 exit /b 1
echo Built CapriPet.exe
