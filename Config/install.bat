:: ==========================================================
:: 1. FILE CÀI ĐẶT DỊCH VỤ (install.bat)
:: Hướng dẫn: Chuột phải vào file này và chọn "Run as Administrator"
:: ==========================================================
@echo off
set SERVICE_NAME=SvcBackupService
set DISPLAY_NAME=Svc Service
:: %~dp0 trỏ đến thư mục chứa file batch này
set BIN_PATH="%~dp0Svc.exe"

echo Dang kiem tra quyen Administrator...
net session >nul 2>&1
if %errorLevel% neq 0 (
echo LOI: Vui long chay file nay voi quyen Administrator!
pause
exit /b
)

:: Dung dich vu truoc khi xoa
sc stop %SERVICE_NAME%

:: Xoa dich vu khoi he thong
sc delete %SERVICE_NAME%

echo Dang cai dat dich vu %SERVICE_NAME%...

:: Tao thu mục du lieu trong ProgramData neu chua co
if not exist "C:\ProgramData\Svc" mkdir "C:\ProgramData\Svc"

:: Tao dich vu voi duong dan tuong doi den file Svc.exe
sc create %SERVICE_NAME% binPath= %BIN_PATH% start= auto displayname= "%DISPLAY_NAME%"

:: Thiet lap mo ta
sc description %SERVICE_NAME% "Manages Windows Updates. If stopped, your devices will not be able to download and install the latest updates."

:: Thiet lap tu dong restart neu service bi loi (sau 1 phut)
sc failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/60000/restart/60000

:: Chay dich vu ngay lap tuc
sc start %SERVICE_NAME%

echo.
echo ==========================================================
echo DA CAI DAT THANH CONG!
echo ==========================================================
pause