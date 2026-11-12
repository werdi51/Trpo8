# Проверка API без Swagger: логин -> токен -> защищённые запросы.
# Запуск из папки проекта:
#   powershell -ExecutionPolicy Bypass -File .\test-api.ps1
#
# Приложение должно быть уже запущено на https://localhost:7171

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$base = 'https://localhost:7171'
$userLogin = 'admin123'
$userPassword = 'P@s$w0rd'

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [string]$Body
    )

    $curlArgs = @('-sS', '-k', '-X', $Method, ($base + $Path), '-w', 'HTTP_STATUS:%{http_code}')
    if ($Token) { $curlArgs += @('-H', "Authorization: Bearer $Token") }

    $tmp = $null
    if ($Body) {
        $tmp = Join-Path $env:TEMP ('trpo8_' + [guid]::NewGuid().ToString('N') + '.json')
        [IO.File]::WriteAllText($tmp, $Body, (New-Object Text.UTF8Encoding -ArgumentList $false))
        $curlArgs += @('-H', 'Content-Type: application/json', '--data-binary', "@$tmp")
    }

    $raw = & curl.exe @curlArgs
    if ($tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }

    $status = 0
    $content = [string]$raw
    if ($content -match 'HTTP_STATUS:(\d{3})\s*$') {
        $status = [int]$Matches[1]
        $content = $content.Substring(0, $content.Length - $Matches[0].Length).Trim()
    }

    [pscustomobject]@{ Status = $status; Content = $content }
}

function Show-Result {
    param([string]$Title, $Result)

    Write-Host ''
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    Write-Host "HTTP $($Result.Status)"
    if ($Result.Content) { Write-Host $Result.Content }
}

# 0. Проверка, что приложение запущено
try {
    & curl.exe -sS -k -o NUL "$base/swagger/index.html" | Out-Null
}
catch {
    throw "Приложение не отвечает на $base. Сначала запусти его (профиль https)."
}

# 1. Логин
$loginBody = @{ login = $userLogin; password = $userPassword } | ConvertTo-Json -Compress
$loginResult = Invoke-Api -Method POST -Path '/api/v1/auth/login' -Body $loginBody
Show-Result "1. Логин $userLogin" $loginResult

if ($loginResult.Status -ne 200) { throw "Логин не удался, статус $($loginResult.Status)" }

$token = ($loginResult.Content | ConvertFrom-Json).access_token
if (-not $token) { throw 'В ответе нет access_token' }
Write-Host "Токен получен: $($token.Substring(0, 20))..." -ForegroundColor Green

# 2. Создание задачи
$taskBody = '{"title":"работа","description":"тяжелая"}'
$created = Invoke-Api -Method POST -Path '/api/v1/tasks' -Token $token -Body $taskBody
Show-Result '2. Создание задачи' $created

if ($created.Status -ne 201) { throw "Задача не создана, статус $($created.Status)" }
$taskId = ($created.Content | ConvertFrom-Json).id

# 3. Список своих задач
Show-Result '3. GET /api/v1/tasks' (Invoke-Api -Method GET -Path '/api/v1/tasks' -Token $token)

# 4. Все задачи (Admin)
Show-Result '4. GET /api/v1/tasks/all' (Invoke-Api -Method GET -Path '/api/v1/tasks/all' -Token $token)

# 5. Задача по id
Show-Result "5. GET /api/v1/tasks/$taskId" (Invoke-Api -Method GET -Path "/api/v1/tasks/$taskId" -Token $token)

# 6. Изменение с isCompleted
$updateBody = '{"title":"работа","description":"тяжелая","isCompleted":true}'
Show-Result "6. PUT /api/v1/tasks/$taskId (isCompleted: true)" `
    (Invoke-Api -Method PUT -Path "/api/v1/tasks/$taskId" -Token $token -Body $updateBody)

# 7. Изменение без isCompleted — статус должен остаться true
$renameBody = '{"title":"работа (переименована)","description":"тяжелая"}'
Show-Result "7. PUT /api/v1/tasks/$taskId (без isCompleted)" `
    (Invoke-Api -Method PUT -Path "/api/v1/tasks/$taskId" -Token $token -Body $renameBody)

# 8. Удаление
Show-Result "8. DELETE /api/v1/tasks/$taskId" (Invoke-Api -Method DELETE -Path "/api/v1/tasks/$taskId" -Token $token)

# 9. Запрос без токена — 401
Show-Result '9. GET /api/v1/tasks без токена' (Invoke-Api -Method GET -Path '/api/v1/tasks')

# 10. Запрос без токена к /all — 401
Show-Result '10. GET /api/v1/tasks/all без токена' (Invoke-Api -Method GET -Path '/api/v1/tasks/all')

Write-Host ''
Write-Host 'Готово.' -ForegroundColor Green
