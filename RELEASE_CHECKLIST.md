# Release Checklist (local)

## 1) Verify build
```powershell
dotnet build "C:\Users\gleb\source\repos\Mod1\BurglinGnomesGrandpaMod\BurglinGnomesGrandpaMod.csproj" -v minimal
```

## 2) Pack release artifacts
```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\gleb\source\repos\Mod1\scripts\pack-release.ps1" -Version 1.6.0
```

## 3) Commit (do not push)
```powershell
git add .
git commit -m "release: prepare v1.6.0"
```

## 4) Create local tag (do not push)
```powershell
git tag -a v1.6.0 -m "v1.6.0"
```

## 5) Push manually later
- You will push branch and tags yourself.


