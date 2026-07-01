param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd("/")
$results = New-Object System.Collections.Generic.List[object]

function Add-Result($Name, $Status, $Detail = "") {
    $script:results.Add([pscustomobject]@{
        Step = $Name
        Status = $Status
        Detail = $Detail
    }) | Out-Null
}

function Json-Body($Body) {
    return ($Body | ConvertTo-Json -Depth 12 -Compress)
}

function Invoke-ApiJson($Method, $Path, $Body = $null, $Token = $null) {
    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $params = @{
        Method = $Method
        Uri = "$base$Path"
        Headers = $headers
    }

    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = Json-Body $Body
    }

    return Invoke-RestMethod @params
}

function Invoke-ApiStatus($Method, $Path, [int[]]$Expected, $Body = $null, $Token = $null) {
    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $params = @{
        UseBasicParsing = $true
        Method = $Method
        Uri = "$base$Path"
        Headers = $headers
    }

    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = Json-Body $Body
    }

    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        if ($_.Exception.Response -eq $null) {
            throw
        }

        $status = [int]$_.Exception.Response.StatusCode
        $stream = $_.Exception.Response.GetResponseStream()
        if ($stream) {
            $reader = New-Object System.IO.StreamReader($stream)
            $content = $reader.ReadToEnd()
        }
        else {
            $content = ""
        }
    }

    if ($Expected -notcontains $status) {
        throw "Expected HTTP $($Expected -join "/") but got $status for $Method $Path. Body: $content"
    }

    return [pscustomobject]@{
        StatusCode = $status
        Content = $content
    }
}

function Wait-Gateway() {
    for ($i = 1; $i -le 20; $i++) {
        try {
            Invoke-ApiJson GET "/" | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Gateway did not become ready."
}

function Expand-Items($Value) {
    if ($null -eq $Value) {
        return
    }

    foreach ($item in $Value) {
        Write-Output $item
    }
}

Wait-Gateway

$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
$testEmail = "smoke.qa@library.local"
$testPassword = "Reader@123"
$testIsbn = "SMOKE-$stamp"
$testCategoryName = "QA Smoke"

$adminToken = $null
$librarianToken = $null
$readerToken = $null
$testReader = $null
$testBook = $null
$borrow = $null
$renewed = $null
$returned = $null

try {
    Add-Result "Gateway root" "PASS" (Invoke-ApiJson GET "/")
}
catch {
    Add-Result "Gateway root" "FAIL" $_.Exception.Message
}

try {
    $admin = Invoke-ApiJson POST "/api/auth/login" @{
        email = "admin@library.local"
        password = "Admin@123"
    }
    $adminToken = $admin.accessToken
    if (-not $adminToken -or $admin.role -ne "Admin") {
        throw "Admin token/role invalid"
    }
    Add-Result "Login Admin" "PASS" $admin.email
}
catch {
    Add-Result "Login Admin" "FAIL" $_.Exception.Message
}

try {
    $librarian = Invoke-ApiJson POST "/api/auth/login" @{
        email = "librarian@library.local"
        password = "Librarian@123"
    }
    $librarianToken = $librarian.accessToken
    if (-not $librarianToken -or $librarian.role -ne "Librarian") {
        throw "Librarian token/role invalid"
    }
    Add-Result "Login Librarian" "PASS" $librarian.email
}
catch {
    Add-Result "Login Librarian" "FAIL" $_.Exception.Message
}

try {
    $oldSmokeBooks = @(Expand-Items (Invoke-ApiJson GET "/api/books?category=QA%20Smoke&includeArchived=false"))
    foreach ($oldBook in @($oldSmokeBooks | Where-Object { $_.isbn -like "SMOKE-*" })) {
        Invoke-ApiStatus DELETE "/api/books/$($oldBook.id)" @(204) $null $adminToken | Out-Null
    }
    Add-Result "Cleanup old smoke books" "PASS" "$($oldSmokeBooks.Count) candidates"
}
catch {
    Add-Result "Cleanup old smoke books" "FAIL" $_.Exception.Message
}

try {
    $books = @(Expand-Items (Invoke-ApiJson GET "/api/books"))
    if ($books.Count -lt 1) {
        throw "No books returned"
    }
    Add-Result "Catalog list books" "PASS" "$($books.Count) books"
}
catch {
    Add-Result "Catalog list books" "FAIL" $_.Exception.Message
}

try {
    $categories = @(Expand-Items (Invoke-ApiJson GET "/api/books/categories"))
    if ($categories.Count -lt 1) {
        throw "No categories returned"
    }
    Add-Result "Catalog categories" "PASS" "$($categories.Count) categories"
}
catch {
    Add-Result "Catalog categories" "FAIL" $_.Exception.Message
}

try {
    $managedCategories = @(Expand-Items (Invoke-ApiJson GET "/api/book-categories"))
    $testCategory = $managedCategories | Where-Object { $_.name -eq $testCategoryName } | Select-Object -First 1
    if (-not $testCategory) {
        $testCategory = Invoke-ApiJson POST "/api/book-categories" @{
            name = $testCategoryName
            description = "Category used by smoke tests."
        } $adminToken
        Add-Result "Catalog create category by Admin" "PASS" $testCategory.name
    }
    else {
        Add-Result "Catalog category fixture ready" "PASS" $testCategory.name
    }
}
catch {
    Add-Result "Catalog category fixture ready" "FAIL" $_.Exception.Message
}

try {
    $deleteBook = Invoke-ApiJson POST "/api/books" @{
        isbn = "DELETE-$stamp"
        title = "Smoke Permanent Delete Book $stamp"
        author = "Codex QA"
        publisher = "BTL Fullstack"
        publishedYear = 2026
        category = $testCategoryName
        totalCopies = 1
        minimumCopies = 0
        coverImageUrl = $null
        description = "Temporary book created only for permanent delete smoke test."
        content = "Temporary content note for permanent delete smoke test."
    } $adminToken

    Invoke-ApiStatus DELETE "/api/books/$($deleteBook.id)/permanent" @(204) $null $adminToken | Out-Null
    Invoke-ApiStatus GET "/api/books/$($deleteBook.id)" @(404) $null $adminToken | Out-Null
    Add-Result "Catalog permanent delete by Admin" "PASS" $deleteBook.id
}
catch {
    Add-Result "Catalog permanent delete by Admin" "FAIL" $_.Exception.Message
}

try {
    $summary = Invoke-ApiJson GET "/api/books/summary"
    if ($summary.totalBooks -lt 1 -or $summary.totalCopies -lt 1) {
        throw "Invalid inventory summary"
    }
    Add-Result "Catalog summary public route" "PASS" "books=$($summary.totalBooks), copies=$($summary.totalCopies)"
}
catch {
    Add-Result "Catalog summary public route" "FAIL" $_.Exception.Message
}

try {
    $testBook = Invoke-ApiJson POST "/api/books" @{
        isbn = $testIsbn
        title = "Smoke Test Library Book $stamp"
        author = "Codex QA"
        publisher = "BTL Fullstack"
        publishedYear = 2026
        category = $testCategoryName
        totalCopies = 2
        minimumCopies = 0
        coverImageUrl = $null
        description = "Temporary book created by API smoke test."
        content = "Temporary content note created by API smoke test."
    } $adminToken
    if (-not $testBook.id -or $testBook.availableCopies -ne 2) {
        throw "Created book response invalid"
    }
    Add-Result "Catalog create book" "PASS" $testBook.id
}
catch {
    Add-Result "Catalog create book" "FAIL" $_.Exception.Message
}

try {
    $search = @(Expand-Items (Invoke-ApiJson GET "/api/books/search?keyword=$testIsbn"))
    if (@($search | Where-Object { $_.isbn -eq $testIsbn }).Count -lt 1) {
        throw "Created book not found by search"
    }
    Add-Result "Catalog search" "PASS" "$($search.Count) result(s)"
}
catch {
    Add-Result "Catalog search" "FAIL" $_.Exception.Message
}

try {
    $updatedBook = Invoke-ApiJson PUT "/api/books/$($testBook.id)" @{
        isbn = $testIsbn
        title = "Smoke Test Library Book $stamp Updated"
        author = "Codex QA"
        publisher = "BTL Fullstack"
        publishedYear = 2026
        category = $testCategoryName
        totalCopies = 2
        minimumCopies = 0
        coverImageUrl = $null
        description = "Temporary book updated by API smoke test."
        content = "Temporary content note updated by API smoke test."
    } $librarianToken
    if ($updatedBook.title -notlike "*Updated") {
        throw "Book update was not applied"
    }
    $testBook = $updatedBook
    Add-Result "Catalog update book by Librarian" "PASS" $updatedBook.title
}
catch {
    Add-Result "Catalog update book by Librarian" "FAIL" $_.Exception.Message
}

try {
    $deny = Invoke-ApiStatus POST "/api/books" @(401, 403) @{
        isbn = "DENY-$stamp"
        title = "Denied"
        author = "Denied"
        publisher = "Denied"
        publishedYear = 2026
        category = "Denied"
        totalCopies = 1
        minimumCopies = 0
        coverImageUrl = $null
        description = $null
    } $null
    Add-Result "Catalog anonymous create denied" "PASS" "HTTP $($deny.StatusCode)"
}
catch {
    Add-Result "Catalog anonymous create denied" "FAIL" $_.Exception.Message
}

try {
    $policy = Invoke-ApiJson GET "/api/circulation-rules" $null $librarianToken
    if ($policy.defaultBorrowDays -lt 1) {
        throw "Policy invalid"
    }
    Add-Result "Circulation policy read by Librarian" "PASS" "default=$($policy.defaultBorrowDays), max=$($policy.maxActiveBorrowingsPerReader)"
}
catch {
    Add-Result "Circulation policy read by Librarian" "FAIL" $_.Exception.Message
}

try {
    $samePolicy = Invoke-ApiJson PUT "/api/circulation-rules" @{
        maxActiveBorrowingsPerReader = [int]$policy.maxActiveBorrowingsPerReader
        defaultBorrowDays = [int]$policy.defaultBorrowDays
        maxRenewalDays = [int]$policy.maxRenewalDays
        finePerOverdueDay = [decimal]$policy.finePerOverdueDay
        allowReaderSelfCheckout = [bool]$policy.allowReaderSelfCheckout
    } $adminToken
    Add-Result "Circulation policy update by Admin" "PASS" "fine/day=$($samePolicy.finePerOverdueDay)"
}
catch {
    Add-Result "Circulation policy update by Admin" "FAIL" $_.Exception.Message
}

try {
    $deny = Invoke-ApiStatus PUT "/api/circulation-rules" @(403) @{
        maxActiveBorrowingsPerReader = 3
        defaultBorrowDays = 14
        maxRenewalDays = 7
        finePerOverdueDay = 5000
        allowReaderSelfCheckout = $true
    } $librarianToken
    Add-Result "Circulation policy update denied for Librarian" "PASS" "HTTP $($deny.StatusCode)"
}
catch {
    Add-Result "Circulation policy update denied for Librarian" "FAIL" $_.Exception.Message
}

try {
    $users = @(Expand-Items (Invoke-ApiJson GET "/api/users" $null $adminToken))
    if ($users.Count -lt 1) {
        throw "No users returned"
    }
    Add-Result "Users list by Admin" "PASS" "$($users.Count) users"
}
catch {
    Add-Result "Users list by Admin" "FAIL" $_.Exception.Message
}

try {
    $deny = Invoke-ApiStatus GET "/api/users" @(403) $null $librarianToken
    Add-Result "Users list denied for Librarian" "PASS" "HTTP $($deny.StatusCode)"
}
catch {
    Add-Result "Users list denied for Librarian" "FAIL" $_.Exception.Message
}

try {
    $testReader = $users | Where-Object { $_.email -eq $testEmail } | Select-Object -First 1
    if (-not $testReader) {
        $testReader = Invoke-ApiJson POST "/api/users" @{
            email = $testEmail
            password = $testPassword
            fullName = "Smoke QA Reader"
            role = "Reader"
        } $adminToken
        Add-Result "Users create Reader by Admin" "PASS" $testReader.userId
    }
    else {
        Invoke-ApiStatus PUT "/api/users/$($testReader.userId)/status" @(204) @{ isActive = $true } $adminToken | Out-Null
        Add-Result "Users fixture Reader ready" "PASS" $testReader.userId
    }
}
catch {
    Add-Result "Users fixture Reader ready" "FAIL" $_.Exception.Message
}

try {
    $readerLogin = Invoke-ApiJson POST "/api/auth/login" @{
        email = $testEmail
        password = $testPassword
    }
    $readerToken = $readerLogin.accessToken
    if (-not $readerToken -or $readerLogin.role -ne "Reader") {
        throw "Reader token/role invalid"
    }
    Add-Result "Login test Reader" "PASS" $readerLogin.email
}
catch {
    Add-Result "Login test Reader" "FAIL" $_.Exception.Message
}

try {
    $readerReport = Invoke-ApiJson GET "/api/reports/reader/$($testReader.userId)" $null $readerToken
    if ([string]$readerReport.readerId -ne [string]$testReader.userId) {
        throw "Reader report user mismatch"
    }
    Add-Result "Reader own report" "PASS" "borrowings=$($readerReport.totalBorrowings)"
}
catch {
    Add-Result "Reader own report" "FAIL" $_.Exception.Message
}

try {
    $deny = Invoke-ApiStatus GET "/api/circulations" @(403) $null $readerToken
    Add-Result "Reader cannot list all circulations" "PASS" "HTTP $($deny.StatusCode)"
}
catch {
    Add-Result "Reader cannot list all circulations" "FAIL" $_.Exception.Message
}

try {
    $borrow = Invoke-ApiJson POST "/api/circulations" @{
        readerId = $testReader.userId
        bookId = $testBook.id
        borrowDays = 1
    } $readerToken
    if (-not $borrow.borrowingId -or $borrow.status -ne "Borrowed") {
        throw "Borrow response invalid"
    }
    Add-Result "Reader self checkout" "PASS" $borrow.borrowingId
}
catch {
    Add-Result "Reader self checkout" "FAIL" $_.Exception.Message
}

try {
    $deny = Invoke-ApiStatus POST "/api/circulations" @(400) @{
        readerId = $testReader.userId
        bookId = $testBook.id
        borrowDays = 1
    } $readerToken
    Add-Result "Duplicate active borrow denied" "PASS" "HTTP $($deny.StatusCode)"
}
catch {
    Add-Result "Duplicate active borrow denied" "FAIL" $_.Exception.Message
}

try {
    $renewed = Invoke-ApiJson POST "/api/circulations/$($borrow.borrowingId)/renew" @{
        extraDays = 1
    } $readerToken
    if ($renewed.status -ne "Borrowed") {
        throw "Renew response invalid"
    }
    Add-Result "Reader renew borrowing" "PASS" "due=$($renewed.dueAtUtc)"
}
catch {
    Add-Result "Reader renew borrowing" "FAIL" $_.Exception.Message
}

try {
    $returnAt = ([DateTimeOffset]::Parse($renewed.dueAtUtc.ToString())).AddDays(2.25).UtcDateTime.ToString("o")
    $returned = Invoke-ApiJson POST "/api/circulations/$($borrow.borrowingId)/return" @{
        returnedAtUtc = $returnAt
    } $readerToken
    if ($returned.status -ne "Returned" -or $returned.fineAmount -le 0) {
        throw "Return/fine invalid. status=$($returned.status), fine=$($returned.fineAmount)"
    }
    Add-Result "Reader return overdue with fine" "PASS" "fine=$($returned.fineAmount)"
}
catch {
    Add-Result "Reader return overdue with fine" "FAIL" $_.Exception.Message
}

try {
    $paid = Invoke-ApiJson POST "/api/circulations/$($borrow.borrowingId)/fine-payment" @{
        amount = [decimal]$returned.outstandingFine
    } $adminToken
    if ($paid.outstandingFine -ne 0) {
        throw "Outstanding fine remains $($paid.outstandingFine)"
    }
    Add-Result "Admin pay fine" "PASS" "paid=$($paid.finePaidAmount)"
}
catch {
    Add-Result "Admin pay fine" "FAIL" $_.Exception.Message
}

try {
    $readerBorrowings = @(Expand-Items (Invoke-ApiJson GET "/api/circulations/reader/$($testReader.userId)" $null $readerToken))
    if (@($readerBorrowings | Where-Object { [string]$_.id -eq [string]$borrow.borrowingId }).Count -lt 1) {
        throw "Borrowing not visible to reader"
    }
    Add-Result "Reader sees own borrowings" "PASS" "$($readerBorrowings.Count) records"
}
catch {
    Add-Result "Reader sees own borrowings" "FAIL" $_.Exception.Message
}

try {
    $fines = Invoke-ApiJson GET "/api/circulations/fines?readerId=$($testReader.userId)" $null $readerToken
    if ($fines.paidAmount -le 0 -or $fines.debtAmount -ne 0) {
        throw "Fine summary invalid: paid=$($fines.paidAmount), debt=$($fines.debtAmount)"
    }
    Add-Result "Reader fine summary after payment" "PASS" "paid=$($fines.paidAmount), debt=$($fines.debtAmount)"
}
catch {
    Add-Result "Reader fine summary after payment" "FAIL" $_.Exception.Message
}

try {
    $allCircs = @(Expand-Items (Invoke-ApiJson GET "/api/circulations" $null $librarianToken))
    if ($allCircs.Count -lt 1) {
        throw "No circulation records returned"
    }
    Add-Result "Librarian list all circulations" "PASS" "$($allCircs.Count) records"
}
catch {
    Add-Result "Librarian list all circulations" "FAIL" $_.Exception.Message
}

try {
    $dashboard = Invoke-ApiJson GET "/api/reports/dashboard" $null $adminToken
    if ($dashboard.totalReaders -lt 1) {
        throw "Dashboard invalid"
    }
    Add-Result "Reports dashboard" "PASS" "readers=$($dashboard.totalReaders), fine=$($dashboard.totalFineCollected)"
}
catch {
    Add-Result "Reports dashboard" "FAIL" $_.Exception.Message
}

try {
    $overdueReaders = @(Expand-Items (Invoke-ApiJson GET "/api/reports/overdue-readers" $null $librarianToken))
    Add-Result "Reports overdue readers" "PASS" "$($overdueReaders.Count) rows"
}
catch {
    Add-Result "Reports overdue readers" "FAIL" $_.Exception.Message
}

try {
    Invoke-ApiStatus PUT "/api/users/$($testReader.userId)/status" @(204) @{ isActive = $false } $adminToken | Out-Null
    Invoke-ApiStatus PUT "/api/users/$($testReader.userId)/status" @(204) @{ isActive = $true } $adminToken | Out-Null
    Add-Result "Admin toggle reader status" "PASS" "disabled then enabled"
}
catch {
    Add-Result "Admin toggle reader status" "FAIL" $_.Exception.Message
}

try {
    Invoke-ApiStatus DELETE "/api/books/$($testBook.id)" @(204) $null $adminToken | Out-Null
    $archived = Invoke-ApiJson GET "/api/books/$($testBook.id)" $null $adminToken
    if (-not $archived.isArchived) {
        throw "Book was not archived"
    }
    Add-Result "Catalog archive test book" "PASS" "archived=true"
}
catch {
    Add-Result "Catalog archive test book" "FAIL" $_.Exception.Message
}

$failures = @($results | Where-Object { $_.Status -ne "PASS" })
$results | Format-Table -AutoSize | Out-String -Width 240

if ($failures.Count -gt 0) {
    Write-Host "FAIL_COUNT=$($failures.Count)"
    exit 1
}

Write-Host "ALL_API_SMOKE_TESTS_PASSED"
