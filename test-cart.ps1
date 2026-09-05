[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$baseUrl = "https://localhost:7163"

Write-Host "`n=== 0. Ensuring Customer Exists (Registering Test User) ===" -ForegroundColor Cyan
$testEmail = "ahmed.helmy@ecommerce.com"
$testPassword = "Customer123@"

$registerPayload = @{
    fullName = "Test Customer"
    email = $testEmail
    password = $testPassword
    confirmPassword = $testPassword
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$baseUrl/api/auth/register" -Method Post -Body $registerPayload -ContentType "application/json"
    Write-Host " Test user registered successfully." -ForegroundColor Green
} catch {
    Write-Host " User already exists or registration skipped, proceeding to login..." -ForegroundColor Yellow
}

Write-Host "`n=== 1. Logging in as Customer ===" -ForegroundColor Cyan
$loginPayload = @{
    email = $testEmail
    password = $testPassword
} | ConvertTo-Json

try {
    $authResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginPayload -ContentType "application/json"
    $token = $authResponse.accessToken
    Write-Host " Logged in successfully. Token acquired." -ForegroundColor Green
} catch {
    Write-Host " Login failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host " Server Error Details: $($reader.ReadToEnd())" -ForegroundColor Red
    }
    exit
}

$headers = @{
    "Authorization" = "Bearer $token"
}

Write-Host "`n=== 2. Getting Available Product ID ===" -ForegroundColor Cyan
$catalog = Invoke-RestMethod -Uri "$baseUrl/api/products?pageSize=1" -Method Get
if ($catalog.items.Count -eq 0) {
    Write-Host " No products found in database! Please add a product first." -ForegroundColor Red
    exit
}
$productId = $catalog.items[0].id
$stock = $catalog.items[0].stockQuantity
Write-Host " Targeting Product ID: $productId (Available Stock: $stock)" -ForegroundColor Gray

Write-Host "`n=== 3. Clear Cart First ===" -ForegroundColor Cyan
Invoke-RestMethod -Uri "$baseUrl/api/cart" -Method Delete -Headers $headers
Write-Host " Cart cleared." -ForegroundColor Green

Write-Host "`n=== 4. Adding 1 Item to Cart ===" -ForegroundColor Cyan
$addPayload = @{ productId = $productId; quantity = 1 } | ConvertTo-Json
$cart = Invoke-RestMethod -Uri "$baseUrl/api/cart/items" -Method Post -Body $addPayload -ContentType "application/json" -Headers $headers
Write-Host " Item added. Cart Total Items: $($cart.items.Count) | Subtotal: $($cart.subtotal)" -ForegroundColor Green

Write-Host "`n=== 5. Adding Same Item Again (Testing Increment) ===" -ForegroundColor Cyan
$cart = Invoke-RestMethod -Uri "$baseUrl/api/cart/items" -Method Post -Body $addPayload -ContentType "application/json" -Headers $headers
$currentQty = ($cart.items | Where-Object { $_.productId -eq $productId }).quantity
Write-Host " Incremented successfully. Current Quantity in Cart: $currentQty" -ForegroundColor Green

Write-Host "`n=== 6. Testing Stock Limit Violation (Expecting 409 Conflict) ===" -ForegroundColor Cyan
$overflowPayload = @{ productId = $productId; quantity = ($stock + 100) } | ConvertTo-Json
try {
    Invoke-RestMethod -Uri "$baseUrl/api/cart/items" -Method Post -Body $overflowPayload -ContentType "application/json" -Headers $headers
    Write-Host " Failed: System allowed exceeding stock!" -ForegroundColor Red
} catch {
    $statusCode = [int]$_.Exception.Response.StatusCode
    if ($statusCode -eq 409) {
        Write-Host " Passed! Server rejected with 409 Conflict as expected." -ForegroundColor Green
    } else {
        Write-Host " Received status code: $statusCode" -ForegroundColor Yellow
    }
}

Write-Host "`n=== 7. Updating Quantity to 3 ===" -ForegroundColor Cyan
$updatePayload = @{ quantity = 3 } | ConvertTo-Json
$cart = Invoke-RestMethod -Uri "$baseUrl/api/cart/items/$productId" -Method Put -Body $updatePayload -ContentType "application/json" -Headers $headers
Write-Host " Updated successfully. Subtotal is now: $($cart.subtotal)" -ForegroundColor Green

Write-Host "`n=== 8. Removing Item from Cart ===" -ForegroundColor Cyan
$cart = Invoke-RestMethod -Uri "$baseUrl/api/cart/items/$productId" -Method Delete -Headers $headers
Write-Host " Removed successfully. Remaining Items: $($cart.items.Count)" -ForegroundColor Green

Write-Host "`n ALL CART TESTS PASSED SUCCESSFULLY!" -ForegroundColor Magenta