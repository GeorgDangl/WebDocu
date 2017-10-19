$environmentName = "Production"

# Applying web.config transformations
$webConfigTransformatorPackages = Join-Path -Path $env:USERPROFILE -ChildPath "\.nuget\packages\WebConfigTransformRunner"
$latestWebConfigTranformator = Join-Path -Path ((Get-ChildItem -Path $webConfigTransformatorPackages | Sort-Object Fullname -Descending)[0].FullName) -ChildPath "Tools\WebConfigTransformRunner.exe"
$webConfigDirs = Get-ChildItem -Path "$PSScriptRoot\publish" -Recurse -Filter "web*.config" | Select -Property Directory -Unique
ForEach ($directory in $webConfigDirs.Directory){
    $transformationSource = (Get-ChildItem -Path $directory -Filter ("web." + $environmentName + ".config"))
    if ($transformationSource) {
        $guid = [Guid]::NewGuid().ToString()
        $transformArguments = @("""" + (Join-Path -Path $directory -ChildPath "web.config") + """",`
                                """" + $transformationSource[0].FullName + """",`
                                """" + (Join-Path -Path $directory -ChildPath $guid) + """")
        $transformationProcess = Start-Process -FilePath $latestWebConfigTranformator -ArgumentList $transformArguments -Wait -PassThru -NoNewWindow
        # Delete original web.config and rename the created one
        Remove-Item -Path (Join-Path -Path $directory -ChildPath "web.config")
        Rename-Item -Path (Join-Path -Path $directory -ChildPath $guid) -NewName (Join-Path -Path $directory -ChildPath "web.config") 
    }
    # Delete all web.*.config files
    ForEach ($file in (Get-ChildItem -Path $directory -Filter "web.*.config")){
        Write-Host "Removing $file"
        Remove-Item -Path $file.FullName
    }
}