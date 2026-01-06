#!/bin/bash

dotnet covenant generate Gantry.sln -n Gantry -v $1 -o sbom.json || exit 1
dotnet covenant convert spdx sbom.json || exit 1
dotnet covenant convert cyclonedx sbom.json || exit 1
dotnet covenant report sbom.json || exit 1
