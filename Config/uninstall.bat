:: ==========================================================
:: 2. FILE GỞ BỎ DỊCH VỤ (uninstall.bat)
:: Hướng dẫn: Chuột phải vào file này và chọn "Run as Administrator"
:: ==========================================================
@echo off
set SERVICE_NAME=SvcBackupService

echo Dang kiem tra quyen Administrator...
net session >nul 2>&1
if %errorLevel% neq 0 (
echo LOI: Vui long chay file nay voi quyen Administrator!
pause
exit /b
)

echo Dang dung va xoa dich vu %SERVICE_NAME%...

:: Dung dich vu truoc khi xoa
sc stop %SERVICE_NAME%

:: Xoa dich vu khoi he thong
sc delete %SERVICE_NAME%

echo.
echo ==========================================================
echo DA GO BO DICH VU THANH CONG!
echo Luu y: Du lieu trong C:\ProgramData\Svc khong bi xoa de bao mat config.
echo ==========================================================
pause