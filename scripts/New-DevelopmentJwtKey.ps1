param([string]$Directory = (Join-Path $env:LOCALAPPDATA 'UniversityRegistration/jwt-development'))
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($Directory)
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($target -eq $repo -or $target.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Key directory must be outside the repository.' }
if (Test-Path -LiteralPath $target) { throw 'Target exists; no key will be replaced.' }
New-Item -ItemType Directory -Path $target | Out-Null
# Protect this newly created directory before private material is written.
$acl = [System.Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$rule = [System.Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $target -AclObject $acl
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    [IO.File]::WriteAllText((Join-Path $target 'private.pem'), $rsa.ExportPkcs8PrivateKeyPem())
    [IO.File]::WriteAllText((Join-Path $target 'public.pem'), $rsa.ExportSubjectPublicKeyInfoPem())
} finally { $rsa.Dispose() }
Write-Output 'Development key files created; content not printed. Configure their absolute paths explicitly.'
