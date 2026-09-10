$ErrorActionPreference = "Stop"

$srcRoot = Join-Path $PSScriptRoot "..\frontend\react-app\src"

$files = @(
    "pages\CropPlanningPage.tsx",
    "pages\InspectionsPage.tsx",
    "pages\LoginPage.tsx",
    "pages\ResourcesPage.tsx",
    "pages\TaskApprovalPage.tsx"
)

foreach ($file in $files) {
    $path = Join-Path $srcRoot $file
    $content = Get-Content $path -Raw
    $content = $content.Replace("import { FormEvent, useEffect, useMemo, useState } from 'react'", "import { useEffect, useMemo, useState } from 'react'`r`nimport type { FormEvent } from 'react'")
    $content = $content.Replace("import { FormEvent, useEffect, useState } from 'react'", "import { useEffect, useState } from 'react'`r`nimport type { FormEvent } from 'react'")
    $content = $content.Replace("import { FormEvent, useState } from 'react'", "import { useState } from 'react'`r`nimport type { FormEvent } from 'react'")
    Set-Content -Path $path -Value $content -Encoding UTF8
}

$viteConfigPath = Join-Path $PSScriptRoot "..\frontend\react-app\vite.config.ts"
$viteConfig = Get-Content $viteConfigPath -Raw
$viteConfig = $viteConfig.Replace("import { defineConfig } from 'vite'", "import { defineConfig } from 'vitest/config'")
Set-Content -Path $viteConfigPath -Value $viteConfig -Encoding UTF8
