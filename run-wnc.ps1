$ErrorActionPreference = 'Stop'

Write-Host '[WNC] Stopping stale processes...' -ForegroundColor Cyan

function Get-WncProcesses {
    Get-CimInstance Win32_Process | Where-Object {
        $_.Name -ieq 'WNC.Airline.exe' -or (
            $_.Name -ieq 'dotnet.exe' -and (
                $_.CommandLine -like '*D:\WNC*' -or
                $_.CommandLine -like '*WNC.Airline.dll*'
            )
        )
    }
}

Get-WncProcesses |
    ForEach-Object {
        try {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
            Write-Host ("  stopped PID {0}" -f $_.ProcessId) -ForegroundColor DarkGray
        }
        catch {
            Write-Host ("  skip PID {0}" -f $_.ProcessId) -ForegroundColor DarkGray
        }
    }

for ($i = 0; $i -lt 20; $i++) {
    if (-not (Get-WncProcesses)) {
        break
    }

    Start-Sleep -Milliseconds 250
}

if (Get-WncProcesses) {
    Write-Host '[WNC] Warning: some stale processes may still be running.' -ForegroundColor Yellow
}

Write-Host '[WNC] Running app...' -ForegroundColor Cyan

# Always run the intended project explicitly.
$env:WNC_OPEN_BROWSER = '1'
dotnet run --project .\WNC.Airline.csproj
