$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageRoot = Join-Path $repoRoot 'artifacts/packages'
$validationRoot = Join-Path $repoRoot 'artifacts/package-validation'
$fixtureRoot = Join-Path $repoRoot 'eng/package-validation'
$consumerRoot = Join-Path $validationRoot 'consumer'
$packageCache = Join-Path $validationRoot 'nuget-packages'
$generatedRoot = Join-Path $validationRoot 'generated'
$generatedAppRoot = Join-Path $generatedRoot 'DispatchFlow'
$templateHive = Join-Path $validationRoot 'template-hive'
$cliValidationRoot = Join-Path $validationRoot 'cli'
$expectedVersion = '0.1.0-preview.2'
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

function Invoke-Tool {
    param(
        [string] $Executable,
        [string[]] $Arguments
    )

    $output = & $Executable @Arguments 2>&1 | Out-String
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

Assert-Condition (Test-Path -LiteralPath $packageRoot -PathType Container) "Package feed does not exist: $packageRoot. Run dotnet pack first."

$archives = @(Get-ChildItem -LiteralPath $packageRoot -Filter 'SliceForge.*.nupkg' -File |
    Where-Object { $_.Name -notlike '*.snupkg' })
Assert-Condition ($archives.Count -eq ($expectedDependencies.Count + 2)) "Expected five library packages, one template package, and one CLI tool package, found $($archives.Count) SliceForge .nupkg files."

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

$templatePackageId = 'SliceForge.Templates'
$templatePackagePath = Join-Path $packageRoot "$templatePackageId.$expectedVersion.nupkg"
Assert-Condition (Test-Path -LiteralPath $templatePackagePath -PathType Leaf) 'The SliceForge.Templates package is missing.'
Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $packageRoot "$templatePackageId.$expectedVersion.snupkg"))) 'The content-only template package must not produce a symbol package.'

$templateArchive = [System.IO.Compression.ZipFile]::OpenRead($templatePackagePath)
try {
    $templateNuspecEntries = @($templateArchive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) })
    Assert-Condition ($templateNuspecEntries.Count -eq 1) 'SliceForge.Templates must contain exactly one nuspec.'
    [xml] $templateNuspec = Read-ZipEntryText $templateNuspecEntries[0]
    $templateMetadata = $templateNuspec.SelectSingleNode("//*[local-name()='metadata']")
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='id']").InnerText -eq $templatePackageId) 'SliceForge.Templates nuspec ID mismatch.'
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='version']").InnerText -eq $expectedVersion) 'SliceForge.Templates version mismatch.'
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='authors']").InnerText -eq 'Clint Villanueva') 'SliceForge.Templates author metadata is missing or incorrect.'
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='title']").InnerText -eq 'SliceForge Templates') 'SliceForge.Templates title metadata is missing or incorrect.'
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='license']").InnerText -eq 'Apache-2.0') 'SliceForge.Templates must declare Apache-2.0 licensing.'
    $templateTypes = @($templateMetadata.SelectNodes("./*[local-name()='packageTypes']/*[local-name()='packageType']") | ForEach-Object { $_.GetAttribute('name') })
    Assert-Condition ($templateTypes.Count -eq 1 -and $templateTypes[0] -eq 'Template') 'SliceForge.Templates must declare PackageType=Template.'
    Assert-Condition ($templateMetadata.SelectSingleNode("./*[local-name()='readme']").InnerText -eq 'README.md') 'SliceForge.Templates nuspec must identify README.md as its package readme.'
    Assert-Condition ($null -ne $templateArchive.GetEntry('README.md')) 'SliceForge.Templates is missing the packaged README.'
    Assert-Condition ($null -ne $templateArchive.GetEntry('LICENSE')) 'SliceForge.Templates is missing the packaged license text.'
    $templateProject = Join-Path $repoRoot 'templates/SliceForge.Templates/SliceForge.Templates.csproj'
    $templateProduct = & dotnet msbuild $templateProject '-getProperty:Product'
    Assert-Condition ($LASTEXITCODE -eq 0 -and $templateProduct.Trim() -eq 'SliceForge') 'SliceForge.Templates must inherit Product=SliceForge from centralized package metadata.'

    $requiredTemplateEntries = @(
        'content/sliceforge-api/.template.config/template.json',
        'content/sliceforge-api/Directory.Build.props',
        'content/sliceforge-api/Directory.Packages.props',
        'content/sliceforge-api/TemplateApp.slnx',
        'content/sliceforge-api/src/TemplateApp.Api/TemplateApp.Api.csproj',
        'content/sliceforge-api/src/TemplateApp.Api/Program.cs',
        'content/sliceforge-api/src/TemplateApp.Api/Features/Examples/GetExample/GetExampleQuery.cs',
        'content/sliceforge-api/src/TemplateApp.Api/Features/Examples/GetExample/GetExampleHandler.cs',
        'content/sliceforge-api/src/TemplateApp.Api/Features/Examples/GetExample/GetExampleEndpoint.cs',
        'content/sliceforge-api/tests/TemplateApp.Api.Tests/TemplateApp.Api.Tests.csproj',
        'content/sliceforge-api/tests/TemplateApp.Api.Tests/ExampleEndpointTests.cs'
    )
    $templateEntries = @($templateArchive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($entry in $requiredTemplateEntries) {
        Assert-Condition ($templateEntries -contains $entry) "SliceForge.Templates is missing expected template content '$entry'."
    }
    Assert-Condition (@($templateArchive.Entries | Where-Object { $_.FullName.StartsWith('content/', [System.StringComparison]::OrdinalIgnoreCase) }).Count -ge $requiredTemplateEntries.Count) 'SliceForge.Templates does not contain the expected content tree.'
    Assert-Condition (-not ($templateArchive.Entries | Where-Object { $_.FullName.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase) })) 'SliceForge.Templates must not contain compiled assemblies.'
    Assert-Condition (-not ($templateArchive.Entries | Where-Object { $_.FullName.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase) })) 'SliceForge.Templates must not contain PDB files.'
    Assert-Condition (-not ($templateArchive.Entries | Where-Object { $_.FullName -match '(?i)(SliceForge\.(Core|Runtime|Validation|AspNetCore|Observability)\.dll)' })) 'SliceForge.Templates must not embed SliceForge runtime assemblies.'

    $templateManifestEntry = $templateArchive.GetEntry('content/sliceforge-api/.template.config/template.json')
    $manifestText = Read-ZipEntryText $templateManifestEntry
    $manifest = ConvertFrom-Json -InputObject $manifestText
    Assert-Condition ($manifest.shortName -eq 'sliceforge-api') 'The template shortName must be sliceforge-api.'
    Assert-Condition ($manifest.sourceName -eq 'TemplateApp') 'The template sourceName must be the neutral TemplateApp placeholder.'
    Assert-Condition ($manifest.preferNameDirectory -eq $true) 'The template must prefer a directory named after the requested application.'
    Assert-Condition (@($templateMetadata.SelectNodes("./*[local-name()='dependencies']//*[local-name()='dependency']")).Count -eq 0) 'SliceForge.Templates must not declare runtime package dependencies.'

    foreach ($entry in $templateArchive.Entries) {
        if ($entry.Length -gt 0) {
            $bytes = Read-ZipEntryBytes $entry
            $utf8Content = [System.Text.Encoding]::UTF8.GetString($bytes)
            $utf16Content = [System.Text.Encoding]::Unicode.GetString($bytes)
            foreach ($repoPath in $repoPathForms) {
                Assert-Condition ($utf8Content.IndexOf($repoPath, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "SliceForge.Templates contains a repository-local absolute path in $($entry.FullName)."
                Assert-Condition ($utf16Content.IndexOf($repoPath, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) "SliceForge.Templates contains a repository-local absolute path in $($entry.FullName)."
            }
        }
    }
}
finally {
    $templateArchive.Dispose()
}

$cliPackageId = 'SliceForge.Tool'
$cliPackagePath = Join-Path $packageRoot "$cliPackageId.$expectedVersion.nupkg"
$cliSymbolPath = Join-Path $packageRoot "$cliPackageId.$expectedVersion.snupkg"
Assert-Condition (Test-Path -LiteralPath $cliPackagePath -PathType Leaf) 'The SliceForge.Tool package is missing.'
Assert-Condition (-not (Test-Path -LiteralPath $cliSymbolPath)) 'SliceForge.Tool should package debugging payload with its tool and must not produce a separate symbol package.'

$cliArchive = [System.IO.Compression.ZipFile]::OpenRead($cliPackagePath)
try {
    $cliNuspecEntries = @($cliArchive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) })
    Assert-Condition ($cliNuspecEntries.Count -eq 1) 'SliceForge.Tool must contain exactly one nuspec.'
    [xml] $cliNuspec = Read-ZipEntryText $cliNuspecEntries[0]
    $cliMetadata = $cliNuspec.SelectSingleNode("//*[local-name()='metadata']")
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='id']").InnerText -eq $cliPackageId) 'SliceForge.Tool nuspec ID mismatch.'
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='title']").InnerText -eq 'SliceForge CLI') 'SliceForge.Cli title metadata is missing or incorrect.'
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='version']").InnerText -eq $expectedVersion) 'SliceForge.Cli version mismatch.'
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='authors']").InnerText -eq 'Clint Villanueva') 'SliceForge.Cli author metadata is missing or incorrect.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($cliMetadata.SelectSingleNode("./*[local-name()='description']").InnerText)) 'SliceForge.Cli description is missing.'
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='license']").InnerText -eq 'Apache-2.0') 'SliceForge.Cli must declare Apache-2.0 licensing.'
    Assert-Condition ($cliMetadata.SelectSingleNode("./*[local-name()='readme']").InnerText -eq 'README.md') 'SliceForge.Cli nuspec must identify README.md as its package readme.'
    Assert-Condition ($null -ne $cliArchive.GetEntry('README.md')) 'SliceForge.Cli is missing the packaged README.'
    $cliRepository = $cliMetadata.SelectSingleNode("./*[local-name()='repository']")
    Assert-Condition ($null -ne $cliRepository -and $cliRepository.GetAttribute('type') -eq 'git' -and $cliRepository.GetAttribute('url') -eq $expectedRepository) 'SliceForge.Cli repository metadata is missing or incorrect.'
    $cliProject = Join-Path $repoRoot 'tools/SliceForge.Cli/SliceForge.Cli.csproj'
    $cliProduct = & dotnet msbuild $cliProject '-getProperty:Product'
    Assert-Condition ($LASTEXITCODE -eq 0 -and $cliProduct.Trim() -eq 'SliceForge') 'SliceForge.Cli must inherit Product=SliceForge from centralized package metadata.'

    $cliPackageTypes = @($cliMetadata.SelectNodes("./*[local-name()='packageTypes']/*[local-name()='packageType']") | ForEach-Object { $_.GetAttribute('name') })
    Assert-Condition ($cliPackageTypes.Count -eq 1 -and $cliPackageTypes[0] -eq 'DotnetTool') 'SliceForge.Cli must declare PackageType=DotnetTool.'
    $cliNuspecDependencies = @($cliNuspec.SelectNodes("//*[local-name()='dependencies']//*[local-name()='dependency']"))
    Assert-Condition (-not ($cliNuspecDependencies | Where-Object { $_.GetAttribute('id').StartsWith('SliceForge.', [System.StringComparison]::Ordinal) })) 'SliceForge.Cli must not depend on SliceForge runtime packages.'

    $toolSettingsEntry = $cliArchive.GetEntry('tools/net10.0/any/DotnetToolSettings.xml')
    $toolAssemblyEntry = $cliArchive.GetEntry('tools/net10.0/any/SliceForge.Cli.dll')
    $toolSymbolsEntry = $cliArchive.GetEntry('tools/net10.0/any/SliceForge.Cli.pdb')
    $toolDependenciesEntry = $cliArchive.GetEntry('tools/net10.0/any/SliceForge.Cli.deps.json')
    Assert-Condition (($null -ne $toolSettingsEntry) -and ($null -ne $toolAssemblyEntry) -and ($null -ne $toolSymbolsEntry) -and ($null -ne $toolDependenciesEntry)) 'SliceForge.Cli is missing expected .NET tool payload files.'

    [xml] $toolSettings = Read-ZipEntryText $toolSettingsEntry
    $toolCommands = @($toolSettings.SelectNodes("//*[local-name()='Command']"))
    Assert-Condition ($toolCommands.Count -eq 1 -and $toolCommands[0].GetAttribute('Name') -eq 'sliceforge') 'SliceForge.Cli tool command must be named sliceforge.'

    $toolDependencies = ConvertFrom-Json -InputObject (Read-ZipEntryText $toolDependenciesEntry)
    $net10Target = $toolDependencies.targets.PSObject.Properties | Where-Object { $_.Name -match 'Version=v10\.0$' } | Select-Object -First 1
    Assert-Condition ($null -ne $net10Target) 'SliceForge.Cli dependency manifest must include the net10.0 target.'
    $cliTarget = $net10Target.Value.PSObject.Properties | Where-Object { $_.Name -like 'SliceForge.Cli/*' } | Select-Object -First 1
    Assert-Condition ($null -ne $cliTarget -and $cliTarget.Value.dependencies.'System.CommandLine' -eq '2.0.12') 'SliceForge.Cli must bundle the centrally approved System.CommandLine 2.0.12 dependency.'
    Assert-Condition (-not ($cliTarget.Value.dependencies.PSObject.Properties | Where-Object { $_.Name.StartsWith('SliceForge.', [System.StringComparison]::Ordinal) })) 'SliceForge.Cli runtime payload must not depend on SliceForge application packages.'
    Assert-Condition ($null -ne $cliArchive.GetEntry('tools/net10.0/any/System.CommandLine.dll')) 'SliceForge.Cli tool payload is missing System.CommandLine.dll.'
}
finally {
    $cliArchive.Dispose()
}

Write-Output 'Installing and generating the template with an isolated template hive...'
Remove-GeneratedDirectory $generatedRoot $generatedRoot $validationRoot
Remove-GeneratedDirectory $templateHive $templateHive $validationRoot
New-Item -ItemType Directory -Path $generatedRoot -Force | Out-Null
New-Item -ItemType Directory -Path $templateHive -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'Directory.Build.targets') -Destination (Join-Path $generatedRoot 'Directory.Build.targets') -Force
Invoke-DotNet @('new', 'install', "$templatePackageId@$expectedVersion", '--add-source', $packageRoot, '--debug:custom-hive', $templateHive)
Invoke-DotNet @('new', 'sliceforge-api', '-n', 'DispatchFlow', '--output', $generatedAppRoot, '--no-update-check', '--debug:custom-hive', $templateHive)

$expectedGeneratedFiles = @(
    'Directory.Build.props',
    'Directory.Packages.props',
    'DispatchFlow.slnx',
    'src/DispatchFlow.Api/DispatchFlow.Api.csproj',
    'src/DispatchFlow.Api/Program.cs',
    'src/DispatchFlow.Api/Features/Examples/GetExample/GetExampleQuery.cs',
    'src/DispatchFlow.Api/Features/Examples/GetExample/GetExampleHandler.cs',
    'src/DispatchFlow.Api/Features/Examples/GetExample/GetExampleEndpoint.cs',
    'tests/DispatchFlow.Api.Tests/DispatchFlow.Api.Tests.csproj',
    'tests/DispatchFlow.Api.Tests/ExampleEndpointTests.cs'
)
foreach ($relativePath in $expectedGeneratedFiles) {
    $fullPath = Join-Path $generatedAppRoot $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) "Generated DispatchFlow application is missing '$relativePath'."
}

$generatedTextFiles = @(Get-ChildItem -LiteralPath $generatedAppRoot -Recurse -File | Where-Object { $_.Extension -in @('.cs', '.csproj', '.props', '.slnx') })
foreach ($file in $generatedTextFiles) {
    Assert-Condition ($file.FullName -notmatch 'TemplateApp') "Template placeholder remains in generated path '$($file.FullName)'."
    $content = Get-Content -LiteralPath $file.FullName -Raw
    Assert-Condition ($content -notmatch 'TemplateApp|SliceForge\.DispatchFlow|Clint\.') "Generated file '$($file.FullName)' contains unreplaced or forbidden application identity."
    Assert-Condition ($content -notmatch '<ProjectReference[^>]*Include="[^"]*(SliceForge|C:\\dev\\SliceForge)') "Generated file '$($file.FullName)' references SliceForge source projects."
}

$generatedNamespaces = @($generatedTextFiles | Where-Object { $_.Extension -eq '.cs' } | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
Assert-Condition ($generatedNamespaces -match 'namespace DispatchFlow\.Api\.') 'Generated API namespaces do not use the DispatchFlow identity.'
Assert-Condition ($generatedNamespaces -match 'namespace DispatchFlow\.Api\.Tests') 'Generated test namespaces do not use the DispatchFlow identity.'

$generatedApiProject = Join-Path $generatedAppRoot 'src/DispatchFlow.Api/DispatchFlow.Api.csproj'
$generatedApiXml = [xml](Get-Content -LiteralPath $generatedApiProject -Raw)
$generatedApiPackageIds = @($generatedApiXml.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { $_.GetAttribute('Include') } | Sort-Object)
$expectedApiPackageIds = @('SliceForge.AspNetCore', 'SliceForge.Core', 'SliceForge.Observability', 'SliceForge.Runtime', 'SliceForge.Validation') | Sort-Object
Assert-Condition (($generatedApiPackageIds -join '|') -eq ($expectedApiPackageIds -join '|')) 'Generated API project must directly reference exactly the five SliceForge packages.'

$generatedPackageVersions = [xml](Get-Content -LiteralPath (Join-Path $generatedAppRoot 'Directory.Packages.props') -Raw)
foreach ($packageId in $expectedApiPackageIds) {
    $packageVersion = $generatedPackageVersions.SelectSingleNode("//*[local-name()='PackageVersion' and @Include='$packageId']")
    Assert-Condition ($null -ne $packageVersion -and $packageVersion.GetAttribute('Version') -eq $expectedVersion) "Generated package version for $packageId must be centrally pinned to $expectedVersion."
}

$generatedProjectFiles = @(Get-ChildItem -LiteralPath $generatedAppRoot -Recurse -Filter '*.csproj' -File)
foreach ($projectFile in $generatedProjectFiles) {
    [xml] $projectXml = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($projectReference in $projectXml.SelectNodes("//*[local-name()='ProjectReference']")) {
        $reference = $projectReference.GetAttribute('Include').Replace('\', [System.IO.Path]::DirectorySeparatorChar).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $referencePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($projectFile.DirectoryName, $reference))
        Assert-Condition ($referencePath.StartsWith($generatedAppRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) "Generated project reference escapes the generated application: $referencePath"
    }
}

Write-Output 'Generated package dependency constraints:'
foreach ($range in $packageRanges) {
    Write-Output "  $range"
}

Remove-GeneratedDirectory $consumerRoot $consumerRoot $validationRoot
try {
    Remove-GeneratedDirectory $packageCache $packageCache $validationRoot
}
catch {
    Write-Warning "The previous isolated package cache could not be cleared; using a new cache for this run. $($_.Exception.Message)"
    $packageCache = Join-Path $validationRoot "nuget-packages-$([guid]::NewGuid().ToString('N'))"
}
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

$generatedSolution = Join-Path $generatedAppRoot 'DispatchFlow.slnx'
Write-Output 'Restoring the generated application from the local SliceForge feed and nuget.org...'
Invoke-DotNet @('restore', $generatedSolution, '--configfile', $nugetConfig, '--packages', $packageCache, '--verbosity', 'minimal')
Write-Output 'Verifying generated application formatting...'
Invoke-DotNet @('format', $generatedSolution, '--verify-no-changes', '--no-restore')
Write-Output 'Building the generated DispatchFlow solution...'
Invoke-DotNet @('build', $generatedSolution, '--no-restore', '-c', 'Release', '--verbosity', 'minimal')
Write-Output 'Running generated DispatchFlow tests...'
Invoke-DotNet @('test', $generatedSolution, '--no-build', '--no-restore', '-c', 'Release', '--verbosity', 'minimal')

Write-Output 'Installing and validating the isolated SliceForge CLI tool...'
Remove-GeneratedDirectory $cliValidationRoot $cliValidationRoot $validationRoot
$cliHome = Join-Path $cliValidationRoot 'home'
$cliToolPath = Join-Path $cliValidationRoot 'tools'
$cliGeneratedRoot = Join-Path $cliValidationRoot 'projects'
$cliGeneratedAppRoot = Join-Path $cliGeneratedRoot 'CliDispatchFlow'
$cliPackageCache = Join-Path $cliValidationRoot 'nuget-packages'
New-Item -ItemType Directory -Path $cliHome -Force | Out-Null
New-Item -ItemType Directory -Path $cliToolPath -Force | Out-Null
New-Item -ItemType Directory -Path $cliGeneratedRoot -Force | Out-Null
New-Item -ItemType Directory -Path $cliPackageCache -Force | Out-Null

$previousDotnetCliHome = $env:DOTNET_CLI_HOME
$previousNugetPackages = $env:NUGET_PACKAGES
$previousSkipFirstTimeExperience = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$previousDotnetTelemetryOptOut = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$previousDotnetNoLogo = $env:DOTNET_NOLOGO
try {
    $env:DOTNET_CLI_HOME = $cliHome
    $env:NUGET_PACKAGES = $cliPackageCache
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'

    Invoke-DotNet @('new', 'install', "$templatePackageId@$expectedVersion", '--add-source', $packageRoot)
    Invoke-DotNet @('tool', 'install', $cliPackageId, '--tool-path', $cliToolPath, '--version', $expectedVersion, '--add-source', $packageRoot)

    $cliExecutableName = if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) { 'sliceforge.exe' } else { 'sliceforge' }
    $cliExecutable = Join-Path $cliToolPath $cliExecutableName
    Assert-Condition (Test-Path -LiteralPath $cliExecutable -PathType Leaf) 'The isolated SliceForge.Cli tool command was not installed.'

    $rootScreenResult = Invoke-Tool $cliExecutable @()
    Assert-Condition ($rootScreenResult.ExitCode -eq 0) 'The installed CLI root command failed.'
    Assert-Condition ($rootScreenResult.Output -match 'SliceForge' -and $rootScreenResult.Output -match 'Vertical Slice Toolkit for \.NET 10' -and $rootScreenResult.Output -match 'Author: Clint Villanueva') 'The installed CLI root screen is missing expected branding.'
    Assert-Condition ($rootScreenResult.Output -match 'new\s+Create a SliceForge application' -and $rootScreenResult.Output -match 'doctor\s+Check the environment' -and $rootScreenResult.Output -match 'version\s+Show version') 'The installed CLI root screen is missing expected commands.'
    Assert-Condition ($rootScreenResult.Output -notmatch '\x1b') 'The installed CLI root screen must not emit ANSI escape sequences.'

    $helpResult = Invoke-Tool $cliExecutable @('--help')
    Assert-Condition ($helpResult.ExitCode -eq 0 -and $helpResult.Output -match 'Usage:') 'The installed CLI must provide standard command-line help.'
    Assert-Condition ($helpResult.Output -notmatch '╭|\x1b') 'CLI help must not use the decorative root screen or ANSI sequences.'

    foreach ($versionArguments in @(@('--version'), @('version'))) {
        $versionResult = Invoke-Tool $cliExecutable $versionArguments
        Assert-Condition ($versionResult.ExitCode -eq 0 -and $versionResult.Output.Trim() -eq $expectedVersion) "The installed CLI version command output was not exactly $expectedVersion."
    }

    $doctorResult = Invoke-Tool $cliExecutable @('doctor')
    Assert-Condition ($doctorResult.ExitCode -eq 0 -and $doctorResult.Output -match 'Environment ready\.') 'The installed CLI doctor command did not recognize the isolated SDK and template installation.'
    Assert-Condition ($doctorResult.Output -notmatch '\x1b') 'The installed CLI doctor command must not emit ANSI escape sequences.'

    Push-Location $cliGeneratedRoot
    try {
        $newResult = Invoke-Tool $cliExecutable @('new', '-n', 'CliDispatchFlow')
    }
    finally {
        Pop-Location
    }
    Assert-Condition ($newResult.ExitCode -eq 0) "The installed CLI failed to generate CliDispatchFlow: $($newResult.Output)"

    $cliExpectedFiles = @(
        'Directory.Build.props',
        'Directory.Packages.props',
        'CliDispatchFlow.slnx',
        'src/CliDispatchFlow.Api/CliDispatchFlow.Api.csproj',
        'src/CliDispatchFlow.Api/Program.cs',
        'tests/CliDispatchFlow.Api.Tests/CliDispatchFlow.Api.Tests.csproj',
        'tests/CliDispatchFlow.Api.Tests/ExampleEndpointTests.cs'
    )
    foreach ($relativePath in $cliExpectedFiles) {
        $fullPath = Join-Path $cliGeneratedAppRoot $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) "CLI-generated application is missing '$relativePath'."
    }

    $cliGeneratedFiles = @(Get-ChildItem -LiteralPath $cliGeneratedAppRoot -Recurse -File | Where-Object { $_.Extension -in @('.cs', '.csproj', '.props', '.slnx') })
    foreach ($file in $cliGeneratedFiles) {
        Assert-Condition ($file.FullName -notmatch 'TemplateApp') "Template placeholder remains in CLI-generated path '$($file.FullName)'."
        $content = Get-Content -LiteralPath $file.FullName -Raw
        Assert-Condition ($content -notmatch 'TemplateApp|SliceForge\.Cli|SliceForge\.CliDispatchFlow|Clint\.') "CLI-generated file '$($file.FullName)' contains an unreplaced or forbidden identity."
    }
    $cliGeneratedSource = @($cliGeneratedFiles | Where-Object { $_.Extension -eq '.cs' } | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    Assert-Condition ($cliGeneratedSource -match 'namespace CliDispatchFlow\.Api\.') 'CLI-generated API namespaces do not use the requested application identity.'
    Assert-Condition ($cliGeneratedSource -match 'namespace CliDispatchFlow\.Api\.Tests') 'CLI-generated test namespaces do not use the requested application identity.'

    $cliApiProject = Join-Path $cliGeneratedAppRoot 'src/CliDispatchFlow.Api/CliDispatchFlow.Api.csproj'
    [xml] $cliApiProjectXml = Get-Content -LiteralPath $cliApiProject -Raw
    $cliApiPackageIds = @($cliApiProjectXml.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { $_.GetAttribute('Include') } | Sort-Object)
    $expectedApiPackageIds = @('SliceForge.AspNetCore', 'SliceForge.Core', 'SliceForge.Observability', 'SliceForge.Runtime', 'SliceForge.Validation') | Sort-Object
    Assert-Condition (($cliApiPackageIds -join '|') -eq ($expectedApiPackageIds -join '|')) 'CLI-generated API must use exactly the five package-only SliceForge references.'
    $cliGeneratedVersions = [xml](Get-Content -LiteralPath (Join-Path $cliGeneratedAppRoot 'Directory.Packages.props') -Raw)
    foreach ($packageId in $expectedApiPackageIds) {
        $packageVersion = $cliGeneratedVersions.SelectSingleNode("//*[local-name()='PackageVersion' and @Include='$packageId']")
        Assert-Condition ($null -ne $packageVersion -and $packageVersion.GetAttribute('Version') -eq $expectedVersion) "CLI-generated package version for $packageId is not $expectedVersion."
    }
    foreach ($projectFile in @(Get-ChildItem -LiteralPath $cliGeneratedAppRoot -Recurse -Filter '*.csproj' -File)) {
        [xml] $projectXml = Get-Content -LiteralPath $projectFile.FullName -Raw
        foreach ($projectReference in $projectXml.SelectNodes("//*[local-name()='ProjectReference']")) {
            $reference = $projectReference.GetAttribute('Include').Replace('\', [System.IO.Path]::DirectorySeparatorChar).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $referencePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($projectFile.DirectoryName, $reference))
            Assert-Condition ($referencePath.StartsWith($cliGeneratedAppRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) "CLI-generated project reference escapes the generated application: $referencePath"
        }
    }

    $cliGeneratedSolution = Join-Path $cliGeneratedAppRoot 'CliDispatchFlow.slnx'
    Write-Output 'Restoring the CLI-generated application from the local SliceForge feed and nuget.org...'
    Invoke-DotNet @('restore', $cliGeneratedSolution, '--configfile', $nugetConfig, '--packages', $packageCache, '--verbosity', 'minimal')
    Write-Output 'Building the CLI-generated application...'
    Invoke-DotNet @('build', $cliGeneratedSolution, '--no-restore', '-c', 'Release', '--verbosity', 'minimal')
    Write-Output 'Running CLI-generated application tests...'
    Invoke-DotNet @('test', $cliGeneratedSolution, '--no-build', '--no-restore', '-c', 'Release', '--verbosity', 'minimal')
}
finally {
    if ($null -eq $previousDotnetCliHome) {
        Remove-Item Env:DOTNET_CLI_HOME -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_CLI_HOME = $previousDotnetCliHome
    }

    if ($null -eq $previousNugetPackages) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    }
    else {
        $env:NUGET_PACKAGES = $previousNugetPackages
    }

    if ($null -eq $previousSkipFirstTimeExperience) {
        Remove-Item Env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousSkipFirstTimeExperience
    }

    if ($null -eq $previousDotnetTelemetryOptOut) {
        Remove-Item Env:DOTNET_CLI_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousDotnetTelemetryOptOut
    }

    if ($null -eq $previousDotnetNoLogo) {
        Remove-Item Env:DOTNET_NOLOGO -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_NOLOGO = $previousDotnetNoLogo
    }
}

Write-Output 'All package contents, isolated package consumers, template generation, and installed CLI validation passed.'
