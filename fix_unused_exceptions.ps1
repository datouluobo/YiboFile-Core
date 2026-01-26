# PowerShell Script: Fix Unused Exception Variables
# This script intelligently detects and fixes all unused catch (Exception ex) blocks

param(
    [string]$ProjectPath = "f:\Download\GitHub\YiboFile"
)

Write-Host "Starting scan for unused exception variables..." -ForegroundColor Cyan

# Statistics
$totalFiles = 0
$modifiedFiles = 0
$totalReplacements = 0

# Get all C# files
$csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\'
}

$totalFiles = $csFiles.Count
Write-Host "Found $totalFiles C# files" -ForegroundColor Yellow

foreach ($file in $csFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $originalContent = $content
        $modified = $false
        
        # Simple pattern: catch (Exception ex) followed by opening brace
        # We'll do line-by-line processing for better control
        $lines = $content -split "`r?`n"
        $newLines = @()
        $i = 0
        
        while ($i -lt $lines.Length) {
            $line = $lines[$i]
            
            # Check if this line contains "catch (Exception ex)"
            if ($line -match 'catch\s*\(\s*Exception\s+ex\s*\)') {
                # Get the catch block content (simplified: next few lines until we see another catch/finally/method)
                $blockContent = ""
                $j = $i + 1
                $braceCount = 0
                $startFound = $false
                
                # Find opening brace and collect block
                while ($j -lt $lines.Length -and $j -lt $i + 50) {
                    $blockLine = $lines[$j]
                    
                    if ($blockLine -match '\{') {
                        $startFound = $true
                        $braceCount += ($blockLine.ToCharArray() | Where-Object { $_ -eq '{' }).Count
                    }
                    if ($blockLine -match '\}') {
                        $braceCount -= ($blockLine.ToCharArray() | Where-Object { $_ -eq '}' }).Count
                    }
                    
                    $blockContent += $blockLine + "`n"
                    
                    if ($startFound -and $braceCount -le 0) {
                        break
                    }
                    $j++
                }
                
                # Check if 'ex' is used in the block
                $usesEx = $blockContent -match '\bex\.' -or
                $blockContent -match '\bex\)' -or
                $blockContent -match '\bex\s*;' -or
                $blockContent -match '\bex\s*,'
                
                if (-not $usesEx) {
                    # Replace the line
                    $newLine = $line -replace 'catch\s*\(\s*Exception\s+ex\s*\)', 'catch (Exception)'
                    $newLines += $newLine
                    $modified = $true
                    $totalReplacements++
                }
                else {
                    $newLines += $line
                }
            }
            else {
                $newLines += $line
            }
            
            $i++
        }
        
        if ($modified) {
            $newContent = $newLines -join "`r`n"
            Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8 -NoNewline
            $modifiedFiles++
            $replacementsInFile = ($originalContent -split "`r?`n" | Where-Object { $_ -match 'catch\s*\(\s*Exception\s+ex\s*\)' }).Count - ($newContent -split "`r?`n" | Where-Object { $_ -match 'catch\s*\(\s*Exception\s+ex\s*\)' }).Count
            Write-Host "  Fixed $($file.Name): $replacementsInFile replacements" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  Error processing $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host "`nFix completed!" -ForegroundColor Cyan
Write-Host "Total files: $totalFiles" -ForegroundColor Yellow
Write-Host "Modified files: $modifiedFiles" -ForegroundColor Green
Write-Host "Total replacements: $totalReplacements" -ForegroundColor Green

