@echo off
REM ==========================================
REM check_camera_number 起動用バッチ（venv固定）
REM ==========================================

REM カレントディレクトリをバッチファイルのある場所に移動
cd /d %~dp0

REM ★重要：activateに頼らず、仮想環境の python.exe を明示指定する
set "PY=%~dp0ukemochi_env\Scripts\python.exe"

REM 仮想環境の python.exe が存在するかチェック
if not exist "%PY%" (
  echo [ERROR] 仮想環境の Python が見つかりません:
  echo %PY%
  pause
  exit /b 1
)

REM どの Python で動いているか表示（切り分け用）
echo [INFO] Using Python:
"%PY%" -c "import sys; print(sys.executable)"

REM pygrabber が入っているかチェック（入っていなければ案内して停止）
"%PY%" -c "import pygrabber" >nul 2>&1
if errorlevel 1 (
  echo [ERROR] pygrabber が仮想環境に見つかりません。
  echo 以下を実行してください:
  echo "%PY%" -m pip install pygrabber
  pause
  exit /b 1
)

REM Pythonスクリプトを実行（このウィンドウで実行して結果を見やすくする）
"%PY%" check_camera_number.py

pause
