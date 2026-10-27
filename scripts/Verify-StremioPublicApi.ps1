$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-JsonResponse([string] $Url) {
    Write-Host "GET $Url"
    # Follow the 307 redirects currently returned by Cinemeta catalog endpoints.
    # curl is reliable in both Windows PowerShell and GitHub's Windows runner.
    $raw = & curl.exe --fail --silent --show-error --location --max-time 25 --user-agent 'WatchStream-ContractSmokeTest/1.0' --header 'Accept: application/json' $Url
    $curlExitCode = $LASTEXITCODE
    if ($curlExitCode -ne 0) {
        throw "HTTP request failed for $Url (curl exit code $curlExitCode)."
    }

    $jsonText = $raw -join "`n"
    try {
        return ($jsonText | ConvertFrom-Json)
    }
    catch {
        throw "Response from $Url was not valid JSON."
    }
}

$manifestUrl = 'https://v3-cinemeta.strem.io/manifest.json'
$manifest = Get-JsonResponse $manifestUrl

if ([string]::IsNullOrWhiteSpace([string]$manifest.id)) { throw 'Manifest has no id.' }
if ([string]::IsNullOrWhiteSpace([string]$manifest.version)) { throw 'Manifest has no version.' }
if (-not ($manifest.types -contains 'movie')) { throw 'Manifest does not advertise movie content.' }
if (-not ($manifest.types -contains 'series')) { throw 'Manifest does not advertise series content.' }
if (-not ($manifest.resources -contains 'catalog')) { throw 'Manifest does not advertise catalog resources.' }

foreach ($type in @('movie', 'series')) {
    $catalog = @($manifest.catalogs | Where-Object { $_.type -eq $type -and $_.id -eq 'top' } | Select-Object -First 1)
    if ($catalog.Count -eq 0) { throw "Manifest does not advertise $type/top catalog." }

    $catalogUrl = "https://v3-cinemeta.strem.io/catalog/$type/top.json"
    $result = Get-JsonResponse $catalogUrl

    if ($null -eq $result.metas -or $result.metas -isnot [System.Array]) {
        throw "$type catalog response does not contain a metas array."
    }

    $invalid = @($result.metas | Where-Object {
        [string]::IsNullOrWhiteSpace([string]$_.id) -or
        [string]::IsNullOrWhiteSpace([string]$_.name) -or
        $_.type -ne $type
    })
    if ($invalid.Count -gt 0) {
        throw "$type catalog contains $($invalid.Count) item(s) missing id/name or with an unexpected type."
    }

    Write-Host "PASS: $type/top returned $($result.metas.Count) valid metadata item(s)."
}

Write-Host 'PASS: public Cinemeta manifest and movie/series catalog contracts look structurally valid.'
Write-Host 'NOTE: this does not test authenticated account APIs, add-on stream availability, or Xbox playback.'
