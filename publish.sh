#!/bin/bash

version=1.0.0
dotnet restore || exit 1
dotnet list package --vulnerable --include-transitive || exit 1

# Windows
dotnet publish --no-restore -o dist/win   -c Release -r win-x64   -p Version=$version -p:PublishSingleFile=true --self-contained true || exit 1
zip dist/gantry-$version-win-x64.zip dist/win/* || exit 1

# Linux
dotnet publish --no-restore -o dist/linux -c Release -r linux-x64 -p Version=$version -p:PublishSingleFile=true --self-contained true || exit 1

# generic tar ball
tar -czf dist/gantry-$version-linux-x64.tar.gz dist/linux/* LICENSE.md gantry.png || exit 1

# Apt Package
mkdir -p package/deb/usr/local/bin/gantry || exit 1
cp dist/linux/* package/deb/usr/local/bin/gantry || exit 1
cp ./gantry.png package/deb/usr/local/bin/gantry || exit 1
cp ./LICENSE.md package/deb/usr/local/bin/gantry || exit 1

chmod +x package/deb/usr/local/bin/gantry/Gantry || exit 1
chmod 644 package/deb/usr/share/applications/gantry.desktop || exit 1

sed -i "s/^Version: .*/Version: $version/" package/deb/DEBIAN/control || exit 1

dpkg-deb --build package/deb dist/gantry-$version-amd64.deb || exit 1
