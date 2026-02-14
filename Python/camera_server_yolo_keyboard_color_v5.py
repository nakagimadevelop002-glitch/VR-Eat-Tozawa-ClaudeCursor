from flask import Flask, Response
import cv2
from ultralytics import YOLO
import numpy as np

app = Flask(__name__)
# camera = cv2.VideoCapture(00)
camera = cv2.VideoCapture(0, cv2.CAP_MSMF)
# camera = cv2.VideoCapture(0, cv2.CAP_DSHOW)

# model = YOLO('yolov8n-seg.pt') #元々リンゴなどを検知できていたモデル
model = YOLO('model_seg.pt') #新たに作成された容器を検知できるモデル

mode = 'mask'
original_frame = None
background_color = (0, 255, 0)  # ★初期背景色を緑（B, G, R）に変更

# 前回のマスクと検出カウントを保持
last_mask_all = None
no_detect_count = 0
NO_DETECT_MAX = 5  # 最大でこのフレーム数は前回のマスクを使う


@app.route('/')
def index():
    return '''
    <h1>YOLOv8 食べ物検出サンプル</h1>
    <p>映像は <a href="/video_feed" target="_blank">こちら</a> から確認できます。</p>

    <p>モード切替：</p>
    <button onclick="setMode('detect')">検出モード (A)</button>
    <button onclick="setMode('mask')">マスクモード (B)</button>
    <button onclick="setMode('restore')">復元モード (C)</button>

    <p>背景色切替：</p>
    <button onclick="setColor('black')">黒</button>
    <button onclick="setColor('green')">緑</button>
    <button onclick="setColor('blue')">青</button>
    <button onclick="setColor('red')">赤</button>

    <p id="status"></p>
    <script>
      function setMode(mode) {
        fetch('/set_mode/' + mode)
          .then(response => response.text())
          .then(text => { document.getElementById('status').innerText = text; });
      }

      function setColor(color) {
        fetch('/set_color/' + color)
          .then(response => response.text())
          .then(text => { document.getElementById('status').innerText = text; });
      }
    </script>
    '''


@app.route('/set_mode/<new_mode>')
def set_mode(new_mode):
    global mode
    if new_mode in ['idle', 'detect', 'mask', 'restore']:
        mode = new_mode
        return f'Mode changed to: {mode}'
    return 'Invalid mode', 400


@app.route('/set_color/<color>')
def set_color(color):
    global background_color
    color_map = {
        'black': (0, 0, 0),
        'green': (0, 255, 0),
        'blue': (255, 0, 0),
        'red': (0, 0, 255),
    }
    if color in color_map:
        background_color = color_map[color]
        return f'Background color set to: {color}'
    return 'Invalid color', 400


def generate_masked_frames():
    global original_frame, mode, background_color
    global last_mask_all, no_detect_count

    # ✅ COCOの「食べ物」クラスのみ検出するリスト
    food_classes = {
        "banana", "apple", "sandwich", "orange", "broccoli", "carrot",
        "hot dog", "pizza", "donut", "cake"
    }

    while True:
        success, frame = camera.read()
        if not success:
            break

        display_frame = frame.copy()
        mask_all = None

        if mode in ['detect', 'mask', 'masked']:
            try:
                # conf を少し上げて誤検出を減らす
                results = model(frame, stream=True, conf=0.45)
            except Exception as e:
                print(f"YOLO 推論エラー: {e}")
                continue

            for r in results:
                if r.masks is not None and len(r.boxes) > 0:
                    masks = r.masks.data
                    boxes = r.boxes
                    mask_all = np.zeros((frame.shape[0], frame.shape[1]), dtype=bool)
                    for i in range(masks.shape[0]):
                        cls_id = int(boxes.cls[i])
                        cls_name = model.names.get(cls_id, "")
                        # ✅ 食べ物クラス以外をスキップ
                        if cls_name not in food_classes:
                            continue
                        np_mask = masks[i].cpu().numpy().astype(bool)
                        mask_all |= np_mask

            # 検出が無い場合は前回のマスクを使用
            if mask_all is None:
                no_detect_count += 1
                if last_mask_all is not None and no_detect_count <= NO_DETECT_MAX:
                    mask_all = last_mask_all
            else:
                last_mask_all = mask_all
                no_detect_count = 0

            if mask_all is not None:
                mask_uint8 = (mask_all.astype(np.uint8) * 255)
                mask_resized = cv2.resize(mask_uint8, (frame.shape[1], frame.shape[0]))

                if mode == 'detect':
                    contours, _ = cv2.findContours(mask_resized, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
                    cv2.drawContours(display_frame, contours, -1, (0, 255, 0), 2)

                elif mode in ['mask', 'masked']:
                    inv_mask = cv2.bitwise_not(mask_resized)
                    food_part = cv2.bitwise_and(frame, frame, mask=mask_resized)
                    background_custom = np.full_like(frame, background_color)
                    background_part = cv2.bitwise_and(background_custom, background_custom, mask=inv_mask)
                    display_frame = cv2.add(food_part, background_part)

                    if mode == 'mask':
                        mode = 'masked'

        elif mode == 'restore':
            if original_frame is not None:
                display_frame = original_frame.copy()
            else:
                display_frame = frame.copy()
            original_frame = None
            mode = 'idle'

        else:
            display_frame = frame.copy()

        if original_frame is None and mode in ['mask', 'masked']:
            original_frame = frame.copy()

        ret, buffer = cv2.imencode('.jpg', display_frame)
        frame_bytes = buffer.tobytes()
        yield (b'--frame\r\n'
               b'Content-Type: image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')


@app.route('/video_feed')
def video_feed():
    return Response(generate_masked_frames(), mimetype='multipart/x-mixed-replace; boundary=frame')


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)