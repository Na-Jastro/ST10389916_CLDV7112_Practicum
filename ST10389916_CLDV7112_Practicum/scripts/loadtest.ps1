param(
	[string]$BaseUrl = "https://app-practicum-st10389916.azurewebsites.net",
	[int]$Requests = 2000,
	[int]$Parallelism = 40
)

$endpoint = "$BaseUrl/events"
$errors = [System.Collections.Concurrent.ConcurrentBag[string]]::new()

$requestScript = {
	param($endpoint)
	$id = [guid]::NewGuid()
	$body = @{
		id = $id
		eventType = "OrderPlaced"
		amount = [math]::Round((Get-Random -Minimum 10 -Maximum 1000) + (Get-Random), 2)
		occurredAt = (Get-Date).ToUniversalTime().ToString("o")
		data = @{ source = "loadtest"; customerId = (Get-Random -Minimum 1 -Maximum 100000) }
	} | ConvertTo-Json -Depth 5

	try {
		Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json" | Out-Null
		return @{ Success = $true; Error = $null }
	}
	catch {
		return @{ Success = $false; Error = $_.Exception.Message }
	}
}

if ($PSVersionTable.PSVersion.Major -ge 7) {
	1..$Requests | ForEach-Object -Parallel {
		param($endpoint)
		$id = [guid]::NewGuid()
		$body = @{
			id = $id
			eventType = "OrderPlaced"
			amount = [math]::Round((Get-Random -Minimum 10 -Maximum 1000) + (Get-Random), 2)
			occurredAt = (Get-Date).ToUniversalTime().ToString("o")
			data = @{ source = "loadtest"; customerId = (Get-Random -Minimum 1 -Maximum 100000) }
		} | ConvertTo-Json -Depth 5

		try {
			Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType "application/json" | Out-Null
		}
		catch {
			$_.Exception.Message
		}
	} -ThrottleLimit $Parallelism -ArgumentList $endpoint | ForEach-Object {
		if ($_ -is [string] -and $_) { $errors.Add($_) }
	}
}
else {
	$jobs = @()
	for ($i = 1; $i -le $Requests; $i++) {
		while (($jobs | Where-Object { $_.State -eq 'Running' }).Count -ge $Parallelism) {
			Start-Sleep -Milliseconds 100
		}

		$jobs += Start-Job -ScriptBlock $requestScript -ArgumentList $endpoint
	}

	$results = $jobs | Wait-Job | Receive-Job
	$jobs | Remove-Job -Force | Out-Null

	foreach ($result in $results) {
		if (-not $result.Success -and $result.Error) {
			$errors.Add($result.Error)
		}
	}
}

"Total Requests: $Requests"
"Errors: $($errors.Count)"
if ($errors.Count -gt 0) {
	"Sample Error: $($errors | Select-Object -First 1)"
}