import cv2
import math
import mediapipe as mp


# 1. 設定與初始化 (Tasks API)
options = mp.tasks.vision.HandLandmarkerOptions(
    base_options=mp.tasks.BaseOptions(model_asset_path='C:/big project/model/hand_landmarker.task'),
    num_hands=2,
    running_mode=mp.tasks.vision.RunningMode.IMAGE)

# 21個節點連成線
CONNECTIONS = [(0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),(5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),(13,17),(17,18),(18,19),(19,20),(0,17)]

wrist_history=[]

# 用來暫存平滑後的節點
class SmoothedLandmark:
    def __init__(self, x, y, z):
        self.x = x
        self.y = y
        self.z = z

# 紀錄上一幀的 21 個平滑節點
prev_smoothed_landmarks = None

#=======利用平滑化(EMA) 讓數據更準確==========

def apply_ema_smoothing(current_landmarks, alpha=0.5):
    global prev_smoothed_landmarks
    smoothed_landmarks = []

    # 如果是第一幀，就直接回傳當前座標並存起來
    if prev_smoothed_landmarks is None:
        #因為目前沒有"上一幀"的資料，逐步複製當前座標
        for p in current_landmarks:
            smoothed_landmarks.append(SmoothedLandmark(p.x, p.y, p.z))
        #存到"上一幀"的資料
        prev_smoothed_landmarks = smoothed_landmarks
        return smoothed_landmarks
        

    
    # 把 21 個節點拿來做平滑計算
    for i in range(len(current_landmarks)):
        curr = current_landmarks[i]
        prev = prev_smoothed_landmarks[i]
        
        # EMA 公式
        #過去的值:現在的值=alpha:(1-alpha)
        smooth_x = alpha * curr.x + (1 - alpha) * prev.x
        smooth_y = alpha * curr.y + (1 - alpha) * prev.y
        smooth_z = alpha * curr.z + (1 - alpha) * prev.z
        
        smoothed_landmarks.append(SmoothedLandmark(smooth_x, smooth_y, smooth_z))
        
    # 將算完的平滑結果存起來
    prev_smoothed_landmarks = smoothed_landmarks
    
    return smoothed_landmarks    



# 2. 手勢判斷邏輯 
def get_distance(p1, p2):
    return math.sqrt((p1.x - p2.x)**2 + (p1.y - p2.y)**2 + (p1.z - p2.z)**2) #求距離公式

def detect_gesture(landmarks, history):
    palm_size = get_distance(landmarks[0], landmarks[5])

    #食指、中指、無名指、小指
    tips = [8, 12, 16, 20] # 指尖
    pips = [6, 10, 14, 18] # 第二指節
    
    #判斷哪隻手指有伸直
    fingers_ext = []
    for tip, pip in zip(tips, pips):
        # 指尖離手腕的距離 > 第二指節離手腕的距離->伸直
        is_ext = get_distance(landmarks[tip], landmarks[0]) > get_distance(landmarks[pip], landmarks[0])
        fingers_ext.append(is_ext)
        
    extended_count = sum(fingers_ext) # 算出伸直的手指數量 (0 ~ 4)


    # 算大拇指尖和食指尖的距離->判斷ok手勢
    thumb_index_dist = get_distance(landmarks[4], landmarks[8])
    palm_size = get_distance(landmarks[0], landmarks[5])

    #判斷大拇指有無伸出去
    is_thumb_open = thumb_index_dist > palm_size * 0.6

    is_fist = (extended_count == 0) and not is_thumb_open  # 握拳：四指全彎 + 拇指收起
    is_thumbs_up = (extended_count == 0) and is_thumb_open # 比讚：四指全彎 + 拇指伸出
    is_open_palm = (extended_count == 4)                   # 張開手：四指全伸直


    # ==========================================
    # ============靜態手勢判斷==============
    # ==========================================
    
    # --- 1👌 OK 手勢 --- (只有食指彎曲<拇指到食指距離很短>，另外三指伸直)
    if  thumb_index_dist < palm_size * 0.3 and extended_count == 3 and not fingers_ext[0]: 
        history.clear() 
        return "OK (UV Light)"

    # --- 2✌️  YA --- (食、中伸直，無、小指彎曲)
    elif fingers_ext[0] and fingers_ext[1] and not fingers_ext[2] and not fingers_ext[3]:
        history.clear()
        return "V-Sign (Flashbang)"

    # --- 3👍 比讚 (抓取道具) ---
    elif is_thumbs_up: 
        history.clear()
        return "Good (Grab Item)"



    # ==========================================
    # ============動態手勢判斷==============
    # ==========================================
    
    # --- 揮手 (Swipe) ---
    elif is_open_palm:
        history.append(landmarks[0].x)
        if len(history) > 5:
            history.pop(0)

        if len(history) >= 2:
            dx = history[-1] - history[-2]  # 每幀移動量

            # 小抖動不算
            if abs(dx) < 0.01:
                return "Open Palm (Idle)"

            # 放大控制 讓鏡頭位移較明顯
            speed = dx * 10

            if speed > 0:
                return f"Turn Left ({speed:.2f})"
            else:
                return f"Turn Right ({abs(speed):.2f})"

        return "Open Palm"
        
    # --- 跑步 (Run) ---

    elif is_fist:  #握拳且拇指收起
        history.append(landmarks[0].y)
        if len(history) > 5: history.pop(0)
            
        if len(history) == 5:
            # 算「總移動軌跡」
            total_movement = 0
            for i in range(1, 5):
                total_movement += abs(history[i] - history[i-1])

            #和手掌大小作比較 而非絕對距離 比較不會因離鏡頭遠近 而判斷失敗 
            if total_movement > (palm_size * 0.6):
                return "Running !!!"
                
        return "Run(Ready...)"

    else:
        history.clear() 
        return "None"


# 3. 繪製手的骨架
def draw_and_get_gesture(image, result):
    global prev_smoothed_landmarks
    if not result.hand_landmarks: 
        prev_smoothed_landmarks = None
        return image
    
    h, w, _ = image.shape
    for hand_lms, handedness in zip(result.hand_landmarks, result.handedness):
       #呼叫平滑化函數
       smoothed_lms = apply_ema_smoothing(hand_lms, alpha=0.5)
        # 轉換座標 
       px_pts = [(int(p.x * w), int(p.y * h)) for p in smoothed_lms]
        
        # 畫線與畫點
       for s, e in CONNECTIONS: cv2.line(image, px_pts[s], px_pts[e], (255, 255, 255), 2)
       for pt in px_pts: cv2.circle(image, pt, 4, (0, 0, 255), -1)

        # 寫出手勢名稱
       gesture = detect_gesture(smoothed_lms,wrist_history)

       cv2.putText(image, f"{handedness[0].category_name}: {gesture}", 
                    (px_pts[0][0] - 20, px_pts[0][1] - 20), 
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2)
    return image


# 4. 主程式迴圈
cap = cv2.VideoCapture(0)

with mp.tasks.vision.HandLandmarker.create_from_options(options) as landmarker:
    while cap.isOpened():
        success, frame = cap.read()
        if not success: break

        # 轉換格式並讓模型偵測
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        result = landmarker.detect(mp_image)
        
        # 畫圖與顯示
        frame = draw_and_get_gesture(frame, result)
        cv2.imshow('MR Game Controller', frame)
        
        if cv2.waitKey(5) == ord('q'): break

cap.release()
cv2.destroyAllWindows()