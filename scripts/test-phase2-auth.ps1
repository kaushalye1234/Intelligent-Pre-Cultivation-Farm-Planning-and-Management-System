$loginBody = @{
    email = "admin@agriassist.local"
    password = "Admin@2026"
} | ConvertTo-Json

$login = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
Write-Output "LOGIN_USER=$($login.user.email)"
Write-Output "LOGIN_ROLE=$($login.user.role)"
Write-Output "TOKEN_LENGTH=$($login.accessToken.Length)"

$headers = @{
    Authorization = "Bearer $($login.accessToken)"
}

$profile = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/profile" -Method Get -Headers $headers
Write-Output "PROFILE_USER=$($profile.email)"
Write-Output "PROFILE_ROLE=$($profile.role)"

$users = Invoke-RestMethod -Uri "http://localhost:5000/api/users?page=1&pageSize=10" -Method Get -Headers $headers
Write-Output "USER_COUNT=$($users.totalCount)"
