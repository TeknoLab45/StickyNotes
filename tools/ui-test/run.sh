#!/bin/bash
cd "$(dirname "$0")"
powershell -NoProfile -STA -Command "Invoke-Expression ([IO.File]::ReadAllText('ui.ps1') + [Environment]::NewLine + [IO.File]::ReadAllText('$1'))"
