# Function to combine .adm files from timestamp folders within the past 24 hours
function Join-AdmFilesFromTimestampFolders {
    param (
        [Parameter(Mandatory = $true)]
        [string]$RootFolder,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputFile,
        
        [Parameter(Mandatory = $false)]
        [int]$HoursBack = 24
    )
    
    # Calculate the cutoff timestamp
    $cutoffDate = [DateTimeOffset]::Now.AddHours(-$HoursBack)
    $cutoffUnixTimestamp = [int]$cutoffDate.ToUnixTimeSeconds()
    
    Write-Host "  Finding .adm files in timestamp folders newer than $cutoffDate..."
    
    $admFiles = @()
    
    if (Test-Path -Path $RootFolder) {
        $folders = Get-ChildItem -Path $RootFolder -Directory
        
        foreach ($folder in $folders) {
            # Check if the folder name is a valid Unix timestamp (all digits)
            if ($folder.Name -match '^\d+$') {
                $folderTimestamp = [int]$folder.Name
                
                # Check if the folder timestamp is within the cutoff period
                if ($folderTimestamp -ge $cutoffUnixTimestamp) {
                    Write-Host "    Processing folder: $($folder.FullName)"
                    # Get the .adm files from this folder
                    $filesInFolder = Get-ChildItem -Path $folder.FullName -Filter "*.adm" -Recurse
                    if ($filesInFolder) {
                        $admFiles += $filesInFolder
                    }
                }
            }
        }
    } else {
        Write-Warning "    Root folder does not exist: $RootFolder"
        return $false
    }

    # Check if any files were found
    if ($admFiles.Count -eq 0) {
        Write-Host "    No .adm files found in relevant timestamp subfolders from the last $HoursBack hours."
        return $false
    }

    Write-Host "    Found $($admFiles.Count) .adm files to combine."

    # Create or clear the output file
    if (Test-Path $OutputFile) {
        Clear-Content -Path $OutputFile
    } else {
        $null = New-Item -Path $OutputFile -ItemType File -Force
    }

    # Combine the content of all found .adm files
    foreach ($file in $admFiles) {
        Get-Content -Path $file.FullName | Add-Content -Path $OutputFile
    }

    Write-Host "    Successfully combined $($admFiles.Count) .adm files into $OutputFile"
    return $true
}

# Function to generate map using the API (handles both Death Map and Player Trace Map)
function Invoke-MapGeneration {
    param (
        [Parameter(Mandatory = $true)]
        [string]$AdminLogFile,
        
        [Parameter(Mandatory = $true)]
        [string]$MapName,
        
        [Parameter(Mandatory = $true)]
        [ValidateSet("DeathMap", "PlayerTrace")]
        [string]$MapType,
        
        [Parameter(Mandatory = $false)]
        [int]$OutputSize = 1024,
        
        [Parameter(Mandatory = $false)]
        [string]$OutputType = "",
        
        [Parameter(Mandatory = $false)]
        [string]$BackgroundImage = "",
        
        [Parameter(Mandatory = $false)]
        [string]$OutputImagePath = ""
    )
    
    # If output path not specified, create one based on the input file
    if ([string]::IsNullOrEmpty($OutputImagePath)) {
        $suffix = if ($MapType -eq "PlayerTrace") { "-trace" } else { "" }
        $OutputImagePath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($AdminLogFile), "$MapName$suffix.png")
    }
    
    # Create a temporary file for the JSON response
    $jsonResponseFile = [System.IO.Path]::ChangeExtension($OutputImagePath, "json")
    
    Write-Host "  Generating $MapType for $MapName..."
    Write-Host "  Input: $AdminLogFile"
    Write-Host "  Output: $OutputImagePath"
    
    try {
        # Create curl command
        $curlCommand = "curl.exe"
        $curlArgs = @(
            "--insecure", # Skip certificate validation (only for development)
            "-X", "POST",
            "-F", "AdminLogFiles=@`"$AdminLogFile`"",
            "-F", "Configuration.MapName=$MapName",
            "-F", "Configuration.OutputSize=$OutputSize",
            "-o", "`"$jsonResponseFile`"" # Save JSON to file instead of direct image output
        )
        
        # Add OutputType parameter only for DeathMap
        if ($MapType -eq "DeathMap" -and -not [string]::IsNullOrEmpty($OutputType)) {
            $curlArgs += "-F"
            $curlArgs += "Configuration.OutputType=$OutputType"
        }
        
        # Add background image if specified
        if (-not [string]::IsNullOrEmpty($BackgroundImage)) {
            $curlArgs += "-F"
            $curlArgs += "Configuration.BackgroundImage=$BackgroundImage"
        }
        
        # Add the appropriate URL based on map type
        $baseUrl = ""
        $endpoint = "$baseUrl/$MapType/generateWithMetadata"
        $curlArgs += $endpoint
        
        # Execute curl command
        Write-Host "  Executing curl command..."
        $process = Start-Process -FilePath $curlCommand -ArgumentList $curlArgs -NoNewWindow -Wait -PassThru
        
        if ($process.ExitCode -eq 0) {
            # Check if JSON file exists and process it
            if (Test-Path $jsonResponseFile) {
                # Read and parse the JSON response
                $jsonResponse = Get-Content -Path $jsonResponseFile -Raw | ConvertFrom-Json
                
                # Extract data based on map type
                $deathCount = $jsonResponse.totalDeaths
                $positionCount = if ($MapType -eq "PlayerTrace") { $jsonResponse.totalPositions } else { 0 }
                $imageData = $jsonResponse.imageData
                
                Write-Host "  Deaths detected: $deathCount"
                if ($MapType -eq "PlayerTrace") {
                    Write-Host "  Positions detected: $positionCount"
                }
                
                if ($imageData) {
                    # Convert base64 string to bytes and save as image
                    $imageBytes = [Convert]::FromBase64String($imageData)
                    [System.IO.File]::WriteAllBytes($OutputImagePath, $imageBytes)
                    
                    Write-Host "  Successfully generated $MapType image: $OutputImagePath"
                    
                    # Return results based on map type
                    if ($MapType -eq "PlayerTrace") {
                        return @{
                            Success = $true
                            DeathCount = $deathCount
                            PositionCount = $positionCount
                        }
                    } else {
                        return @{
                            Success = $true
                            DeathCount = $deathCount
                        }
                    }
                } else {
                    Write-Error "No image data found in the API response"
                }
            } else {
                Write-Error "Response file not found: $jsonResponseFile"
            }
        } else {
            Write-Error "Error calling the $MapType API: curl exited with code $($process.ExitCode)"
        }
    }
    catch {
        Write-Error "Error executing curl command: $_"
    }
    finally {
        # Clean up the temporary JSON file
        if (Test-Path $jsonResponseFile) {
            Remove-Item -Path $jsonResponseFile -Force
        }
    }
    
    # Return failure result with appropriate structure
    if ($MapType -eq "PlayerTrace") {
        return @{
            Success = $false
            DeathCount = 0
            PositionCount = 0
        }
    } else {
        return @{
            Success = $false
            DeathCount = 0
        }
    }
}

# Function to send map image to Discord webhook (handles both Death Map and Player Trace Map)
function Send-MapToDiscordWebhook {
    param (
        [Parameter(Mandatory = $true)]
        [string]$WebhookUrl,
        
        [Parameter(Mandatory = $true)]
        [string]$ImagePath,
        
        [Parameter(Mandatory = $true)]
        [string]$MapName,
        
        [Parameter(Mandatory = $true)]
        [ValidateSet("DeathMap", "PlayerTrace")]
        [string]$MapType,
        
        [Parameter(Mandatory = $false)]
        [int]$DeathCount = 0,
        
        [Parameter(Mandatory = $false)]
        [int]$PositionCount = 0
    )
    
    # Check if the file exists and is at least 10KB
    if (-not (Test-Path -Path $ImagePath)) {
        Write-Warning "  Image file does not exist: $ImagePath"
        return $false
    }
    
    $fileInfo = Get-Item -Path $ImagePath
    if ($fileInfo.Length -lt 10KB) {
        Write-Warning "  Image file is smaller than 10KB (possibly empty or error): $ImagePath"
        return $false
    }
    
    # Prepare the message based on map type
    $currentDate = Get-Date -Format "yyyy-MM-dd HH:mm"
    if ($MapType -eq "DeathMap") {
        $message = "Heatmap for $MapName - Generated on $currentDate - Total Deaths: $DeathCount"
    } else {
        $message = "Player Trace Map for $MapName - Generated on $currentDate - Deaths: $DeathCount - Positions: $PositionCount"
    }
    
    try {
        Write-Host "  Sending $MapType to Discord webhook..."
        
        # Create curl command
        $curlCommand = "curl.exe"
        $curlArgs = @(
            "-X", "POST",
            "-H", "Content-Type: multipart/form-data",
            "-F", "content=`"$message`"",
            "-F", "file=@`"$ImagePath`"",
            "-v", # Verbose output
            $WebhookUrl
        )
        
        # Execute curl command and capture the output
        Write-Host "  Executing Discord webhook command..."
        $response = & $curlCommand $curlArgs
        
        # Display the response
        Write-Host "  Discord API Response:"
        $response | ForEach-Object { Write-Host "    $_" }
        
        # If we got this far, consider it a success
        Write-Host "  Successfully sent $MapType to Discord webhook"
        return $true
    }
    catch {
        Write-Error "Error sending to Discord webhook: $_"
        return $false
    }
}

# JSON configuration string (you can also load this from a file)
$jsonConfig = @"
[
    {
        "mapName": "Namalsk",
        "rootFolder": "C:\\Program Files (x86)\\CFTools Software GmbH\\Architect\\Agent\\deployments\\Namalsk\\log_storage",
        "outputFile": "C:\\ps\\discordheatmap\\Namalsk.adm",
        "outputImage": "C:\\ps\\discordheatmap\\Namalsk.png",
        "traceImage": "C:\\ps\\discordheatmap\\Namalsk-trace.png",
        "outputSize": 1024,
        "heatMapWebhook": "",
        "traceMapWebhook": ""
    }
]
"@

# Discord webhook URL
$discordWebhookUrl = ''

# Parse the JSON configuration
try {
    $config = $jsonConfig | ConvertFrom-Json
}
catch {
    Write-Error "Failed to parse JSON configuration"
    exit 1 <#Do this if a terminating exception happens#>
}

# Process each mapping from the configuration
foreach ($mapping in $config) {
    Write-Host "Processing mapping: $($mapping.mapName)"
    Write-Host "  Root folder: $($mapping.rootFolder)"
    Write-Host "  Output file: $($mapping.outputFile)"
    
    $result = Join-AdmFilesFromTimestampFolders -RootFolder $mapping.rootFolder -OutputFile $mapping.outputFile
    
    if ($result) {
        Write-Host "  Successfully processed $($mapping.mapName)"
        
        # Get common parameters
        $outputSize = if ($mapping.PSObject.Properties.Name -contains "outputSize") { $mapping.outputSize } else { 1024 }
        $outputImage = if ($mapping.PSObject.Properties.Name -contains "outputImage") { $mapping.outputImage } else { "" }
        $traceImage = if ($mapping.PSObject.Properties.Name -contains "traceImage") { $mapping.traceImage } else { "" }
        
        # Generate death heatmap
        $apiResult = Invoke-MapGeneration -AdminLogFile $mapping.outputFile `
                                        -MapName $mapping.mapName `
                                        -MapType "DeathMap" `
                                        -OutputSize $outputSize `
                                        -OutputType "heat" `
                                        -OutputImagePath $outputImage
        
        if ($apiResult.Success) {
            Write-Host "  Successfully generated death heatmap for $($mapping.mapName) with $($apiResult.DeathCount) deaths"
            
            # Send the death heatmap to Discord
            $webhookResult = Send-MapToDiscordWebhook -WebhookUrl $discordWebhookUrl `
                                                     -ImagePath $outputImage `
                                                     -MapName $mapping.mapName `
                                                     -MapType "DeathMap" `
                                                     -DeathCount $apiResult.DeathCount
            
            if ($webhookResult) {
                Write-Host "  Successfully sent death heatmap to Discord for $($mapping.mapName)"
            } else {
                Write-Host "  Failed to send death heatmap to Discord for $($mapping.mapName)"
            }
        } else {
            Write-Host "  Failed to generate death heatmap for $($mapping.mapName)"
        }
        
        # Generate player trace map
        $traceApiResult = Invoke-MapGeneration -AdminLogFile $mapping.outputFile `
                                             -MapName $mapping.mapName `
                                             -MapType "PlayerTrace" `
                                             -OutputSize $outputSize `
                                             -OutputImagePath $traceImage
        
        if ($traceApiResult.Success) {
            Write-Host "  Successfully generated player trace map for $($mapping.mapName) with $($traceApiResult.DeathCount) deaths and $($traceApiResult.PositionCount) positions"
            
            # Send the player trace map to Discord
            $traceWebhookResult = Send-MapToDiscordWebhook -WebhookUrl $discordWebhookUrl `
                                                          -ImagePath $traceImage `
                                                          -MapName $mapping.mapName `
                                                          -MapType "PlayerTrace" `
                                                          -DeathCount $traceApiResult.DeathCount `
                                                          -PositionCount $traceApiResult.PositionCount
            
            if ($traceWebhookResult) {
                Write-Host "  Successfully sent player trace map to Discord for $($mapping.mapName)"
            } else {
                Write-Host "  Failed to send player trace map to Discord for $($mapping.mapName)"
            }
        } else {
            Write-Host "  Failed to generate player trace map for $($mapping.mapName)"
        }
    } else {
        Write-Host "  No files were combined for $($mapping.mapName)"
    }
    
    Write-Host ""
}

Write-Host "Processing complete."