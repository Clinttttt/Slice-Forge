$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageRoot = Join-Path $repoRoot 'artifacts/packages'
$validationRoot = Join-Path $repoRoot 'artifacts/package-validation'
$fixtureRoot = Join-Path $repoRoot 'eng/package-validation'
$consumerRoot = Join-Path $validationRoot 'consumer'
$packageCache = Join-Path $validationRoot 'nuget-packages'
$expectedVersion = '0.1.0-preview.1'
$expectedRepository = 'https://github.com/Clinttttt/Slice-Forge'

$expectedDependencies = [ordered]@{
    'SliceForge.Core' = @()
    'SliceForge.Runtime' = @('SliceForge.Core', 'Microsoft.Extensions.DependencyInjection.Abstractions')
    'SliceForge.Validation' = @('SliceForge.Runtime', 'FluentValidation', 'Microsoft.Extensions.DependencyInjection.Abstractions')
    'SliceForge.AspNetCore' = @('SliceForge.Core')
    'SliceForge.Observability' = @('SliceForge.Runtime', 'Microsoft.Extensions.DependencyInjection.Abstractions', 'Microsoft.Extensions.Logging.Abstractions')
}
$expectedTags = @{
    'SliceForge.Core' = 'sliceforge core results messaging'
    'SliceForge.Runtime' = 'sliceforge runtime messaging commands queries'
    'SliceForge.Validation' = 'sliceforge validation fluentvalidation'
    'SliceForge.AspNetCore' = 'sliceforge aspnetcore results http'
    'SliceForge.Observability' = 'sliceforge observability logging tracing metrics'
}

function Assert-Condition {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-ZipEntryText {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $stream = $Entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Read-ZipEntryBytes {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $inputStream = $Entry.Open()
    $outputStream = [System.IO.MemoryStream]::new()
    try {
        $inputStream.CopyTo($outputStream)
        return ,$outputStream.ToArray()
    }
    finally {
        $inputStream.Dispose()
        $outputStream.Dispose()
    }
}

function Remove-GeneratedDirectory {
    param(
        [string] $Path,
        [string] $ExpectedPath,
        [string] $SafeRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $expectedFullPath = [System.IO.Path]::GetFullPath($ExpectedPath)
    $safeRootFullPath = [System.IO.Path]::GetFullPath($SafeRoot)
    Assert-Condition ($fullPath -eq $expectedFullPath) "Refusing to clean an unexpected generated directory: $fullPath"
    Assert-Condition ($fullPath.StartsWith($safeRootFullPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) "Refusing to clean outside the package-validation artifacts directory: $fullPath"

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Assert-Condition (Test-Path -LiteralPath $packageRoot -PathType Container) "Package feed does not exist: $packageRoot. Run dotnet pack first."

$archives = @(Get-ChildItem -LiteralPath $packageRoot -Filter 'SliceForge.*.nupkg' -File |
    Where-Object { $_.Name -notlike '*.snupkg' })
Assert-Condition ($archives.Count -eq $expectedDependencies.Count) "Expected five SliceForge .nupkg files, found $($archives.Count)."

$packageRanges = [System.Collections.Generic.List[string]]::new()
$repoPathForms = @(
    $repoRoot,
    $repoRoot.Replace('\', '/'),
    $repoRoot.Replace('/', '\')
) | Select-Object -Unique

foreach ($packageId in $expectedDependencies.Keys) {
    $packagePath = Join-Path $packageRoot "$packageId.$expectedVersion.nupkg"
    Assert-Condition (Test-Path -LiteralPath $packagePath -PathType Leaf) "Missing package $packageId $expectedVersion."

    $symbolPath = Join-Path $packageRoot "$packageId.$expectedVersion.snupkg"
    Assert-Condition (Test-Path -LiteralPath $symbolPath -PathType Leaf) "Missing symbol package for $packageId."

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) })
        Assert-Condition ($nuspecEntries.Count -eq 1) "$packageId must contain exactly one nuspec."
        [xml] $nuspec = Read-ZipEntryText $nuspecEntries[0]
        $metadata = $nuspec.SelectSingleNode("//*[local-name()='metadata']")
        Assert-Condition ($null -ne $metadata) "$packageId nuspec has no metadata element."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='id']").InnerText -eq $packageId) "$packageId nuspec ID mismatch."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='version']").InnerText -eq $expectedVersion) "$packageId nuspec version mismatch."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='authors']").InnerText -eq 'Clint Villanueva') "$packageId author metadata is missing or incorrect."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='copyright']").InnerText -eq 'Copyright (c) 2026 Clint Villanueva') "$packageId copyright metadata is missing or incorrect."
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($metadata.SelectSingleNode("./*[local-name()='description']").InnerText)) "$packageId description is missing."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='tags']").InnerText -eq $expectedTags[$packageId]) "$packageId package tags are missing or incorrect."
        Assert-Condition ($null -eq $metadata.SelectSingleNode("./*[local-name()='company']")) "$packageId must not claim company metadata."

        $license = $metadata.SelectSingleNode("./*[local-name()='license']")
        Assert-Condition ($null -ne $license -and $license.GetAttribute('type') -eq 'expression' -and $license.InnerText -eq 'Apache-2.0') "$packageId must declare the Apache-2.0 license expression."
        Assert-Condition ($null -ne $archive.GetEntry('LICENSE')) "$packageId is missing the packaged license text."
        Assert-Condition ($metadata.SelectSingleNode("./*[local-name()='readme']").InnerText -eq 'README.md') "$packageId nuspec must identify README.md as its package readme."
        Assert-Condition ($null -ne $archive.GetEntry('README.md')) "$packageId is missing the packaged README."

        $repository = $metadata.SelectSingleNode("./*[local-name()='repository']")
        Assert-Condition ($null -ne $repository -and $repository.GetAttribute('type') -eq 'git' -and $repository.GetAttribute('url') -eq $expectedRepository) "$packageId repository metadata is missing or incorrect."

        $dependencies = @($nuspec.SelectNodes("//*[local-name()='dependencies']//*[local-name()='dependency']"))
        $actualDependencyIds = @($dependencies | ForEach-Object { $_.GetAttribute('id') } | Sort-Object -Unique)
        $expectedDependencyIds = @($expectedDependencies[$packageId] | Sort-Object -Unique)
        Assert-Condition (($actualDependencyIds -join '|') -eq ($expectedDependencyIds -join '|')) "$packageId dependency IDs differ. Expected [$($expectedDependencyIds -join ', ')], got [$($actualDependencyIds -join ', ')]."

        foreach ($dependency in $dependencies) {
            $dependencyId = $dependency.GetAttribute('id')
            $dependencyVersion = $dependency.GetAttribute('version')
            $dependencyGroup = $dependency.ParentNode
            $targetFramework = $dependencyGroup.GetAttribute('targetFramework')
            Assert-Condition ($targetFramework -match '10\.0') "$packageId dependency $dependencyId is not in the net10.0 dependency group (group '$targetFramework')."
            $expectedDependencyVersion = if ($dependencyId.StartsWith('SliceForge.', [System.StringComparison]::Ordinal)) { $expectedVersion } elseif ($dependencyId -eq 'FluentValidation') { '12.1.1' } else { '10.0.12' }
            Assert-Condition ($dependencyVersion -eq $expectedDependencyVersion) "$packageId dependency $dependencyId has version '$dependencyVersion'; expected '$expectedDependencyVersion'."
            $packageRanges.Add("$packageId -> $dependencyId $dependencyVersion")
        }

        $expectedDll = "lib/net10.0/$packageId.dll"
        $expectedXml = "lib/net10.0/$packageId.xml"
        Assert-Condition ($null -ne $archive.GetEntry($expectedDll)) "$packageId is missing $expectedDll."
        Assert-Condition ($null -ne $archive.GetEntry($expectedXml)) "$packageId is missing XML API documentation at $expectedXml."
        $dllEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase) })
        $sliceForgeDllEntries = @($dllEntries | Where-Object { [System.IO.Path]::GetFileName($_.FullName).StartsWith('SliceForge.', [System.StringComparison]::OrdinalIgnoreCase) })
        Assert-Condition ($sliceForgeDllEntries.Count -eq 1 -and $sliceForgeDllEntries[0].FullName -eq $expectedDll) "$packageId contains an unexpected or embedded sibling SliceForge assembly."
        Assert-Condition (-not ($archive.Entries | Where-Object { $_.FullName.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase) })) "$packageId normal package must not contain PDB files."
        Assert-Condition (-not ($archive.Entries | Where-Object { $_.FullName.EndsWith('.cs', [System.StringComparison]::OrdinalIgnoreCase) })) "$packageId must not contain source files."
        Assert-Condition (-not ($dllEntries | Where-Object { $_.FullName -match '(?i)(\.Tests|Sample\.Api)' })) "$packageId contains a test or sample assembly."

        foreach ($entry in $archive.Entries) {
            if ($entry.Length -gt 0) {
                $bytes = Read-ZipEntryBytes $entry
                $utf8Content = [System.Text.Encoding]::UTF8.GetString($bytes)
                $utf16Content = [System.Text.Encoding]::Unicode.GetString($bytes)
                foreach ($repoPath in $repoPathForms) {
                    Assert-Condition ($utf8Content.IndexOf($repoPath, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "$packageId contains a repository-local absolute path in $($entry.FullName)."
                    Assert-Condition ($utf16Content.IndexOf($repoPath, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "$packageId contains a repository-local absolute path in $($entry.FullName)."
                }
            }
        }

        if ($packageId -eq 'SliceForge.AspNetCore') {
            $frameworkReference = $nuspec.SelectSingleNode("//*[local-name()='frameworkReference' and @name='Microsoft.AspNetCore.App']")
            Assert-Condition ($null -ne $frameworkReference) 'SliceForge.AspNetCore nuspec must declare the Microsoft.AspNetCore.App framework reference.'
            $frameworkGroup = $frameworkReference.ParentNode
            Assert-Condition ($frameworkGroup.GetAttribute('targetFramework') -match '10\.0') 'SliceForge.AspNetCore framework reference must target net10.0.'
        }
    }
    finally {
        $archive.Dispose()
    }

    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbolPath)
    try {
        $pdbEntries = @($symbolArchive.Entries | Where-Object { $_.FullName.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase) })
        Assert-Condition ($pdbEntries.Count -gt 0) "$packageId symbol package does not contain PDB symbols."
        foreach ($pdbEntry in $pdbEntries) {
            $pdbBytes = Read-ZipEntryBytes $pdbEntry
            $pdbText = [System.Text.Encoding]::UTF8.GetString($pdbBytes)
            Assert-Condition ($pdbText.IndexOf('https://raw.githubusercontent.com/Clinttttt/Slice-Forge/', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) "$packageId PDB is missing GitHub Source Link metadata."
            foreach ($repoPath in $repoPathForms) {
                Assert-Condition ($pdbText.IndexOf($repoPath, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "$packageId symbol package contains a repository-local absolute path."
            }
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

Write-Output 'Generated package dependency constraints:'
foreach ($range in $packageRanges) {
    Write-Output "  $range"
}

Remove-GeneratedDirectory $consumerRoot $consumerRoot $validationRoot
Remove-GeneratedDirectory $packageCache $packageCache $validationRoot
New-Item -ItemType Directory -Path $consumerRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packageCache -Force | Out-Null
Copy-Item -Path (Join-Path $fixtureRoot '*') -Destination $consumerRoot -Recurse -Force

$consumerProject = Join-Path $consumerRoot 'PackageValidationConsumer.csproj'
$consumerProjectXml = [xml](Get-Content -LiteralPath $consumerProject -Raw)
$consumerProjectReferences = $consumerProjectXml.SelectNodes("//*[local-name()='ProjectReference' or local-name()='Reference']")
Assert-Condition ($consumerProjectReferences.Count -eq 0) 'Package validation consumer must not contain project or assembly references.'
$packageReferences = @($consumerProjectXml.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { $_.GetAttribute('Include') })
foreach ($packageId in $expectedDependencies.Keys) {
    Assert-Condition ($packageReferences -contains $packageId) "Package consumer does not directly reference $packageId."
}

$nugetConfig = Join-Path $consumerRoot 'NuGet.Config'
$propsPath = Join-Path $consumerRoot 'Directory.Build.props'
$targetsPath = Join-Path $consumerRoot 'Directory.Build.targets'
$packagesPropsPath = Join-Path $consumerRoot 'Directory.Packages.props'
Assert-Condition ((Test-Path $nugetConfig) -and (Test-Path $propsPath) -and (Test-Path $targetsPath) -and (Test-Path $packagesPropsPath)) 'Isolated consumer configuration files were not materialized.'

Write-Output 'Restoring the package-only consumer with its isolated NuGet configuration and cache...'
Invoke-DotNet @('restore', $consumerProject, '--configfile', $nugetConfig, '--packages', $packageCache, '--verbosity', 'minimal')
Write-Output 'Building the package-only consumer...'
Invoke-DotNet @('build', $consumerProject, '--no-restore', '-c', 'Release', '--verbosity', 'minimal')
Write-Output 'Running the package-only consumer...'
Invoke-DotNet @('run', '--project', $consumerProject, '--no-build', '--no-restore', '-c', 'Release')

Write-Output 'All package contents and the isolated package-only consumer passed validation.'
