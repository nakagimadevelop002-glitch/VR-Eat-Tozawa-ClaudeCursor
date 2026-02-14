@echo off
REM カレントディレクトリをバッチファイルのある場所に移動
cd /d %~dp0

REM 仮想環境を有効化
REM call .\ukemochi_env\Scripts\activate

REM Pythonスクリプトを実行（バックグラウンド）
REM start cmd /k python camera_server_yolo_keyboard_color_v5.py

set "PY=%~dp0ukemochi_env\Scripts\python.exe"
start cmd /k "%PY%" camera_server_yolo_keyboard_color_v5.py

REM 5秒待機
timeout /t 5 /nobreak >nul

REM ブラウザでURLを開く
start http://localhost:5000/video_feed
start http://localhost:5000