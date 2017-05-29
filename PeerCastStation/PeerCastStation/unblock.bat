@cd %~dp0
for %%i in (*.exe,*.dll) do (
echo. > %%i:Zone.Identifier
)
pause

