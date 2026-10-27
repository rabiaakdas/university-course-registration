# PowerShell 7; önce dotnet build çalıştırın. Betik kendi API sürecini kapatır.
param([int]$Port = 5081, [switch]$VerifyDatabaseSource)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $projectRoot 'bin/Debug/net10.0/UniversityRegistration.dll'
$baseUrl = "http://127.0.0.1:$Port"

function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Get-JsonArray([string]$path) {
    $response = Invoke-WebRequest "$baseUrl$path" -TimeoutSec 5
    Assert ($response.StatusCode -eq 200) "$path HTTP 200 dönmedi."
    $contentType = $response.Headers['Content-Type'] -join ','
    Assert ($contentType -match '^application/json(?:\s*;|$)') "$path JSON içerik türü dönmedi."
    $document = [System.Text.Json.JsonDocument]::Parse($response.Content)
    try {
        Assert ($document.RootElement.ValueKind -eq 'Array') "$path JSON dizisi değil."
        Write-Host "PASS $path : HTTP 200; $contentType"
        return $document.RootElement.Clone()
    } finally { $document.Dispose() }
}

function Assert-Fields($item, [string]$expected) {
    $actual = ($item.EnumerateObject().Name | Sort-Object) -join ','
    Assert ($actual -ceq $expected) "Beklenmeyen alanlar: $actual"
}

function Wait-Api($process) {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Assert (-not $process.HasExited) 'API süreci beklenmeden kapandı.'
        try { $null = Invoke-WebRequest "$baseUrl/courses" -TimeoutSec 2; return }
        catch { Start-Sleep -Milliseconds 250 }
    }
    throw 'API başlatılamadı.'
}

function Invoke-AppSql([string]$statement) {
    $null = & sqlcmd -S '(localdb)\MSSQLLocalDB' -E -d university_registration_app -b -Q $statement
    Assert ($LASTEXITCODE -eq 0) 'Uygulama DB doğrulama SQL işlemi başarısız.'
}

Assert (Test-Path $dll) 'Önce dotnet build çalıştırın.'
# Başka bir uygulamayı yanlışlıkla doğrulamamak için portun boş olduğunu denetle.
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
try { $listener.Start() } finally { $listener.Stop() }
$apiProcess = Start-Process -FilePath (Get-Command dotnet).Source `
    -ArgumentList @(('"' + $dll + '"'), '--urls', $baseUrl) -WorkingDirectory $projectRoot `
    -WindowStyle Hidden -PassThru
try {
    Wait-Api $apiProcess

    $courses = @( (Get-JsonArray '/courses').EnumerateArray() )
    Assert ($courses.Count -eq 2) 'Tam iki ders olmalı.'
    Assert ($courses[0].GetProperty('id').GetInt32() -eq 1 -and $courses[1].GetProperty('id').GetInt32() -eq 2) 'Dersler Id sırasıyla dönmeli.'
    $expectedCourses = @{
        1 = @{ Code = 'CSE201'; Name = 'Veri Yapıları' }
        2 = @{ Code = 'CSE203'; Name = 'Veritabanı Yönetimi' }
    }
    $coursesById = @{}
    foreach ($course in $courses) {
        Assert-Fields $course 'code,id,name'
        $id = $course.GetProperty('id').GetInt32()
        Assert ($expectedCourses.ContainsKey($id) -and -not $coursesById.ContainsKey($id)) 'Ders kimliği yanlış veya tekrar ediyor.'
        Assert ($course.GetProperty('code').GetString() -ceq $expectedCourses[$id].Code) 'Ders kodu değişti.'
        Assert ($course.GetProperty('name').GetString() -ceq $expectedCourses[$id].Name) 'Ders adı değişti.'
        $coursesById[$id] = $course
    }

    $terms = @( (Get-JsonArray '/academic-terms').EnumerateArray() )
    Assert ($terms.Count -eq 2) 'Tam iki örnek dönem olmalı.'
    $expectedTerms = @{
        1 = @('2026-2027 Güz', '2026-09-21', '2027-01-15')
        2 = @('2026-2027 Bahar', '2027-02-15', '2027-06-11')
    }
    $termsById = @{}
    foreach ($term in $terms) {
        Assert-Fields $term 'endsOn,id,name,startsOn'
        $id = $term.GetProperty('id').GetInt32()
        Assert ($expectedTerms.ContainsKey($id) -and -not $termsById.ContainsKey($id)) 'Dönem kimliği yanlış veya tekrar ediyor.'
        $start = $term.GetProperty('startsOn').GetString()
        $end = $term.GetProperty('endsOn').GetString()
        $startDate = [DateOnly]::ParseExact($start, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        $endDate = [DateOnly]::ParseExact($end, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        Assert ($startDate -lt $endDate) 'Dönem tarih sırası yanlış.'
        Assert ($term.GetProperty('name').GetString() -ceq $expectedTerms[$id][0] -and $start -ceq $expectedTerms[$id][1] -and $end -ceq $expectedTerms[$id][2]) 'Dönem içeriği yanlış.'
        $termsById[$id] = $term
    }

    $offerings = @( (Get-JsonArray '/course-offerings').EnumerateArray() )
    Assert ($offerings.Count -eq 3) 'Tam üç örnek açılış olmalı.'
    $expectedOfferings = @{ 1 = @(1, 1, 'A', 40); 2 = @(1, 1, 'B', 30); 3 = @(1, 2, 'A', 35) }
    $seenIds = @{}
    $seenSections = @{}
    foreach ($offering in $offerings) {
        Assert-Fields $offering 'academicTermId,capacity,courseCode,courseId,courseName,id,sectionCode,termName'
        $id = $offering.GetProperty('id').GetInt32()
        $courseId = $offering.GetProperty('courseId').GetInt32()
        $termId = $offering.GetProperty('academicTermId').GetInt32()
        $section = $offering.GetProperty('sectionCode').GetString()
        $capacity = $offering.GetProperty('capacity').GetInt32()
        Assert ($expectedOfferings.ContainsKey($id) -and -not $seenIds.ContainsKey($id)) 'Açılış kimliği yanlış veya tekrar ediyor.'
        Assert ($coursesById.ContainsKey($courseId) -and $termsById.ContainsKey($termId)) 'Açılış olmayan ders/döneme işaret ediyor.'
        $expected = $expectedOfferings[$id]
        Assert ($courseId -eq $expected[0] -and $termId -eq $expected[1] -and $section -ceq $expected[2] -and $capacity -eq $expected[3]) 'Açılışın ders/dönem/şube/kapasite değerleri yanlış.'
        Assert ($offering.GetProperty('courseCode').GetString() -ceq $coursesById[$courseId].GetProperty('code').GetString()) 'Ders kodu eşleşmiyor.'
        Assert ($offering.GetProperty('courseName').GetString() -ceq $coursesById[$courseId].GetProperty('name').GetString()) 'Ders adı eşleşmiyor.'
        Assert ($offering.GetProperty('termName').GetString() -ceq $termsById[$termId].GetProperty('name').GetString()) 'Dönem adı eşleşmiyor.'
        $sectionKey = "$courseId/$termId/$section"
        Assert (-not $seenSections.ContainsKey($sectionKey)) 'Aynı ders ve dönemde şube tekrar ediyor.'
        $seenIds[$id] = $true
        $seenSections[$sectionKey] = $true
    }
    Assert (-not $apiProcess.HasExited) 'Doğrulama sırasında API kapandı.'
    Write-Host 'PASS: Üç endpoint, alanlar, tarihler, Türkçe değerler, kimlik eşleşmeleri, A/B şubeleri ve 40/30 kapasiteleri doğrulandı. Kimlik doğrulama bilgisi gönderilmedi.'

    if ($VerifyDatabaseSource) {
        # Read configuration without printing it; refuse any other mutation target.
        $config = Get-Content (Join-Path $projectRoot 'appsettings.json') -Raw | ConvertFrom-Json
        $configuredConnection = $config.ConnectionStrings.UniversityDatabase
        if ($env:ConnectionStrings__UniversityDatabase) { $configuredConnection = $env:ConnectionStrings__UniversityDatabase }
        $target = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($configuredConnection)
        Assert ($target.DataSource -eq '(localdb)\MSSQLLocalDB' -and $target.InitialCatalog -eq 'university_registration_app' -and $target.IntegratedSecurity) 'DB kanıtı yalnızca varsayılan yerel uygulama DB hedefinde çalışır.'

        try {
            # Commit makes the change visible to the API's separate connection.
            Invoke-AppSql @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE dbo.Courses SET Name=N'Veri Yapıları (DB doğrulaması)'
WHERE Id=1 AND Code=N'CSE201' AND Credits=4
  AND CONVERT(varbinary(max),Name)=CONVERT(varbinary(max),N'Veri Yapıları');
IF @@ROWCOUNT<>1 THROW 51000, 'Unexpected initial course value; refusing overwrite.', 1;
COMMIT;
'@
            $changed = @( (Get-JsonArray '/courses').EnumerateArray() )
            Assert ($changed[0].GetProperty('name').GetString() -ceq 'Veri Yapıları (DB doğrulaması)') 'Commit edilen DB değişikliği HTTP yanıtında görülmedi.'
            Write-Host 'PASS: Commit edilen ders adı gerçek HTTP yanıtına yansıdı.'
        } finally {
            Invoke-AppSql @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.Courses WITH (UPDLOCK,HOLDLOCK)
 WHERE Id=1 AND Code=N'CSE201' AND Credits=4
 AND CONVERT(varbinary(max),Name) IN (CONVERT(varbinary(max),N'Veri Yapıları'),CONVERT(varbinary(max),N'Veri Yapıları (DB doğrulaması)')))
 THROW 51000, 'Unexpected current course value; refusing cleanup overwrite.', 1;
UPDATE dbo.Courses SET Name=N'Veri Yapıları'
WHERE Id=1 AND CONVERT(varbinary(max),Name)=CONVERT(varbinary(max),N'Veri Yapıları (DB doğrulaması)');
COMMIT;
'@
            $restored = @( (Get-JsonArray '/courses').EnumerateArray() )
            Assert ($restored[0].GetProperty('name').GetString() -ceq 'Veri Yapıları') 'Eski ad HTTP üzerinden doğrulanamadı.'
            Write-Host 'PASS: Eski ders adı geri yüklendi ve HTTP ile doğrulandı.'
        }

        Stop-Process -Id $apiProcess.Id
        $apiProcess.WaitForExit()
        $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source `
            -ArgumentList @(('"' + $dll + '"'), '--urls', $baseUrl) -WorkingDirectory $projectRoot `
            -WindowStyle Hidden -PassThru
        Wait-Api $apiProcess
        $restarted = @( (Get-JsonArray '/courses').EnumerateArray() )
        Assert ($restarted.Count -eq 2 -and $restarted[0].GetProperty('name').GetString() -ceq 'Veri Yapıları') 'Yeniden başlatma sonrası DB okuması başarısız.'

        $client = [System.Net.Http.HttpClient]::new()
        try {
            $requests = @(1..8 | ForEach-Object { $client.GetAsync("$baseUrl/courses") })
            [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$requests)
            foreach ($request in $requests) {
                $response = $request.GetAwaiter().GetResult()
                try {
                    Assert ([int]$response.StatusCode -eq 200) 'Eşzamanlı istek başarısız.'
                    Assert ($response.Content.Headers.ContentType.MediaType -eq 'application/json') 'Eşzamanlı yanıt JSON değil.'
                    $document = [System.Text.Json.JsonDocument]::Parse($response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
                    try {
                        Assert ($document.RootElement.GetArrayLength() -eq 2) 'Eşzamanlı yanıtta ders sayısı yanlış.'
                        foreach ($course in $document.RootElement.EnumerateArray()) { Assert-Fields $course 'code,id,name' }
                        Assert ($document.RootElement[0].GetProperty('name').GetString() -ceq 'Veri Yapıları') 'Eşzamanlı yanıtta ders adı yanlış.'
                    } finally { $document.Dispose() }
                } finally { $response.Dispose() }
            }
        } finally { $client.Dispose() }
        Write-Host 'PASS: Yeniden başlatma sonrası DB okuması ve 8 eşzamanlı request başarılı. Bu bir kontenjan/concurrency garantisi değildir.'
    }
} finally {
    if (-not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id
        $apiProcess.WaitForExit()
    }
    Write-Host "Geçici API süreci kapatıldı: $($apiProcess.HasExited)"
}
