if ($host.Name -ne "Windows PowerShell ISE Host")
{
    Write-Warning "This module does only run inside PowerShell ISE"
    return
}

Add-Type -Path $PSScriptRoot\VariableExplorer.dll -PassThru
$typeVariableExplorer = [IseAddons.VariableExplorer]
$psISE.CurrentPowerShellTab.VerticalAddOnTools.Add("VariableExplorer", $typeVariableExplorer, $true)