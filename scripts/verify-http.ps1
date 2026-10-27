# PowerShell 7; önce dotnet build çalıştırın. Betik kendi API sürecini kapatır.
param([int]$Port = 5081)

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
    Assert ($courses.Count -ge 2) 'Örnek dersler bulunmalı.'
    $previousId = [int]::MinValue
    $expectedCourses = @{
        1 = @{ Code = 'CSE201'; Name = 'Veri Yapıları' }
        2 = @{ Code = 'CSE203'; Name = 'Veritabanı Yönetimi' }
    }
    $coursesById = @{}
    foreach ($course in $courses) {
        Assert-Fields $course 'code,id,name'
        $id = $course.GetProperty('id').GetInt32()
        Assert ($id -gt $previousId) 'Dersler benzersiz ve Id sırasıyla dönmeli.'
        $previousId = $id
        if ($expectedCourses.ContainsKey($id)) {
            Assert ($course.GetProperty('code').GetString() -ceq $expectedCourses[$id].Code) 'Ders kodu değişti.'
            Assert ($course.GetProperty('name').GetString() -ceq $expectedCourses[$id].Name) 'Ders adı değişti.'
        }
        $coursesById[$id] = $course
    }

    Assert ($coursesById.ContainsKey(1) -and $coursesById.ContainsKey(2)) 'İki örnek ders bulunmalı.'

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

} finally {
    if (-not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id
        $apiProcess.WaitForExit()
    }
    Write-Host "Geçici API süreci kapatıldı: $($apiProcess.HasExited)"
}
