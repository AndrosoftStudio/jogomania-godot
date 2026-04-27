Get-ChildItem -Path "Scenes", "Scripts", "Shaders" -Include *.cs, *.gdshader -Recurse -File | Group-Object Extension | Select-Object Name, @{Name="Linhas";Expression={($_.Group | Get-Content | Measure-Object -Line).Lines}}



PS C:\GODOT\Godot_v4.6.2-stable_mono_win64\Projects\jogomania> Get-ChildItem -Path "Scenes", "Scripts", "Shaders" -Include *.cs, *.gdshader, *.tscn -Recurse -File | Group-Object Extension | Select-Object Name, @{Name="Linhas";Expression={($_.Group | Get-Content | Measure-Object -Line).Lines}}