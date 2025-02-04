#!/bin/bash

# .gitignore dosyasındaki kurallara göre git tarafından izlenmeyen dosyaları temizle
echo "Silinecek dosyalar kontrol ediliyor..."

# git status komutunu kullanarak izlenmeyen dosyaları listele
# -uall tüm izlenmeyen dosyaları gösterir, --ignored yalnızca .gitignore'a uyanları listeler
git ls-files --ignored --exclude-standard --others -o -X .gitignore | while read -r file; do
    # Her dosyayı silelim
    if [ -e "$file" ]; then
        echo "Siliniyor: $file"
        rm -rf "$file"
    fi
done

echo "İzlenmeyen dosyalar silindi."
