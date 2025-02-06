# 📺 ShadeBox

[![Boyut](https://img.shields.io/github/repo-size/shadesofdeath/ShadeBox?logo=git&logoColor=white&label=Boyut)](#)
[![Görüntülenme](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https://github.com/shadesofdeath/ShadeBox&title=Görüntülenme)](#)
[![Build](https://github.com/shadesofdeath/ShadeBox/actions/workflows/build.yml/badge.svg)](https://github.com/shadesofdeath/KekikStream/actions/workflows/build.yml)

*Dizi, film ve anime izleyebileceğiniz açık kaynak kodlu masaüstü uygulaması..*

[![SS](https://github.com/shadesofdeath/ShadeBox/raw/main/.github/images/SS.jpg?raw=True)](https://github.com/shadesofdeath/ShadeBox/releases/tag/latest)

[![ForTheBadge made-with-csharp](https://ForTheBadge.com/images/badges/made-with-c-sharp.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![ForTheBadge built-with-love](https://ForTheBadge.com/images/badges/built-with-love.svg)](https://GitHub.com/ShadeBox/)

## 🌐 Telif Hakkı ve Lisans

* *Copyright (C) 2025 by* [shadesofdeath](https://github.com/shadesofdeath) ❤️️
* [GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007](https://github.com/shadesofdeath/ShadeBox/blob/main/LICENSE) *Koşullarına göre lisanslanmıştır..*

***

### Linux (Bottles) Bağımlılıklar

- `dotnetcoredesktop9`
- `d3dcompiler_47`

#### Şişeye Font Kurulumu

> Fontları Şişeye Kopyalayın
```bash
cp /usr/share/fonts/noto/NotoSans-*.ttf ~/.var/app/com.usebottles.bottles/data/bottles/bottles/ShadeBox/drive_c/windows/Fonts/.
```

> Fontları Regedit e Kaydedin
> 
> `NotoFonts.reg`
```reg
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Fonts]
"Noto Sans Regular (TrueType)"="C:\\windows\\Fonts\\NotoSans-Regular.ttf"
"Noto Sans Bold (TrueType)"="C:\\windows\\Fonts\\NotoSans-Bold.ttf"
"Noto Sans Italic (TrueType)"="C:\\windows\\Fonts\\NotoSans-Italic.ttf"
```